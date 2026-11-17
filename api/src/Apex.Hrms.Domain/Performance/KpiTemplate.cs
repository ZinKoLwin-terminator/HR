using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Performance;

/// <summary>
/// The editable fields of a KPI template. A null department means the KPI is available to every department. Status
/// changes through Activate and Deactivate.
/// </summary>
public sealed record KpiTemplateDetails(
    string Code,
    string Name,
    string? Description,
    int CategoryId,
    int? DepartmentId,
    KpiMeasurementType MeasurementType,
    string? Unit,
    KpiDirection Direction,
    KpiScoringMethod ScoringMethod,
    decimal DefaultTarget,
    decimal? RangeMin,
    decimal? RangeMax,
    decimal? MaxRating,
    decimal DefaultWeight,
    decimal? MaxAchievementPct,
    KpiFrequency Frequency)
{
    /// <summary>The scoring rule the details describe.</summary>
    public KpiRule Rule => new(MeasurementType, Direction, ScoringMethod, DefaultTarget, RangeMin, RangeMax, MaxRating, MaxAchievementPct);

    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public KpiTemplateDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
        Unit = TextNormalization.CleanOptional(Unit),
    };
}

/// <summary>
/// A reusable KPI definition (kpi.Template). Assigning it to a scorecard copies its target, weight and scoring rules into
/// the scorecard item, so editing a template never changes a scorecard that already uses it.
/// </summary>
public sealed class KpiTemplate : CodedRecord
{
    /// <summary>The most one KPI can weigh, and what all KPIs of a scorecard add up to.</summary>
    public const decimal MaxWeight = 100m;

    private KpiTemplate()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>When null the KPI is available to every department.</summary>
    public int? DepartmentId { get; private set; }

    public KpiMeasurementType MeasurementType { get; private set; }

    public string? Unit { get; private set; }

    public KpiDirection Direction { get; private set; }

    public KpiScoringMethod ScoringMethod { get; private set; }

    public decimal DefaultTarget { get; private set; }

    public decimal? RangeMin { get; private set; }

    public decimal? RangeMax { get; private set; }

    public decimal? MaxRating { get; private set; }

    public decimal DefaultWeight { get; private set; }

    public decimal? MaxAchievementPct { get; private set; }

    public KpiFrequency Frequency { get; private set; }

    /// <summary>The scoring rule of this template.</summary>
    public KpiRule Rule => new(MeasurementType, Direction, ScoringMethod, DefaultTarget, RangeMin, RangeMax, MaxRating, MaxAchievementPct);

    public static Result<KpiTemplate> Create(KpiTemplateDetails details, RecordStatus status)
    {
        var template = new KpiTemplate();
        var result = template.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<KpiTemplate>(result.Error);
        }

        template.SetInitialStatus(status);
        return Result.Success(template);
    }

    /// <summary>
    /// Checks the rules that need no other record, without changing anything. The scoring rule is checked by
    /// <see cref="KpiRule.Validate"/>; its field names are mapped to the request's (the target is <c>defaultTarget</c>).
    /// </summary>
    public static Result Validate(KpiTemplateDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations
            .Require("name", "KPI name", normalized.Name)
            .AddIf(
                normalized.DefaultWeight is <= 0m or > MaxWeight,
                "defaultWeight",
                $"The default weight must be greater than 0 and at most {MaxWeight:0}.");

        var rule = normalized.Rule.Validate();
        if (rule.IsFailure)
        {
            foreach (var (field, messages) in rule.Error.FieldErrors ?? new Dictionary<string, string[]>())
            {
                foreach (var message in messages)
                {
                    violations.Add(field == "target" ? "defaultTarget" : field, message);
                }
            }
        }

        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(KpiTemplateDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        Name = normalized.Name;
        Description = normalized.Description;
        CategoryId = normalized.CategoryId;
        DepartmentId = normalized.DepartmentId;
        MeasurementType = normalized.MeasurementType;
        Unit = normalized.Unit;
        Direction = normalized.Direction;
        ScoringMethod = normalized.ScoringMethod;
        DefaultTarget = normalized.DefaultTarget;
        RangeMin = normalized.RangeMin;
        RangeMax = normalized.RangeMax;
        MaxRating = normalized.MaxRating;
        DefaultWeight = normalized.DefaultWeight;
        MaxAchievementPct = normalized.MaxAchievementPct;
        Frequency = normalized.Frequency;
        return Result.Success();
    }
}
