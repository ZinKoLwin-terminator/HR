using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Recruitment;

namespace Apex.Hrms.Application.Recruitment.Views;

/// <summary>The next scheduled interview of an application, shown on its pipeline card.</summary>
public sealed record PipelineInterviewDto(int Id, string Round, DateOnly Date, TimeOnly StartTime);

/// <summary>
/// One card of the pipeline board. <c>NextInterview</c> is the earliest scheduled interview dated today or later (null
/// when there is none); <c>CompletedInterviews</c> counts the completed ones. <c>RowVersion</c> is the application's, so a
/// card can be moved without another read.
/// </summary>
public sealed record PipelineCardDto(
    int ApplicationId,
    int JobOpeningId,
    string JobTitle,
    int CandidateId,
    string CandidateName,
    decimal? ExperienceYears,
    string Stage,
    DateOnly AppliedOn,
    string? RejectionReason,
    PipelineInterviewDto? NextInterview,
    int CompletedInterviews,
    byte[] RowVersion);

/// <summary>The cards of one stage, ordered by candidate name.</summary>
public sealed record PipelineColumnDto(string Stage, int Count, IReadOnlyList<PipelineCardDto> Cards);

/// <summary>The applications grouped by stage: always the five columns, in pipeline order.</summary>
public sealed record PipelineDto(int? JobOpeningId, IReadOnlyList<PipelineColumnDto> Columns);

/// <summary>Applications and selected candidates of one opening. Openings without applications are not listed (count 0).</summary>
public sealed record JobOpeningCountDto(int JobOpeningId, int Applications, int Selected);

/// <summary>Applications of one candidate. Candidates without applications are not listed (count 0).</summary>
public sealed record CandidateCountDto(int CandidateId, int Applications);

public sealed record RecruitmentCountsDto(
    IReadOnlyList<JobOpeningCountDto> JobOpenings,
    IReadOnlyList<CandidateCountDto> Candidates);

public interface IRecruitmentPipelineHandler
{
    /// <param name="jobOpeningId">Only this opening's applications; null for every opening.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<PipelineDto>> HandleAsync(int? jobOpeningId, CancellationToken cancellationToken);
}

public interface IRecruitmentCountsHandler
{
    Task<Result<RecruitmentCountsDto>> HandleAsync(CancellationToken cancellationToken);
}

internal sealed class RecruitmentPipelineHandler(
    IRepository<JobApplication> applications,
    IRepository<Candidate> candidates,
    IRepository<JobOpening> openings,
    IRepository<Interview> interviews,
    IClock clock) : IRecruitmentPipelineHandler
{
    public async Task<Result<PipelineDto>> HandleAsync(int? jobOpeningId, CancellationToken cancellationToken)
    {
        if (jobOpeningId is { } id && await openings.FindAsync(id, cancellationToken) is null)
        {
            return Result.Failure<PipelineDto>(NotFoundError.For("Job opening", id));
        }

        var rows = await applications.ListAsync(
            application => jobOpeningId == null || application.JobOpeningId == jobOpeningId,
            cancellationToken);
        var applicationIds = rows.Select(application => application.Id).ToList();
        var candidateIds = rows.Select(application => application.CandidateId).Distinct().ToList();
        var openingIds = rows.Select(application => application.JobOpeningId).Distinct().ToList();

        var people = (await candidates.ListAsync(candidate => candidateIds.Contains(candidate.Id), cancellationToken))
            .ToDictionary(candidate => candidate.Id);
        var jobs = (await openings.ListAsync(opening => openingIds.Contains(opening.Id), cancellationToken))
            .ToDictionary(opening => opening.Id);
        var byApplication = (await interviews.ListAsync(interview => applicationIds.Contains(interview.ApplicationId), cancellationToken))
            .ToLookup(interview => interview.ApplicationId);

        var today = clock.Today;
        var cards = rows.Select(application =>
        {
            people.TryGetValue(application.CandidateId, out var candidate);
            jobs.TryGetValue(application.JobOpeningId, out var job);
            var mine = byApplication[application.Id].ToList();
            var next = mine
                .Where(interview => interview.Status == InterviewStatus.Scheduled && interview.Date >= today)
                .OrderBy(interview => interview.Date)
                .ThenBy(interview => interview.StartTime)
                .ThenBy(interview => interview.Id)
                .FirstOrDefault();
            return new PipelineCardDto(
                application.Id,
                application.JobOpeningId,
                job?.Title ?? "—",
                application.CandidateId,
                candidate?.FullName ?? $"Candidate #{application.CandidateId}",
                candidate?.ExperienceYears,
                ApplicationStageText.ToText(application.Stage),
                application.AppliedOn,
                application.RejectionReason,
                next is null ? null : new PipelineInterviewDto(next.Id, InterviewRoundText.ToText(next.Round), next.Date, next.StartTime),
                mine.Count(interview => interview.Status == InterviewStatus.Completed),
                application.RowVersion);
        }).ToList();

        var columns = Enum.GetValues<ApplicationStage>().Select(stage =>
        {
            var text = ApplicationStageText.ToText(stage);
            var inStage = cards
                .Where(card => card.Stage == text)
                .OrderBy(card => card.CandidateName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(card => card.ApplicationId)
                .ToList();
            return new PipelineColumnDto(text, inStage.Count, inStage);
        }).ToList();
        return Result.Success(new PipelineDto(jobOpeningId, columns));
    }
}

internal sealed class RecruitmentCountsHandler(IRepository<JobApplication> applications) : IRecruitmentCountsHandler
{
    public async Task<Result<RecruitmentCountsDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var all = await applications.ListAsync(Predicates.Always<JobApplication>(), cancellationToken);
        var openings = all
            .GroupBy(application => application.JobOpeningId)
            .OrderBy(group => group.Key)
            .Select(group => new JobOpeningCountDto(group.Key, group.Count(), group.Count(a => a.Stage == ApplicationStage.Selected)))
            .ToList();
        var candidates = all
            .GroupBy(application => application.CandidateId)
            .OrderBy(group => group.Key)
            .Select(group => new CandidateCountDto(group.Key, group.Count()))
            .ToList();
        return Result.Success(new RecruitmentCountsDto(openings, candidates));
    }
}
