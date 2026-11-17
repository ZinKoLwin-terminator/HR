using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>
/// The fields of an evaluation period. The period type is a <see cref="KpiFrequency"/>. Status is not part of the
/// details: it only moves through Activate, Complete and Close.
/// </summary>
public sealed record EvaluationPeriodDetails(
    int CompanyId,
    string Name,
    KpiFrequency PeriodType,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly? SubmissionDueDate,
    bool RequiresHrReview)
{
    /// <summary>Trims the name. Idempotent.</summary>
    public EvaluationPeriodDetails Normalized() => this with { Name = TextNormalization.Clean(Name) };
}

/// <summary>
/// One evaluation window of a company (kpi.EvaluationPeriod): Draft, Active, Completed, Closed. Scorecards are planned in
/// Draft and Active, submitted and reviewed while Active, and Closed is final. A Draft period can be changed in every
/// field and deleted; an Active one only changes its name and submission due date; Completed and Closed ones are
/// read-only. Overlap with other periods of the company is checked by the application layer (it needs other records).
/// </summary>
public sealed class EvaluationPeriod : AuditedEntity
{
    private EvaluationPeriod()
    {
    }

    public int CompanyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public KpiFrequency PeriodType { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public DateOnly? SubmissionDueDate { get; private set; }

    /// <summary>Adds the optional HR final review after the manager's approval.</summary>
    public bool RequiresHrReview { get; private set; }

    public EvaluationPeriodStatus Status { get; private set; }

    /// <summary>True when the dates of the two periods share at least one day (both ends are inclusive).</summary>
    public static bool Overlaps(DateOnly startA, DateOnly endA, DateOnly startB, DateOnly endB) =>
        startA <= endB && endA >= startB;

    /// <summary>A new period in Draft.</summary>
    public static Result<EvaluationPeriod> Create(EvaluationPeriodDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return Result.Failure<EvaluationPeriod>(valid.Error);
        }

        var period = new EvaluationPeriod { Status = EvaluationPeriodStatus.Draft };
        period.Assign(details.Normalized());
        return Result.Success(period);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(EvaluationPeriodDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .Require("name", "Period name", normalized.Name)
            .AddIf(
                normalized.EndDate <= normalized.StartDate,
                "endDate",
                "Must be after the start date.",
                "The end date must be after the start date.")
            .AddIf(
                normalized.SubmissionDueDate is { } due && due < normalized.StartDate,
                "submissionDueDate",
                "Must be on or after the start date.",
                "The submission due date cannot be before the period starts.")
            .ToResult();
    }

    /// <summary>
    /// Checks whether the details may be applied in the current status, without changing anything: every field in Draft,
    /// only the name and the submission due date while Active, nothing once Completed or Closed. A refusal is a Conflict.
    /// </summary>
    public Result CheckEdit(EvaluationPeriodDetails details)
    {
        var normalized = details.Normalized();
        return Status switch
        {
            EvaluationPeriodStatus.Draft => Result.Success(),
            EvaluationPeriodStatus.Active when ChangesLockedFields(normalized) => Result.Failure(AppError.Conflict(
                $"Period \"{Name}\" is Active: only its name and submission due date can be changed.")),
            EvaluationPeriodStatus.Active => Result.Success(),
            _ => Result.Failure(AppError.Conflict(
                $"Period \"{Name}\" is {EvaluationPeriodStatusText.ToText(Status)} and can no longer be changed.")),
        };
    }

    /// <summary>Applies the details, or changes nothing when a rule or the status forbids it. Status is left untouched.</summary>
    public Result Update(EvaluationPeriodDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var allowed = CheckEdit(details);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Assign(details.Normalized());
        return Result.Success();
    }

    /// <summary>Draft to Active.</summary>
    public Result Activate() => MoveTo(EvaluationPeriodStatus.Draft, EvaluationPeriodStatus.Active, "activated");

    /// <summary>Active to Completed.</summary>
    public Result Complete() => MoveTo(EvaluationPeriodStatus.Active, EvaluationPeriodStatus.Completed, "completed");

    /// <summary>Completed to Closed (final).</summary>
    public Result Close() => MoveTo(EvaluationPeriodStatus.Completed, EvaluationPeriodStatus.Closed, "closed");

    /// <summary>Results are entered and scorecards submitted only while the period is Active (409 otherwise).</summary>
    public Result EnsureAcceptsResults() => Status == EvaluationPeriodStatus.Active
        ? Result.Success()
        : Result.Failure(AppError.Conflict(
            $"Period \"{Name}\" is {EvaluationPeriodStatusText.ToText(Status)}; results can only be entered and submitted while the period is Active."));

    /// <summary>Evidence files are added or removed only while the period is Active (409 otherwise).</summary>
    public Result EnsureAcceptsEvidence() => Status == EvaluationPeriodStatus.Active
        ? Result.Success()
        : Result.Failure(AppError.Conflict(
            $"Period \"{Name}\" is {EvaluationPeriodStatusText.ToText(Status)}; evidence can only be added or removed while the period is Active."));

    /// <summary>A submitted scorecard is reviewed while the period is Active or Completed; Draft and Closed periods refuse it (409).</summary>
    public Result EnsureAllowsReview() => Status is EvaluationPeriodStatus.Active or EvaluationPeriodStatus.Completed
        ? Result.Success()
        : Result.Failure(AppError.Conflict(
            $"Period \"{Name}\" is {EvaluationPeriodStatusText.ToText(Status)}; a scorecard can only be reviewed while its period is Active or Completed."));

    /// <summary>Only a Draft period can be deleted. The caller also checks that no scorecard uses it.</summary>
    public Result EnsureCanDelete() => Status == EvaluationPeriodStatus.Draft
        ? Result.Success()
        : Result.Failure(AppError.Conflict(
            $"Period \"{Name}\" is {EvaluationPeriodStatusText.ToText(Status)}; only a Draft period can be deleted."));

    private bool ChangesLockedFields(EvaluationPeriodDetails details) =>
        details.CompanyId != CompanyId
        || details.PeriodType != PeriodType
        || details.StartDate != StartDate
        || details.EndDate != EndDate
        || details.RequiresHrReview != RequiresHrReview;

    private Result MoveTo(EvaluationPeriodStatus from, EvaluationPeriodStatus to, string verb)
    {
        if (Status != from)
        {
            return Result.Failure(AppError.Conflict(
                $"Period \"{Name}\" is {EvaluationPeriodStatusText.ToText(Status)}; only a {EvaluationPeriodStatusText.ToText(from)} period can be {verb}."));
        }

        Status = to;
        return Result.Success();
    }

    private void Assign(EvaluationPeriodDetails details)
    {
        CompanyId = details.CompanyId;
        Name = details.Name;
        PeriodType = details.PeriodType;
        StartDate = details.StartDate;
        EndDate = details.EndDate;
        SubmissionDueDate = details.SubmissionDueDate;
        RequiresHrReview = details.RequiresHrReview;
    }
}
