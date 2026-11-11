using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Recruitment;
using FluentValidation;

namespace Apex.Hrms.Application.Recruitment.Interviews;

/// <summary>
/// Mirrors the Interview interface in recruitment.models.ts. <c>Round</c> is "Phone screen", "Technical", "HR" or
/// "Final"; <c>Mode</c> "Onsite", "Video" or "Phone"; <c>Status</c> "Scheduled", "Completed" or "Cancelled";
/// <c>Recommendation</c> "Strong hire", "Hire" or "No hire". <c>StartTime</c> is "HH:mm". Score, recommendation and
/// feedback (the interviewer's opinion of a candidate) are sensitive.
/// </summary>
public sealed record InterviewDto(
    int Id,
    int ApplicationId,
    string Round,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    string Mode,
    int InterviewerId,
    string? Location,
    string Status,
    int? Score,
    string? Recommendation,
    string? Feedback,
    byte[] RowVersion) : IIdentified;

internal interface IInterviewFields
{
    int ApplicationId { get; }

    string Round { get; }

    DateOnly Date { get; }

    TimeOnly StartTime { get; }

    int DurationMinutes { get; }

    string Mode { get; }

    int InterviewerId { get; }

    string? Location { get; }
}

internal static class InterviewDetailsFactory
{
    /// <summary>Call only after the request validator passed (the round and mode texts are known to be valid).</summary>
    public static InterviewDetails From(IInterviewFields fields) => new InterviewDetails(
        fields.ApplicationId,
        InterviewRoundText.Parse(fields.Round.Trim()),
        fields.Date,
        fields.StartTime,
        fields.DurationMinutes,
        InterviewModeText.Parse(fields.Mode.Trim()),
        fields.InterviewerId,
        fields.Location).Normalized();
}

/// <summary>
/// POST body. <c>Status</c> is "Scheduled" (the default: the date cannot be in the past) or "Completed" (an interview
/// that already took place: a score from 1 to 5 and a recommendation are required and the date cannot be in the
/// future). Time is "HH:mm" in the business time zone; the server checks the date against its own clock.
/// </summary>
public sealed record InterviewCreateRequest(
    int ApplicationId,
    string Round,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    string Mode,
    int InterviewerId,
    string? Location,
    string? Status = null,
    int? Score = null,
    string? Recommendation = null,
    string? Feedback = null) : IInterviewFields
{
    public InterviewDetails ToDetails() => InterviewDetailsFactory.From(this);
}

/// <summary>
/// PUT body: reschedules a scheduled interview (or corrects the details of a completed one). The application never
/// changes, a cancelled interview is locked, and the status and outcome move through commands. Requires the row version.
/// </summary>
public sealed record InterviewUpdateRequest(
    int ApplicationId,
    string Round,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMinutes,
    string Mode,
    int InterviewerId,
    string? Location,
    string? RowVersion) : IInterviewFields, IVersionedRequest
{
    public InterviewDetails ToDetails() => InterviewDetailsFactory.From(this);
}

