using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;

namespace Apex.Hrms.Application.Attendance.Records;

/// <summary>Mirrors the AttendanceRecord interface in attendance.models.ts. Times are "HH:mm" or null. Returned by a punch.</summary>
public sealed record AttendanceRecordDto(
    int Id,
    int EmployeeId,
    DateOnly Date,
    int? ShiftId,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    string Status,
    string Source,
    string Remarks,
    byte[] RowVersion) : IIdentified;

/// <summary>Mirrors AttendanceListItem in attendance.models.ts: the record with employee, organization and shift names.</summary>
public sealed record AttendanceListItemDto(
    int Id,
    int EmployeeId,
    DateOnly Date,
    int? ShiftId,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    string Status,
    string Source,
    string Remarks,
    byte[] RowVersion,
    string EmployeeCode,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    int BranchId,
    string BranchName,
    string ShiftName) : IIdentified;

/// <summary>
/// Filters of the list: equality fields, an inclusive date range, and "only records with late minutes / early-leave
/// minutes / overtime" (<c>lateOnly</c>, <c>earlyOnly</c>, <c>overtimeOnly</c>). <c>search</c> matches the employee name or code.
/// </summary>
public sealed record AttendanceFilter(
    int? EmployeeId = null,
    int? DepartmentId = null,
    int? BranchId = null,
    string? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    bool? LateOnly = null,
    bool? EarlyOnly = null,
    bool? OvertimeOnly = null);

/// <summary>
/// POST /api/attendance/punches body. There is no date and no time: the server stamps the punch with today's date and
/// the time of day now in the business time zone. <c>kind</c> is <c>CheckIn</c> or <c>CheckOut</c>.
/// </summary>
public sealed record PunchRequest(int EmployeeId, string? Kind);

internal sealed class AttendanceRecordMapping :
    IEntityDescriptor<AttendanceRecord>,
    IDtoMapper<AttendanceRecord, AttendanceRecordDto>
{
    public string Label => "Attendance record";

    public string Describe(AttendanceRecord entity) => $"{entity.EmployeeId} {entity.Date:yyyy-MM-dd}";

    public AttendanceRecordDto ToDto(AttendanceRecord entity) => new(
        entity.Id,
        entity.EmployeeId,
        entity.Date,
        entity.ShiftId,
        entity.CheckIn,
        entity.CheckOut,
        entity.WorkedMinutes,
        entity.LateMinutes,
        entity.EarlyLeaveMinutes,
        entity.OvertimeMinutes,
        AttendanceStatusText.ToText(entity.Status),
        AttendanceSourceText.ToText(entity.Source),
        entity.Remarks ?? string.Empty,
        entity.RowVersion);
}
