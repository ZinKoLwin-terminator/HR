using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Components;

/// <summary>
/// Mirrors the SalaryComponent interface in payroll.models.ts. DEMONSTRATION rules only: <c>value</c> is an amount for
/// <c>Fixed</c> and a percentage (above 0, at most 100) for the other methods; none of it is a statutory tax or social
/// security rate.
/// </summary>
public sealed record SalaryComponentDto(
    int Id,
    string Code,
    string Name,
    string Type,
    string Calculation,
    decimal Value,
    string Description,
    string Status,
    byte[] RowVersion) : IIdentified;

internal interface ISalaryComponentFields
{
    string Code { get; }

    string Name { get; }

    string Type { get; }

    string Calculation { get; }

    decimal Value { get; }

    string? Description { get; }
}

/// <summary>POST body. <c>type</c> is Earning or Deduction; <c>calculation</c> is Fixed, PercentOfBasic or PercentOfGross; status is the status the component starts with.</summary>
public sealed record SalaryComponentCreateRequest(
    string Code,
    string Name,
    string Type,
    string Calculation,
    decimal Value,
    string? Description,
    string Status) : ISalaryComponentFields, ICreateRequest<SalaryComponentDetails>
{
    public SalaryComponentDetails ToDetails() => SalaryComponentDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record SalaryComponentUpdateRequest(
    string Code,
    string Name,
    string Type,
    string Calculation,
    decimal Value,
    string? Description,
    string? RowVersion) : ISalaryComponentFields, IDetailsRequest<SalaryComponentDetails>, IVersionedRequest
{
    public SalaryComponentDetails ToDetails() => SalaryComponentDetailsFactory.From(this);
}

internal static class SalaryComponentDetailsFactory
{
    public static SalaryComponentDetails From(ISalaryComponentFields fields) => new SalaryComponentDetails(
        fields.Code,
        fields.Name,
        ComponentTypeText.Parse(fields.Type),
        CalculationMethodText.Parse(fields.Calculation),
        fields.Value,
        fields.Description).Normalized();
}

public sealed record SalaryComponentFilter(string? Status = null, string? Type = null, string? Calculation = null);
