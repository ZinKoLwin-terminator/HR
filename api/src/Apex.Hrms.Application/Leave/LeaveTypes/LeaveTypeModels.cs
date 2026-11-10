using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.LeaveTypes;

/// <summary>Mirrors the LeaveType interface in leave.models.ts. Entitlements are sample figures, not statutory ones.</summary>
public sealed record LeaveTypeDto(
    int Id,
    string Code,
    string Name,
    string Description,
    decimal DefaultDaysPerYear,
    bool IsPaid,
    bool RequiresDocument,
    short MaxConsecutiveDays,
    bool CarryForward,
    decimal MaxCarryForwardDays,
    string Color,
    string? EligibleGender,
    string Status,
    byte[] RowVersion) : IIdentified;

/// <summary>The fields a leave type has on create and update.</summary>
internal interface ILeaveTypeFields
{
    string Code { get; }

    string Name { get; }

    string? Description { get; }

    decimal DefaultDaysPerYear { get; }

    bool IsPaid { get; }

    bool RequiresDocument { get; }

    short MaxConsecutiveDays { get; }

    bool CarryForward { get; }

    decimal MaxCarryForwardDays { get; }

    string Color { get; }

    /// <summary>Male, Female or Other; null or blank for every employee.</summary>
    string? EligibleGender { get; }
}

/// <summary>POST body. Status is the status the new leave type starts with.</summary>
public sealed record LeaveTypeCreateRequest(
    string Code,
    string Name,
    string? Description,
    decimal DefaultDaysPerYear,
    bool IsPaid,
    bool RequiresDocument,
    short MaxConsecutiveDays,
    bool CarryForward,
    decimal MaxCarryForwardDays,
    string Color,
    string? EligibleGender,
    string Status) : ILeaveTypeFields, ICreateRequest<LeaveTypeDetails>
{
    public LeaveTypeDetails ToDetails() => LeaveTypeDetailsFactory.From(this);

    public RecordStatus ParseStatus() => Enum.Parse<RecordStatus>(Status);
}

/// <summary>PUT body. It has no status (use activate and deactivate) and requires the row version.</summary>
public sealed record LeaveTypeUpdateRequest(
    string Code,
    string Name,
    string? Description,
    decimal DefaultDaysPerYear,
    bool IsPaid,
    bool RequiresDocument,
    short MaxConsecutiveDays,
    bool CarryForward,
    decimal MaxCarryForwardDays,
    string Color,
    string? EligibleGender,
    string? RowVersion) : ILeaveTypeFields, IDetailsRequest<LeaveTypeDetails>, IVersionedRequest
{
    public LeaveTypeDetails ToDetails() => LeaveTypeDetailsFactory.From(this);
}

internal static class LeaveTypeDetailsFactory
{
    public static LeaveTypeDetails From(ILeaveTypeFields fields) => new LeaveTypeDetails(
        fields.Code,
        fields.Name,
        fields.Description,
        fields.DefaultDaysPerYear,
        fields.IsPaid,
        fields.RequiresDocument,
        fields.MaxConsecutiveDays,
        fields.CarryForward,
        fields.MaxCarryForwardDays,
        fields.Color,
        string.IsNullOrWhiteSpace(fields.EligibleGender) ? null : GenderText.Parse(fields.EligibleGender.Trim())).Normalized();
}

public sealed record LeaveTypeFilter(string? Status = null);
