using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Recruitment;
using FluentValidation;

namespace Apex.Hrms.Application.Recruitment.Interviews;

/// <summary>
/// The rules of an interview that need other records: the application exists, the interviewer is a current employee, a
/// new scheduled interview belongs to an application in Screening or Interview, and the interviewer is not double booked.
/// </summary>
internal sealed class InterviewRules(
    IRepository<JobApplication> applications,
    IRepository<Interview> interviews,
    RecruitmentLookup lookup)
{
    public async Task<Result<JobApplication>> CheckReferencesAsync(InterviewDetails details, CancellationToken cancellationToken)
    {
        // Tracked, so that scheduling can move the application on to the Interview stage.
        var application = await applications.FindAsync(details.ApplicationId, cancellationToken);
        if (application is null)
        {
            return Result.Failure<JobApplication>(AppError.Validation(
                "Choose an application.", "applicationId", "Choose an application."));
        }

        var interviewer = await lookup.EnsureCurrentEmployeeAsync(
            details.InterviewerId,
            "Choose a current employee as interviewer.",
            "interviewerId",
            cancellationToken);
        return interviewer.IsFailure ? Result.Failure<JobApplication>(interviewer.Error) : Result.Success(application);
    }

    /// <summary>A new interview is scheduled only for candidates being screened or interviewed.</summary>
    public static Result EnsureStageAllowsScheduling(JobApplication application) =>
        application.Stage is ApplicationStage.Screening or ApplicationStage.Interview
            ? Result.Success()
            : Result.Failure(AppError.Validation(
                $"Interviews can be scheduled for candidates in Screening or Interview (this one is {ApplicationStageText.ToText(application.Stage)}).",
                "applicationId",
                $"Application is at \"{ApplicationStageText.ToText(application.Stage)}\"."));

    /// <summary>The interviewer has no other scheduled interview on the same day that shares a minute with this one.</summary>
    public async Task<Result> EnsureInterviewerFreeAsync(InterviewDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var interviewerId = details.InterviewerId;
        var date = details.Date;
        var sameDay = await interviews.ListAsync(
            interview => interview.Id != excludedId
                && interview.Status == InterviewStatus.Scheduled
                && interview.InterviewerEmployeeId == interviewerId
                && interview.Date == date,
            cancellationToken);
        var start = Interview.MinuteOf(details.StartTime);
        var clash = sameDay.FirstOrDefault(other =>
            Interview.Overlap(start, details.DurationMinutes, other.StartMinute, other.DurationMinutes));
        if (clash is null)
        {
            return Result.Success();
        }

        var busyFrom = clash.StartTime.ToString("HH':'mm", CultureInfo.InvariantCulture);
        return Result.Failure(AppError.Validation(
            $"The interviewer already has an interview at {busyFrom} on that day.",
            "startTime",
            $"Interviewer is busy from {busyFrom}."));
    }
}

internal sealed class InterviewCreateHandler(
    IValidator<InterviewCreateRequest> validator,
    InterviewRules rules,
    IClock clock,
    IRepository<Interview> repository,
    IUnitOfWork unitOfWork,
    InterviewMapping mapper) : ICreateHandler<InterviewCreateRequest, InterviewDto>
{
    public async Task<Result<InterviewDto>> HandleAsync(InterviewCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<InterviewDto>(valid.Error);
        }

        var details = request.ToDetails();
        var today = clock.Today;
        var completed = request.Status?.Trim() == "Completed";
        var interview = completed ? RecordCompleted(request, details, today) : Schedule(request, details, today);
        if (interview.IsFailure)
        {
            return Result.Failure<InterviewDto>(interview.Error);
        }

        var references = await rules.CheckReferencesAsync(details, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<InterviewDto>(references.Error);
        }

        if (!completed)
        {
            var allowed = InterviewRules.EnsureStageAllowsScheduling(references.Value);
            if (allowed.IsFailure)
            {
                return Result.Failure<InterviewDto>(allowed.Error);
            }

            var free = await rules.EnsureInterviewerFreeAsync(details, excludedId: 0, cancellationToken);
            if (free.IsFailure)
            {
                return Result.Failure<InterviewDto>(free.Error);
            }
        }

        references.Value.AdvanceToInterview();
        repository.Add(interview.Value);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<InterviewDto>(saved.Error)
            : Result.Success(mapper.ToDto(interview.Value));
    }

    private static Result<Interview> Schedule(InterviewCreateRequest request, InterviewDetails details, DateOnly today)
    {
        if (request.Score is not null || !string.IsNullOrWhiteSpace(request.Recommendation))
        {
            return Result.Failure<Interview>(AppError.Validation(
                "Only completed interviews can have a score or recommendation.",
                "score",
                "Complete the interview first."));
        }

        return Interview.Schedule(details, today);
    }

    private static Result<Interview> RecordCompleted(InterviewCreateRequest request, InterviewDetails details, DateOnly today)
    {
        var recommendation = string.IsNullOrWhiteSpace(request.Recommendation)
            ? (InterviewRecommendation?)null
            : InterviewRecommendationText.Parse(request.Recommendation.Trim());
        return Interview.RecordCompleted(details, new InterviewOutcome(request.Score, recommendation, request.Feedback), today);
    }
}

