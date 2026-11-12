using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Assignments;

/// <summary>
/// Mirrors the SalaryAssignment interface in payroll.models.ts. <c>basicSalary</c> is a monthly DEMONSTRATION figure and
/// personal data: it is only returned to callers with payroll access and never written to logs or the activity feed.
/// </summary>
public sealed record SalaryAssignmentDto(
    int Id,
    int EmployeeId,
    int StructureId,
    decimal BasicSalary,
    DateOnly EffectiveFrom,
    string Remarks,
    byte[] RowVersion) : IIdentified;

internal interface ISalaryAssignmentFields
{
    int EmployeeId { get; }

    int StructureId { get; }

    decimal BasicSalary { get; }

    DateOnly EffectiveFrom { get; }

    string? Remarks { get; }
}

/// <summary>
/// POST body. The basic salary must be inside the band of the employee's job grade, the date cannot be before the joining
/// date, and the employee cannot have two assignments starting on the same date.
/// </summary>
public sealed record SalaryAssignmentCreateRequest(
    int EmployeeId,
    int StructureId,
    decimal BasicSalary,
    DateOnly EffectiveFrom,
    string? Remarks) : ISalaryAssignmentFields
{
    public SalaryAssignmentDetails ToDetails() => SalaryAssignmentDetailsFactory.From(this);
}

/// <summary>PUT body. The employee cannot change (send the same <c>employeeId</c>). Requires the row version.</summary>
public sealed record SalaryAssignmentUpdateRequest(
    int EmployeeId,
    int StructureId,
    decimal BasicSalary,
    DateOnly EffectiveFrom,
    string? Remarks,
    string? RowVersion) : ISalaryAssignmentFields, IVersionedRequest
{
    public SalaryAssignmentDetails ToDetails() => SalaryAssignmentDetailsFactory.From(this);
}

internal static class SalaryAssignmentDetailsFactory
{
    public static SalaryAssignmentDetails From(ISalaryAssignmentFields fields) => new SalaryAssignmentDetails(
        fields.EmployeeId,
        fields.StructureId,
        fields.BasicSalary,
        fields.EffectiveFrom,
        fields.Remarks).Normalized();
}

/// <summary>Filters of the list. An employee's salary history is the list filtered by <c>employeeId</c>, sorted by <c>effectiveFrom</c>.</summary>
public sealed record SalaryAssignmentFilter(int? EmployeeId = null, int? StructureId = null);
