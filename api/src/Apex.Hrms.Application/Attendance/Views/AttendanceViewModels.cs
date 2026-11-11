namespace Apex.Hrms.Application.Attendance.Views;

// Mirror the dashboard and schedule interfaces in attendance.models.ts.

/// <param name="Scheduled">Employees expected to work that day.</param>
/// <param name="AttendanceRate">Percent of the scheduled employees who checked in (present + late + half day), one decimal.</param>
public sealed record AttendanceDaySummaryDto(
    DateOnly Date,
    int Scheduled,
    int Present,
    int Late,
    int HalfDay,
    int Absent,
    int OnLeave,
    decimal AttendanceRate);

public sealed record DepartmentAttendanceDto(
    int DepartmentId,
    string Department,
    int Present,
    int Late,
    int HalfDay,
    int Absent,
    int OnLeave,
    int Total);

/// <param name="Detail">For example "Checked in 09:42 · 27 min late".</param>
public sealed record AttendanceExceptionDto(int EmployeeId, string EmployeeCode, string Name, string Department, string Detail);

public sealed record AttendanceTrendDayDto(DateOnly Date, int Present, int Late, int Absent, int OnLeave);

public sealed record AttendanceOverviewDto(
    AttendanceDaySummaryDto Summary,
    IReadOnlyList<DepartmentAttendanceDto> ByDepartment,
    IReadOnlyList<AttendanceExceptionDto> Late,
    IReadOnlyList<AttendanceExceptionDto> Absent,
    IReadOnlyList<AttendanceTrendDayDto> Trend);

/// <param name="Kind">"Shift", "Off", "Holiday" or "Leave".</param>
/// <param name="Label">Short text, e.g. "09:00–18:00", "Off", the holiday name, "On leave".</param>
public sealed record ScheduleCellDto(DateOnly Date, string Kind, string Label);

public sealed record ScheduleRowDto(
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string Department,
    string ShiftName,
    string ShiftColor,
    IReadOnlyList<ScheduleCellDto> Cells);

public sealed record WorkScheduleDto(DateOnly WeekStart, IReadOnlyList<DateOnly> Days, IReadOnlyList<ScheduleRowDto> Rows);

/// <summary>The query of <c>GET /api/attendance/schedule</c>: the seven days from <c>WeekStart</c>.</summary>
public sealed record WorkScheduleRequest(DateOnly WeekStart, int? DepartmentId = null, int? BranchId = null);