internal sealed class InterviewUpdateHandler(
    IValidator<InterviewUpdateRequest> validator,
    EntityLoader<Interview> loader,
    InterviewRules rules,
    IClock clock,
    IUnitOfWork unitOfWork,
    InterviewMapping mapper) : IUpdateHandler<InterviewUpdateRequest, InterviewDto>
{
    public async Task<Result<InterviewDto>> HandleAsync(int id, InterviewUpdateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<InterviewDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<InterviewDto>(loaded.Error);
        }

        var interview = loaded.Value;
        var details = request.ToDetails();
        var consistent = interview.ValidateUpdate(details, clock.Today);
        if (consistent.IsFailure)
        {
            return Result.Failure<InterviewDto>(consistent.Error);
        }

        var references = await rules.CheckReferencesAsync(details, cancellationToken);
        if (references.IsFailure)
        {
            return Result.Failure<InterviewDto>(references.Error);
        }

        if (interview.Status == InterviewStatus.Scheduled)
        {
            var free = await rules.EnsureInterviewerFreeAsync(details, interview.Id, cancellationToken);
            if (free.IsFailure)
            {
                return Result.Failure<InterviewDto>(free.Error);
            }
        }

        var updated = interview.Update(details, clock.Today);
        if (updated.IsFailure)
        {
            return Result.Failure<InterviewDto>(updated.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<InterviewDto>(saved.Error)
            : Result.Success(mapper.ToDto(interview));
    }
}

/// <summary>Deletes an interview unless it is Completed: a completed interview is part of the hiring record.</summary>
internal sealed class InterviewDeleteHandler(
    EntityLoader<Interview> loader,
    IRepository<Interview> repository,
    IUnitOfWork unitOfWork) : IDeleteHandler<InterviewDto>
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

        repository.Remove(loaded.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Records the outcome of an interview. A completed interview can be corrected, but not so that a selected candidate
/// loses the last "Hire" or "Strong hire" recommendation that supports the selection.
/// </summary>
internal sealed class RecordInterviewOutcomeHandler(
    EntityLoader<Interview> loader,
    IRepository<JobApplication> applications,
    IRepository<Interview> interviews,
    IClock clock,
    IUnitOfWork unitOfWork,
    InterviewMapping mapper) : ICommandHandler<RecordInterviewOutcomeCommand, InterviewDto>
{
    public async Task<Result<InterviewDto>> HandleAsync(RecordInterviewOutcomeCommand command, CancellationToken cancellationToken)
    {
        var recommendation = ParseRecommendation(command.Recommendation);
        if (recommendation.IsFailure)
        {
            return Result.Failure<InterviewDto>(recommendation.Error);
        }

        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<InterviewDto>(loaded.Error);
        }

        var interview = loaded.Value;
        var outcome = new InterviewOutcome(command.Score, recommendation.Value, command.Feedback);
        var allowed = interview.CheckOutcome(outcome, clock.Today);
        if (allowed.IsFailure)
        {
            return Result.Failure<InterviewDto>(allowed.Error);
        }

        var supportsSelection = await EnsureSelectionStaysSupportedAsync(interview, recommendation.Value, cancellationToken);
        if (supportsSelection.IsFailure)
        {
            return Result.Failure<InterviewDto>(supportsSelection.Error);
        }

        var completed = interview.Complete(outcome, clock.Today);
        if (completed.IsFailure)
        {
            return Result.Failure<InterviewDto>(completed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<InterviewDto>(saved.Error)
            : Result.Success(mapper.ToDto(interview));
    }

    private static Result<InterviewRecommendation?> ParseRecommendation(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Success<InterviewRecommendation?>(null);
        }

        return InterviewRecommendationText.TryParse(text.Trim(), out var value)
            ? Result.Success<InterviewRecommendation?>(value)
            : Result.Failure<InterviewRecommendation?>(AppError.Validation(
                $"Recommendation must be {InterviewRecommendationText.Map.OneOf}.",
                "recommendation",
                $"Recommendation must be {InterviewRecommendationText.Map.OneOf}."));
    }

    private async Task<Result> EnsureSelectionStaysSupportedAsync(
        Interview interview,
        InterviewRecommendation? recommendation,
        CancellationToken cancellationToken)
    {
        var losesSupport = interview.Status == InterviewStatus.Completed
            && InterviewRecommendationText.IsPositive(interview.Recommendation)
            && !InterviewRecommendationText.IsPositive(recommendation);
        if (!losesSupport)
        {
            return Result.Success();
        }

        var application = await applications.FindAsync(interview.ApplicationId, cancellationToken);
        if (application?.Stage != ApplicationStage.Selected)
        {
            return Result.Success();
        }

        var applicationId = interview.ApplicationId;
        var interviewId = interview.Id;
        var otherSupport = await interviews.AnyAsync(
            other => other.ApplicationId == applicationId
                && other.Id != interviewId
                && other.Status == InterviewStatus.Completed
                && (other.Recommendation == InterviewRecommendation.Hire
                    || other.Recommendation == InterviewRecommendation.StrongHire),
            cancellationToken);
        return otherSupport
            ? Result.Success()
            : Result.Failure(AppError.Conflict(
                "This interview supports the selection of the candidate. Reject the application first, or keep a \"Hire\" recommendation."));
    }
}

/// <summary>Cancels a scheduled interview; afterwards it is locked.</summary>
internal sealed class CancelInterviewHandler(
    EntityLoader<Interview> loader,
    IUnitOfWork unitOfWork,
    InterviewMapping mapper) : ICommandHandler<CancelInterviewCommand, InterviewDto>
{
    public async Task<Result<InterviewDto>> HandleAsync(CancelInterviewCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<InterviewDto>(loaded.Error);
        }

        var cancelled = loaded.Value.Cancel(command.Reason);
        if (cancelled.IsFailure)
        {
            return Result.Failure<InterviewDto>(cancelled.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<InterviewDto>(saved.Error)
            : Result.Success(mapper.ToDto(loaded.Value));
    }
}
