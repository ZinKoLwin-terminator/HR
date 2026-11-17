using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Categories;

/// <summary>A grouping of KPI templates (kpi.Category), for example Productivity or Quality.</summary>
public sealed record KpiCategoryDto(
    int Id,
    string Code,
    string Name,
    string Description,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a KPI category has on create and update.</summary>
internal interface IKpiCategoryFields
{
    string Code { get; }

    string Name { get; }

    string? Description { get; }
}

/// <summary>POST body. Status is the status the new category starts with.</summary>
public sealed record KpiCategoryCreateRequest(
    string Code,
    string Name,
    string? Description,
    string Status) : IKpiCategoryFields, ICreateRequest<KpiCategoryDetails>
{
    public KpiCategoryDetails ToDetails() => KpiCategoryDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record KpiCategoryUpdateRequest(
    string Code,
    string Name,
    string? Description,
    string? RowVersion) : IKpiCategoryFields, IDetailsRequest<KpiCategoryDetails>, IVersionedRequest
{
    public KpiCategoryDetails ToDetails() => KpiCategoryDetailsFactory.From(this);
}

internal static class KpiCategoryDetailsFactory
{
    public static KpiCategoryDetails From(IKpiCategoryFields fields) =>
        new KpiCategoryDetails(fields.Code, fields.Name, fields.Description).Normalized();
}

public sealed record KpiCategoryFilter(string? Status = null);
