using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Templates;

/// <summary>
/// A reusable KPI definition (kpi.Template). Measurement type, direction, scoring method and frequency are the texts of
/// the database CHECK constraints (for example "Higher Is Better", "Half-Yearly"). A null department means every department.
/// </summary>
public sealed record KpiTemplateDto(
    int Id,
    string Code,
    string Name,
    string Description,
    int CategoryId,
    int? DepartmentId,
    string MeasurementType,
    string? Unit,
    string Direction,
    string ScoringMethod,
    decimal DefaultTarget,
    decimal? RangeMin,
    decimal? RangeMax,
    decimal? MaxRating,
    decimal DefaultWeight,
    decimal? MaxAchievementPct,
    string Frequency,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a KPI template has on create and update.</summary>
internal interface IKpiTemplateFields
{
    string Code { get; }

    string Name { get; }

    string? Description { get; }

    int CategoryId { get; }

    int? DepartmentId { get; }

    /// <summary>Number, Percentage, Currency, Hours, Rating or Boolean.</summary>
    string MeasurementType { get; }

    string? Unit { get; }

    /// <summary>Higher Is Better, Lower Is Better, Exact Target or Range Based.</summary>
    string Direction { get; }

    /// <summary>Direct Percentage, Capped Percentage, Rating Based, Boolean or Range Based.</summary>
    string ScoringMethod { get; }

    decimal DefaultTarget { get; }

    decimal? RangeMin { get; }

    decimal? RangeMax { get; }

    decimal? MaxRating { get; }

    decimal DefaultWeight { get; }

    decimal? MaxAchievementPct { get; }

    /// <summary>Monthly, Quarterly, Half-Yearly, Yearly or Custom.</summary>
    string Frequency { get; }
}

/// <summary>POST body. Status is the status the new template starts with.</summary>
public sealed record KpiTemplateCreateRequest(
    string Code,
    string Name,
    string? Description,
    int CategoryId,
    int? DepartmentId,
    string MeasurementType,
    string? Unit,
    string Direction,
    string ScoringMethod,
    decimal DefaultTarget,
    decimal? RangeMin,
    decimal? RangeMax,
    decimal? MaxRating,
    decimal DefaultWeight,
    decimal? MaxAchievementPct,
    string Frequency,
    string Status) : IKpiTemplateFields, ICreateRequest<KpiTemplateDetails>
{
    public KpiTemplateDetails ToDetails() => KpiTemplateDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record KpiTemplateUpdateRequest(
    string Code,
    string Name,
    string? Description,
    int CategoryId,
    int? DepartmentId,
    string MeasurementType,
    string? Unit,
    string Direction,
    string ScoringMethod,
    decimal DefaultTarget,
    decimal? RangeMin,
    decimal? RangeMax,
    decimal? MaxRating,
    decimal DefaultWeight,
    decimal? MaxAchievementPct,
    string Frequency,
    string? RowVersion) : IKpiTemplateFields, IDetailsRequest<KpiTemplateDetails>, IVersionedRequest
{
    public KpiTemplateDetails ToDetails() => KpiTemplateDetailsFactory.From(this);
}

internal static class KpiTemplateDetailsFactory
{
    public static KpiTemplateDetails From(IKpiTemplateFields fields) => new KpiTemplateDetails(
        fields.Code,
        fields.Name,
        fields.Description,
        fields.CategoryId,
        fields.DepartmentId,
        KpiMeasurementTypeText.Parse(fields.MeasurementType.Trim()),
        fields.Unit,
        KpiDirectionText.Parse(fields.Direction.Trim()),
        KpiScoringMethodText.Parse(fields.ScoringMethod.Trim()),
        fields.DefaultTarget,
        fields.RangeMin,
        fields.RangeMax,
        fields.MaxRating,
        fields.DefaultWeight,
        fields.MaxAchievementPct,
        KpiFrequencyText.Parse(fields.Frequency.Trim())).Normalized();
}

public sealed record KpiTemplateFilter(
    string? Status = null,
    int? CategoryId = null,
    int? DepartmentId = null,
    string? ScoringMethod = null);
