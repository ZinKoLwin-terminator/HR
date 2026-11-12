using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Structures;

/// <summary>Mirrors the SalaryStructure interface in payroll.models.ts. <c>componentIds</c> is in display order.</summary>
public sealed record SalaryStructureDto(
    int Id,
    string Code,
    string Name,
    string Description,
    IReadOnlyList<int> ComponentIds,
    string Status,
    byte[] RowVersion) : IIdentified;

internal interface ISalaryStructureFields
{
    string Code { get; }

    string Name { get; }

    string? Description { get; }

    IReadOnlyList<int>? ComponentIds { get; }
}

/// <summary>POST body. A structure needs at least one existing component, and at least one of them an earning.</summary>
public sealed record SalaryStructureCreateRequest(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<int>? ComponentIds,
    string Status) : ISalaryStructureFields, ICreateRequest<SalaryStructureDetails>
{
    public SalaryStructureDetails ToDetails() => SalaryStructureDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record SalaryStructureUpdateRequest(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<int>? ComponentIds,
    string? RowVersion) : ISalaryStructureFields, IDetailsRequest<SalaryStructureDetails>, IVersionedRequest
{
    public SalaryStructureDetails ToDetails() => SalaryStructureDetailsFactory.From(this);
}

internal static class SalaryStructureDetailsFactory
{
    public static SalaryStructureDetails From(ISalaryStructureFields fields) => new SalaryStructureDetails(
        fields.Code,
        fields.Name,
        fields.Description,
        fields.ComponentIds ?? []).Normalized();
}

public sealed record SalaryStructureFilter(string? Status = null);
