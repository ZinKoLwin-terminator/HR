using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Recruitment;
using FluentValidation;

namespace Apex.Hrms.Application.Recruitment.Applications;

/// <summary>
/// Applications need their own create, update, delete and move flows: the stage, the date and the pairing of opening and
/// candidate are owned by the server, and every rule reads other records (the opening, the candidate, interviews).
/// Reads use the generic handlers.
/// </summary>
internal sealed class JobApplicationCreateHandler(
    IValidator<JobApplicationCreateRequest> validator,
    IRepository<JobOpening> openings,
    IRepository<Candidate> candidates,
    IRepository<JobApplication> applications,
    IClock clock,
    IActivityRecorder activity,
    RecruitmentLookup lookup,
    IUnitOfWork unitOfWork,
    JobApplicationMapping mapper) : ICreateHandler<JobApplicationCreateRequest, JobApplicationDto>
{
    public async Task<Result<JobApplicationDto>> HandleAsync(JobApplicationCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(valid.Error);
        }

        var opening = await openings.FindAsync(request.JobOpeningId, cancellationToken);
        if (opening is null)
        {
            return Result.Failure<JobApplicationDto>(AppError.Validation(
                "Choose a job opening.", "jobOpeningId", "Choose a job opening."));
        }

        if (await candidates.FindAsync(request.CandidateId, cancellationToken) is null)
        {
            return Result.Failure<JobApplicationDto>(AppError.Validation(
                "Choose a candidate.", "candidateId", "Choose a candidate."));
        }

        if (!opening.AcceptsApplications)
        {
            var status = JobOpeningStatusText.ToText(opening.Status).ToLowerInvariant();
            return Result.Failure<JobApplicationDto>(AppError.Validation(
                $"\"{opening.Title}\" is {status} and not accepting applications.",
                "jobOpeningId",
                $"This opening is {status}."));
        }

        var jobId = request.JobOpeningId;
        var candidateId = request.CandidateId;
        if (await applications.AnyAsync(a => a.JobOpeningId == jobId && a.CandidateId == candidateId, cancellationToken))
        {
            return Result.Failure<JobApplicationDto>(AppError.Validation(
                "This candidate has already applied for this job.",
                "candidateId",
                "Already applied for this opening."));
        }

        var created = JobApplication.Create(jobId, candidateId, request.Notes, clock.Today);
        applications.Add(created);
        activity.Record(mapper.ActivityCategory, ActivityMessages.Created(mapper.Label, await lookup.ApplicationLabelAsync(created, cancellationToken)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<JobApplicationDto>(saved.Error)
            : Result.Success(mapper.ToDto(created));
    }
}

internal sealed class JobApplicationUpdateHandler(
    IValidator<JobApplicationUpdateRequest> validator,
    EntityLoader<JobApplication> loader,
    IActivityRecorder activity,
    RecruitmentLookup lookup,
    IUnitOfWork unitOfWork,
    JobApplicationMapping mapper) : IUpdateHandler<JobApplicationUpdateRequest, JobApplicationDto>
{
    public async Task<Result<JobApplicationDto>> HandleAsync(int id, JobApplicationUpdateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(loaded.Error);
        }

        var application = loaded.Value;
        if (application.JobOpeningId != request.JobOpeningId || application.CandidateId != request.CandidateId)
        {
            return Result.Failure<JobApplicationDto>(AppError.Validation("The job opening and candidate cannot be changed."));
        }

        application.UpdateNotes(request.Notes);
        activity.Record(mapper.ActivityCategory, ActivityMessages.Updated(mapper.Label, await lookup.ApplicationLabelAsync(application, cancellationToken)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<JobApplicationDto>(saved.Error)
            : Result.Success(mapper.ToDto(application));
    }
}

/// <summary>Deletes an application unless it is Selected (reject it first) or interviews still point at it.</summary>
internal sealed class JobApplicationDeleteHandler(
    EntityLoader<JobApplication> loader,
    IRepository<Candidate> candidates,
    IRepository<JobOpening> openings,
    IEnumerable<IReferenceCheck<JobApplication>> references,
    IRepository<JobApplication> repository,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    JobApplicationMapping mapper) : IDeleteHandler<JobApplicationDto>
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var allowed = loaded.Value.EnsureCanDelete();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var usage = await ReferenceUsage.SummarizeAsync(references, ReferenceUse.Delete, id, cancellationToken);
        if (usage is not null)
        {
            var label = await ApplicationLabel.OfAsync(loaded.Value, candidates, openings, cancellationToken);
            return Result.Failure(AppError.Conflict(
                $"\"{label}\" cannot be deleted because it is used by {usage}. Deactivate it instead."));
        }

        activity.Record(
            mapper.ActivityCategory,
            ActivityMessages.Deleted(mapper.Label, await ApplicationLabel.OfAsync(loaded.Value, candidates, openings, cancellationToken)));
        repository.Remove(loaded.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>"Candidate for Job title", as the mock names an application in messages.</summary>
internal static class ApplicationLabel
{
    public static async Task<string> OfAsync(
        JobApplication application,
        IRepository<Candidate> candidates,
        IRepository<JobOpening> openings,
        CancellationToken cancellationToken)
    {
        var candidate = await candidates.FindAsync(application.CandidateId, cancellationToken);
        var opening = await openings.FindAsync(application.JobOpeningId, cancellationToken);
        return $"{candidate?.FullName ?? $"candidate #{application.CandidateId}"} for {opening?.Title ?? $"job #{application.JobOpeningId}"}";
    }
}

/// <summary>
/// Moves an application to another stage. The transition table comes first; rejecting needs a reason; any other move
/// needs the opening to be Open or On Hold; selecting also needs a completed interview with a Hire or Strong hire
/// recommendation and a vacancy that is still free. A rejection cancels the scheduled interviews of the application;
/// the opening becomes Filled when its vacancies are taken (and Open again when a selection drops out).
/// </summary>
internal sealed class MoveApplicationStageHandler(
    EntityLoader<JobApplication> loader,
    IRepository<JobOpening> openings,
    IRepository<Interview> interviews,
    RecruitmentLookup lookup,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    JobApplicationMapping mapper) : ICommandHandler<MoveApplicationStageCommand, JobApplicationDto>
{
    public const string RejectedNote = "Cancelled: the application was rejected.";

    public async Task<Result<JobApplicationDto>> HandleAsync(MoveApplicationStageCommand command, CancellationToken cancellationToken)
    {
        var shape = CheckShape(command, out var stage);
        if (shape.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(shape.Error);
        }

        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(loaded.Error);
        }

        var application = loaded.Value;
        var transition = application.EnsureCanMoveTo(stage);
        if (transition.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(transition.Error);
        }

        var opening = await openings.FindAsync(application.JobOpeningId, cancellationToken);
        if (opening is null)
        {
            return Result.Failure<JobApplicationDto>(AppError.Conflict("The job opening of this application no longer exists."));
        }

        var otherSelected = await lookup.SelectedCountAsync(opening.Id, cancellationToken, exceptApplicationId: application.Id);
        var allowed = stage == ApplicationStage.Rejected
            ? Result.Success()
            : await CheckForwardMoveAsync(application, opening, stage, otherSelected, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(allowed.Error);
        }

        var moved = application.MoveTo(stage, command.Reason);
        if (moved.IsFailure)
        {
            return Result.Failure<JobApplicationDto>(moved.Error);
        }

        if (stage == ApplicationStage.Rejected)
        {
            await CancelScheduledInterviewsAsync(application.Id, cancellationToken);
        }

        // The mock logs the opening first (filled or reopened), then the move itself.
        var filling = opening.SyncFilled(otherSelected + (stage == ApplicationStage.Selected ? 1 : 0));
        if (filling == JobOpeningStatus.Filled)
        {
            activity.Record(mapper.ActivityCategory, ActivityMessages.JobOpeningFilled(opening.Label));
        }
        else if (filling == JobOpeningStatus.Open)
        {
            activity.Record(mapper.ActivityCategory, ActivityMessages.JobOpeningReopened(opening.Label));
        }

        activity.Record(
            mapper.ActivityCategory,
            ActivityMessages.StageMoved(await lookup.ApplicationLabelAsync(application, cancellationToken), ApplicationStageText.ToText(stage)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<JobApplicationDto>(saved.Error)
            : Result.Success(mapper.ToDto(application));
    }

    private static Result CheckShape(MoveApplicationStageCommand command, out ApplicationStage stage)
    {
        if (!ApplicationStageText.TryParse(command.Stage?.Trim(), out stage))
        {
            return Result.Failure(AppError.Validation(
                $"Stage must be {ApplicationStageText.Map.OneOf}.",
                "stage",
                $"Stage must be {ApplicationStageText.Map.OneOf}."));
        }

        return command.Reason?.Trim().Length > JobApplication.ReasonMaxLength
            ? Result.Failure(AppError.Validation(
                $"Reason must be at most {JobApplication.ReasonMaxLength} characters.",
                "reason",
                $"Reason must be at most {JobApplication.ReasonMaxLength} characters."))
            : Result.Success();
    }

    private async Task<Result> CheckForwardMoveAsync(
        JobApplication application,
        JobOpening opening,
        ApplicationStage stage,
        int otherSelected,
        CancellationToken cancellationToken)
    {
        if (!opening.AllowsProgress)
        {
            return Result.Failure(AppError.Conflict(
                $"\"{opening.Title}\" is {JobOpeningStatusText.ToText(opening.Status).ToLowerInvariant()}, so applications cannot move forward."));
        }

        if (stage != ApplicationStage.Selected)
        {
            return Result.Success();
        }

        var applicationId = application.Id;
        var positive = await interviews.AnyAsync(
            interview => interview.ApplicationId == applicationId
                && interview.Status == InterviewStatus.Completed
                && (interview.Recommendation == InterviewRecommendation.Hire
                    || interview.Recommendation == InterviewRecommendation.StrongHire),
            cancellationToken);
        if (!positive)
        {
            return Result.Failure(AppError.Conflict(
                "Selecting a candidate needs a completed interview with a \"Hire\" recommendation."));
        }

        // The mock counts every selected application, this one included only if it is already selected (it cannot be:
        // Selected does not lead to Selected), so the other selections are all there are.
        return otherSelected >= opening.Vacancies
            ? Result.Failure(AppError.Conflict(
                $"All {opening.Vacancies} vacancies for \"{opening.Title}\" already have a selected candidate."))
            : Result.Success();
    }

    private async Task CancelScheduledInterviewsAsync(int applicationId, CancellationToken cancellationToken)
    {
        var scheduled = await interviews.ListAsync(
            interview => interview.ApplicationId == applicationId && interview.Status == InterviewStatus.Scheduled,
            cancellationToken);
        foreach (var listed in scheduled)
        {
            // The list is not tracked; load the interview again so that the change is saved.
            if (await interviews.FindAsync(listed.Id, cancellationToken) is { } interview)
            {
                interview.Cancel(RejectedNote);
            }
        }
    }
}
