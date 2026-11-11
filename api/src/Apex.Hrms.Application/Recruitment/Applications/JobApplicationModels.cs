using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Recruitment;
using FluentValidation;

namespace Apex.Hrms.Application.Recruitment.Applications;

/// <summary>
/// Mirrors the Application interface in recruitment.models.ts. <c>Stage</c> is "Applied", "Screening", "Interview",
/// "Selected" or "Rejected"; <c>RejectionReason</c> is set only when the stage is Rejected.
/// </summary>
public sealed record JobApplicationDto(
    int Id,
    int JobOpeningId,
    int CandidateId,
    string Stage,
    DateOnly AppliedOn,
    string? RejectionReason,
    string? Notes,
    byte[] RowVersion) : IIdentified;

/// <summary>
/// POST body. The application starts at Applied and is dated today by the server; it is only accepted while the
/// opening is Open and the candidate has not applied for it before.
/// </summary>
public sealed record JobApplicationCreateRequest(int JobOpeningId, int CandidateId, string? Notes);

/// <summary>
/// PUT body. Only the notes can change: the opening and the candidate are sent back unchanged (a different one is
/// refused) and the stage moves through <see cref="MoveApplicationStageCommand"/>. Requires the row version.
/// </summary>
public sealed record JobApplicationUpdateRequest(int JobOpeningId, int CandidateId, string? Notes, string? RowVersion)
    : IVersionedRequest;

/// <summary>Filters of the list; <c>stage</c> is the text of a stage, "all" or blank for no filter.</summary>
public sealed record JobApplicationFilter(int? JobOpeningId = null, int? CandidateId = null, string? Stage = null);

/// <summary>
/// Moves an application along the pipeline (<c>Stage</c> is the text of the target stage). <c>Reason</c> is required
/// when rejecting and ignored otherwise.
/// </summary>
public sealed record MoveApplicationStageCommand(int Id, string? RowVersion, string? Stage, string? Reason)
    : IEntityCommand;

internal sealed class JobApplicationMapping :
    IEntityDescriptor<JobApplication>,
    IDtoMapper<JobApplication, JobApplicationDto>
{
    public string Label => "Application";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Recruitment;

    public string Describe(JobApplication entity) => $"application #{entity.Id}";

    public JobApplicationDto ToDto(JobApplication entity) => new(
        entity.Id,
        entity.JobOpeningId,
        entity.CandidateId,
        ApplicationStageText.ToText(entity.Stage),
        entity.AppliedOn,
        entity.RejectionReason,
        entity.Notes,
        entity.RowVersion);
}

internal sealed class JobApplicationQuerySpecification : IQuerySpecification<JobApplication, JobApplicationFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<JobApplication, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<JobApplication, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = a => a.Id,
            ["jobOpeningId"] = a => a.JobOpeningId,
            ["candidateId"] = a => a.CandidateId,
            ["stage"] = a => a.Stage,
            ["appliedOn"] = a => a.AppliedOn,
        };

    public Expression<Func<JobApplication, bool>> Search(string term) =>
        Predicates.ContainsAny<JobApplication>(term, a => a.Notes, a => a.RejectionReason);

    public Expression<Func<JobApplication, bool>> Filter(JobApplicationFilter filter) => Predicates.Always<JobApplication>()
        .And(filter.JobOpeningId is { } jobId ? a => a.JobOpeningId == jobId : Predicates.Always<JobApplication>())
        .And(filter.CandidateId is { } candidateId ? a => a.CandidateId == candidateId : Predicates.Always<JobApplication>())
        .And(StageFilter(filter.Stage));

    private static Expression<Func<JobApplication, bool>> StageFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<JobApplication>();
        }

        return ApplicationStageText.TryParse(text, out var stage)
            ? a => a.Stage == stage
            : Predicates.Never<JobApplication>();
    }
}

/// <summary>Limits of an application that follow from the database columns.</summary>
public static class JobApplicationLimits
{
    public const int NotesMaxLength = 300;
    public const int ReasonMaxLength = JobApplication.ReasonMaxLength;
}

internal sealed class JobApplicationCreateRequestValidator : RequestValidator<JobApplicationCreateRequest>
{
    public JobApplicationCreateRequestValidator() =>
        RuleForText(x => x.Notes, "Notes").OptionalText(JobApplicationLimits.NotesMaxLength);
}

internal sealed class JobApplicationUpdateRequestValidator : RequestValidator<JobApplicationUpdateRequest>
{
    public JobApplicationUpdateRequestValidator()
    {
        RuleForText(x => x.Notes, "Notes").OptionalText(JobApplicationLimits.NotesMaxLength);
        RuleForText(x => x.RowVersion).RequiredRowVersion();
    }
}