/// <summary>Filters of the list: equality fields and an inclusive date range (<c>dateFrom</c>, <c>dateTo</c>).</summary>
public sealed record InterviewFilter(
    string? Status = null,
    int? InterviewerId = null,
    int? ApplicationId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

/// <summary>Records the outcome: Scheduled (or Completed, to correct it) becomes Completed. Score 1 to 5 and recommendation are required.</summary>
public sealed record RecordInterviewOutcomeCommand(int Id, string? RowVersion, int? Score, string? Recommendation, string? Feedback)
    : IEntityCommand;

/// <summary>Scheduled to Cancelled. The reason is optional and is kept as the interview's feedback.</summary>
public sealed record CancelInterviewCommand(int Id, string? RowVersion, string? Reason) : IEntityCommand;

internal sealed class InterviewMapping :
    IEntityDescriptor<Interview>,
    IDtoMapper<Interview, InterviewDto>
{
    public string Label => "Interview";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Recruitment;

    public string Describe(Interview entity) => $"{InterviewRoundText.ToText(entity.Round)} interview #{entity.Id}";

    public InterviewDto ToDto(Interview entity) => new(
        entity.Id,
        entity.ApplicationId,
        InterviewRoundText.ToText(entity.Round),
        entity.Date,
        entity.StartTime,
        entity.DurationMinutes,
        InterviewModeText.ToText(entity.Mode),
        entity.InterviewerEmployeeId,
        entity.Location,
        InterviewStatusText.ToText(entity.Status),
        entity.Score,
        entity.Recommendation is { } recommendation ? InterviewRecommendationText.ToText(recommendation) : null,
        entity.Feedback,
        entity.RowVersion);
}

internal sealed class InterviewQuerySpecification : IQuerySpecification<Interview, InterviewFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Interview, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Interview, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = i => i.Id,
            ["applicationId"] = i => i.ApplicationId,
            ["round"] = i => i.Round,
            ["date"] = i => i.Date,
            ["startTime"] = i => i.StartTime,
            ["durationMinutes"] = i => i.DurationMinutes,
            ["mode"] = i => i.Mode,
            ["interviewerId"] = i => i.InterviewerEmployeeId,
            ["status"] = i => i.Status,
            ["score"] = i => i.Score ?? 0,
        };

    public Expression<Func<Interview, bool>> Search(string term) =>
        Predicates.ContainsAny<Interview>(term, i => i.Location, i => i.Feedback);

    public Expression<Func<Interview, bool>> Filter(InterviewFilter filter) => Predicates.Always<Interview>()
        .And(StatusFilter(filter.Status))
        .And(filter.InterviewerId is { } interviewerId ? i => i.InterviewerEmployeeId == interviewerId : Predicates.Always<Interview>())
        .And(filter.ApplicationId is { } applicationId ? i => i.ApplicationId == applicationId : Predicates.Always<Interview>())
        .And(filter.DateFrom is { } from ? i => i.Date >= from : Predicates.Always<Interview>())
        .And(filter.DateTo is { } to ? i => i.Date <= to : Predicates.Always<Interview>());

    private static Expression<Func<Interview, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<Interview>();
        }

        return InterviewStatusText.TryParse(text, out var status)
            ? i => i.Status == status
            : Predicates.Never<Interview>();
    }
}

/// <summary>Limits of an interview that follow from the database columns.</summary>
public static class InterviewLimits
{
    public const int LocationMaxLength = 120;
    public const int FeedbackMaxLength = Interview.FeedbackMaxLength;
}

/// <summary>Shape rules shared by the create and update requests. The domain owns duration, dates and the outcome.</summary>
internal abstract class InterviewFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IInterviewFields
{
    protected InterviewFieldsValidator()
    {
        RuleForText(x => x.Location, "Location").OptionalText(InterviewLimits.LocationMaxLength);
        RuleForText(x => x.Round, "Round")
            .NotEmpty().WithMessage("Round is required.")
            .Must(round => InterviewRoundText.TryParse(round?.Trim(), out _))
            .WithMessage($"Round must be {InterviewRoundText.Map.OneOf}.");
        RuleForText(x => x.Mode, "Mode")
            .NotEmpty().WithMessage("Mode is required.")
            .Must(mode => InterviewModeText.TryParse(mode?.Trim(), out _))
            .WithMessage($"Mode must be {InterviewModeText.Map.OneOf}.");
    }
}

internal sealed class InterviewCreateRequestValidator : InterviewFieldsValidator<InterviewCreateRequest>
{
    public InterviewCreateRequestValidator()
    {
        RuleForText(x => x.Feedback, "Feedback").OptionalText(InterviewLimits.FeedbackMaxLength);
        RuleForText(x => x.Status, "Status")
            .Must(status => string.IsNullOrWhiteSpace(status) || status.Trim() is "Scheduled" or "Completed")
            .WithMessage("Status must be Scheduled or Completed.");
        RuleForText(x => x.Recommendation, "Recommendation")
            .Must(text => string.IsNullOrWhiteSpace(text) || InterviewRecommendationText.TryParse(text.Trim(), out _))
            .WithMessage($"Recommendation must be {InterviewRecommendationText.Map.OneOf}.");
    }
}

internal sealed class InterviewUpdateRequestValidator : InterviewFieldsValidator<InterviewUpdateRequest>
{
    public InterviewUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
