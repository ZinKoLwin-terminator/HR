using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>How a KPI result is measured. Descriptive, except Rating and Boolean, which also decide how it is scored. The texts match the database CHECK constraints exactly.</summary>
public enum KpiMeasurementType
{
    Number,
    Percentage,
    Currency,
    Hours,
    Rating,
    Boolean,
}

/// <summary>Which way a better result points. The texts match the database CHECK constraints exactly.</summary>
public enum KpiDirection
{
    HigherIsBetter,
    LowerIsBetter,
    ExactTarget,
    RangeBased,
}

/// <summary>How a result becomes an achievement percentage. The texts match the database CHECK constraints exactly.</summary>
public enum KpiScoringMethod
{
    DirectPercentage,
    CappedPercentage,
    RatingBased,
    Boolean,
    RangeBased,
}

/// <summary>How often a KPI is measured; also the type of an evaluation period. The texts match the database CHECK constraints exactly.</summary>
public enum KpiFrequency
{
    Monthly,
    Quarterly,
    HalfYearly,
    Yearly,
    Custom,
}

/// <summary>The life of an evaluation period. The texts match the database CHECK constraints exactly.</summary>
public enum EvaluationPeriodStatus
{
    Draft,
    Active,
    Completed,
    Closed,
}

/// <summary>The life of an employee's scorecard, from planning to the finalized score. The texts match the database CHECK constraints exactly.</summary>
public enum ScorecardStatus
{
    Planning,
    Draft,
    Submitted,
    UnderReview,
    Returned,
    Resubmitted,
    Approved,
    Rejected,
    Finalized,
}

/// <summary>Who takes part in the review of a scorecard. The texts match the database CHECK constraints exactly.</summary>
public enum ReviewStep
{
    Employee,
    Manager,
    HR,
}

/// <summary>A step recorded in the review history of a scorecard. The texts match the database CHECK constraints exactly.</summary>
public enum ReviewActionType
{
    FinalizePlan,
    Submit,
    Resubmit,
    StartReview,
    Approve,
    Reject,
    Return,
    Finalize,
}

public static class KpiMeasurementTypeText
{
    public static readonly TextMap<KpiMeasurementType> Map = new(new Dictionary<KpiMeasurementType, string>
    {
        [KpiMeasurementType.Number] = "Number",
        [KpiMeasurementType.Percentage] = "Percentage",
        [KpiMeasurementType.Currency] = "Currency",
        [KpiMeasurementType.Hours] = "Hours",
        [KpiMeasurementType.Rating] = "Rating",
        [KpiMeasurementType.Boolean] = "Boolean",
    });

    public static string ToText(KpiMeasurementType value) => Map.ToText(value);

    public static KpiMeasurementType Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out KpiMeasurementType value) => Map.TryParse(text, out value);
}

public static class KpiDirectionText
{
    public static readonly TextMap<KpiDirection> Map = new(new Dictionary<KpiDirection, string>
    {
        [KpiDirection.HigherIsBetter] = "Higher Is Better",
        [KpiDirection.LowerIsBetter] = "Lower Is Better",
        [KpiDirection.ExactTarget] = "Exact Target",
        [KpiDirection.RangeBased] = "Range Based",
    });

    public static string ToText(KpiDirection value) => Map.ToText(value);

    public static KpiDirection Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out KpiDirection value) => Map.TryParse(text, out value);
}

public static class KpiScoringMethodText
{
    public static readonly TextMap<KpiScoringMethod> Map = new(new Dictionary<KpiScoringMethod, string>
    {
        [KpiScoringMethod.DirectPercentage] = "Direct Percentage",
        [KpiScoringMethod.CappedPercentage] = "Capped Percentage",
        [KpiScoringMethod.RatingBased] = "Rating Based",
        [KpiScoringMethod.Boolean] = "Boolean",
        [KpiScoringMethod.RangeBased] = "Range Based",
    });

    public static string ToText(KpiScoringMethod value) => Map.ToText(value);

    public static KpiScoringMethod Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out KpiScoringMethod value) => Map.TryParse(text, out value);
}

public static class KpiFrequencyText
{
    public static readonly TextMap<KpiFrequency> Map = new(new Dictionary<KpiFrequency, string>
    {
        [KpiFrequency.Monthly] = "Monthly",
        [KpiFrequency.Quarterly] = "Quarterly",
        [KpiFrequency.HalfYearly] = "Half-Yearly",
        [KpiFrequency.Yearly] = "Yearly",
        [KpiFrequency.Custom] = "Custom",
    });

    public static string ToText(KpiFrequency value) => Map.ToText(value);

    public static KpiFrequency Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out KpiFrequency value) => Map.TryParse(text, out value);
}

public static class EvaluationPeriodStatusText
{
    public static readonly TextMap<EvaluationPeriodStatus> Map = new(new Dictionary<EvaluationPeriodStatus, string>
    {
        [EvaluationPeriodStatus.Draft] = "Draft",
        [EvaluationPeriodStatus.Active] = "Active",
        [EvaluationPeriodStatus.Completed] = "Completed",
        [EvaluationPeriodStatus.Closed] = "Closed",
    });

    public static string ToText(EvaluationPeriodStatus value) => Map.ToText(value);

    public static EvaluationPeriodStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out EvaluationPeriodStatus value) => Map.TryParse(text, out value);
}

public static class ScorecardStatusText
{
    public static readonly TextMap<ScorecardStatus> Map = new(new Dictionary<ScorecardStatus, string>
    {
        [ScorecardStatus.Planning] = "Planning",
        [ScorecardStatus.Draft] = "Draft",
        [ScorecardStatus.Submitted] = "Submitted",
        [ScorecardStatus.UnderReview] = "Under Review",
        [ScorecardStatus.Returned] = "Returned",
        [ScorecardStatus.Resubmitted] = "Resubmitted",
        [ScorecardStatus.Approved] = "Approved",
        [ScorecardStatus.Rejected] = "Rejected",
        [ScorecardStatus.Finalized] = "Finalized",
    });

    public static string ToText(ScorecardStatus value) => Map.ToText(value);

    public static ScorecardStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out ScorecardStatus value) => Map.TryParse(text, out value);
}

public static class ReviewStepText
{
    public static readonly TextMap<ReviewStep> Map = new(new Dictionary<ReviewStep, string>
    {
        [ReviewStep.Employee] = "Employee",
        [ReviewStep.Manager] = "Manager",
        [ReviewStep.HR] = "HR",
    });

    public static string ToText(ReviewStep value) => Map.ToText(value);

    public static ReviewStep Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out ReviewStep value) => Map.TryParse(text, out value);
}

public static class ReviewActionTypeText
{
    public static readonly TextMap<ReviewActionType> Map = new(new Dictionary<ReviewActionType, string>
    {
        [ReviewActionType.FinalizePlan] = "Finalize Plan",
        [ReviewActionType.Submit] = "Submit",
        [ReviewActionType.Resubmit] = "Resubmit",
        [ReviewActionType.StartReview] = "Start Review",
        [ReviewActionType.Approve] = "Approve",
        [ReviewActionType.Reject] = "Reject",
        [ReviewActionType.Return] = "Return",
        [ReviewActionType.Finalize] = "Finalize",
    });

    public static string ToText(ReviewActionType value) => Map.ToText(value);

    public static ReviewActionType Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out ReviewActionType value) => Map.TryParse(text, out value);
}
