using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.LeavePolicies;

/// <summary>Mirrors the LeavePolicy interface in leave.models.ts. A null grade or employment type applies to everyone.</summary>
public sealed record LeavePolicyDto(
    int Id,
    string Name,
    int LeaveTypeId,
    int? JobGradeId,
    string? EmploymentType,
    decimal DaysPerYear,
    string Accrual,
    short MinServiceMonths,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a leave policy has on create and update.</summary>
internal interface ILeavePolicyFields
{
    string Name { get; }

    int LeaveTypeId { get; }

    int? JobGradeId { get; }

    /// <summary>Full-time, Part-time, Contract or Intern; null or blank for every employment type.</summary>
    string? EmploymentType { get; }

    decimal DaysPerYear { get; }

    /// <summary>Upfront or Monthly.</summary>
    string Accrual { get; }

    short MinServiceMonths { get; }
}

/// <summary>POST body. Status is the status the new policy starts with.</summary>
public sealed record LeavePolicyCreateRequest(
    string Name,
    int LeaveTypeId,
    int? JobGradeId,
    string? EmploymentType,
    decimal DaysPerYear,
    string Accrual,
    short MinServiceMonths,
    string Status) : ILeavePolicyFields, ICreateRequest<LeavePolicyDetails>
{
    public LeavePolicyDetails ToDetails() => LeavePolicyDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record LeavePolicyUpdateRequest(
    string Name,
    int LeaveTypeId,
    int? JobGradeId,
    string? EmploymentType,
    decimal DaysPerYear,
    string Accrual,
    short MinServiceMonths,
    string? RowVersion) : ILeavePolicyFields, IDetailsRequest<LeavePolicyDetails>, IVersionedRequest
{
    public LeavePolicyDetails ToDetails() => LeavePolicyDetailsFactory.From(this);
}

internal static class LeavePolicyDetailsFactory
{
    public static LeavePolicyDetails From(ILeavePolicyFields fields) => new LeavePolicyDetails(
        fields.Name,
        fields.LeaveTypeId,
        fields.JobGradeId,
        string.IsNullOrWhiteSpace(fields.EmploymentType) ? null : EmploymentTypeText.Parse(fields.EmploymentType.Trim()),
        fields.DaysPerYear,
        LeaveAccrualText.Parse(fields.Accrual.Trim()),
        fields.MinServiceMonths).Normalized();
}

public sealed record LeavePolicyFilter(
    string? Status = null,
    int? LeaveTypeId = null,
    int? JobGradeId = null,
    string? EmploymentType = null,
    string? Accrual = null);
