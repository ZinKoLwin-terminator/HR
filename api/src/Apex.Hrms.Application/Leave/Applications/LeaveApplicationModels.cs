using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.Applications;

/// <summary>
/// Mirrors the LeaveApplication interface in leave.models.ts. <see cref="ApproverId"/> is the employee who approved or
/// rejected (null for undecided leave and for decisions of the built-in administrator); <see cref="ActionedByName"/> is
/// the name of whoever approved, rejected or cancelled, as it was at that time.
/// </summary>
public sealed record LeaveApplicationDto(
    int Id,
    string ApplicationNo,
    int EmployeeId,
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Days,
    bool HalfDay,
    string Reason,
    string? DocumentName,
    string Status,
    DateOnly AppliedOn,
    int? ApproverId,
    string? ActionedByName,
    DateOnly? ActionedOn,
    string? ApproverRemarks,
    byte[] RowVersion) : IIdentified;

/// <summary>
/// POST body. The application starts as a Draft with a server-made number; the days are worked out by the server
/// (weekends and holidays are left out; a half day is 0.5). <c>documentName</c> is the name of the supporting document
/// (no file is stored yet).
/// </summary>
public sealed record LeaveApplicationCreateRequest(
    int EmployeeId,
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool HalfDay,
    string Reason,
    string? DocumentName) : ILeaveApplicationFields
{
    public LeaveApplicationDetails ToDetails() => LeaveApplicationDetailsFactory.From(this);
}

/// <summary>PUT body: changes a draft. Requires the row version.</summary>
public sealed record LeaveApplicationUpdateRequest(
    int EmployeeId,
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    bool HalfDay,
    string Reason,
    string? DocumentName,
    string? RowVersion) : ILeaveApplicationFields, IVersionedRequest
{
    public LeaveApplicationDetails ToDetails() => LeaveApplicationDetailsFactory.From(this);
}

internal interface ILeaveApplicationFields
{
    int EmployeeId { get; }

    int LeaveTypeId { get; }

    DateOnly StartDate { get; }

    DateOnly EndDate { get; }

    bool HalfDay { get; }

    string Reason { get; }

    string? DocumentName { get; }
}

internal static class LeaveApplicationDetailsFactory
{
    public static LeaveApplicationDetails From(ILeaveApplicationFields fields) => new LeaveApplicationDetails(
        fields.EmployeeId,
        fields.LeaveTypeId,
        fields.StartDate,
        fields.EndDate,
        fields.HalfDay,
        fields.Reason,
        fields.DocumentName).Normalized();
}

/// <summary>Filters of the list: equality fields plus <c>dateFrom</c> / <c>dateTo</c>, matched as an overlap with the leave dates.</summary>
public sealed record LeaveApplicationFilter(
    int? EmployeeId = null,
    int? LeaveTypeId = null,
    string? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

// ---- Commands (status never changes through PUT) ----

/// <summary>Draft to Submitted.</summary>
public sealed record SubmitLeaveCommand(int Id, string? RowVersion) : IEntityCommand<SubmitLeaveCommand>
{
    public static SubmitLeaveCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>Submitted to Approved. The remarks are optional.</summary>
public sealed record ApproveLeaveCommand(int Id, string? RowVersion, string? Remarks) : IEntityCommand;

/// <summary>Submitted to Rejected. The remarks are the reason and are required.</summary>
public sealed record RejectLeaveCommand(int Id, string? RowVersion, string? Remarks) : IEntityCommand;

/// <summary>Submitted, or approved leave that has not started, to Cancelled. The remarks are optional.</summary>
public sealed record CancelLeaveCommand(int Id, string? RowVersion, string? Remarks) : IEntityCommand;
