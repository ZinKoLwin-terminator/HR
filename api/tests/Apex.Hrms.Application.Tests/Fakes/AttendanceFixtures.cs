using Apex.Hrms.Application.Attendance.Adjustments;
using Apex.Hrms.Application.Attendance.Approvals;
using Apex.Hrms.Application.Attendance.Holidays;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// Fictional attendance requests and the handler calls the attendance tests share. The fake clock says today is Monday
/// 2026-11-09 at 09:00 (set <c>host.Clock.TimeNow</c> to punch at another time).
/// </summary>
internal static class AttendanceFixtures
{
    public static readonly DateOnly Monday = new(2026, 11, 9);

    public static readonly DateOnly Saturday = new(2026, 11, 14);

    public static TimeOnly At(int hour, int minute) => new(hour, minute);

    public static ShiftCreateRequest NewShift(
        string code = "GEN",
        string name = "General Shift",
        TimeOnly? start = null,
        TimeOnly? end = null,
        int breakMinutes = 60,
        int graceMinutes = 15,
        int[]? workDays = null,
        bool night = false,
        string status = "Active") => new(
        code,
        name,
        start ?? At(9, 0),
        end ?? At(18, 0),
        breakMinutes,
        graceMinutes,
        workDays ?? [1, 2, 3, 4, 5],
        night,
        "#3f6fd8",
        status);

    public static ShiftUpdateRequest ShiftUpdateOf(ShiftDto current, string? name = null) => new(
        current.Code,
        name ?? current.Name,
        current.StartTime,
        current.EndTime,
        current.BreakMinutes,
        current.GraceMinutes,
        current.WorkDays,
        current.IsNightShift,
        current.Color,
        Requests.Version(current.RowVersion));

    public static Task<Result<ShiftDto>> CreateShift(this TestHost host, ShiftCreateRequest? request = null) =>
        host.Get<ICreateHandler<ShiftCreateRequest, ShiftDto>>().HandleAsync(request ?? NewShift(), default);

    public static async Task<ShiftDto> CreateShiftAsync(this TestHost host, ShiftCreateRequest? request = null) =>
        ResultAssert.Succeeded(await host.CreateShift(request));

    public static Task<Result<HolidayDto>> CreateHoliday(this TestHost host, DateOnly date, string name = "Water Festival", string type = "Public") =>
        host.Get<ICreateHandler<HolidayCreateRequest, HolidayDto>>().HandleAsync(new HolidayCreateRequest(date, name, type), default);

    public static async Task<HolidayDto> CreateHolidayAsync(this TestHost host, DateOnly date, string name = "Water Festival") =>
        ResultAssert.Succeeded(await host.CreateHoliday(date, name));

    /// <summary>An employee of the fictional organization working the given shift (the first shift when null).</summary>
    public static async Task<EmployeeDto> CreateWorkerAsync(this TestHost host, OrgSeed org, string email, int? shiftId = null) =>
        await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: email) with { ShiftId = shiftId });

    public static Task<Result<AttendanceRecordDto>> Punch(this TestHost host, int employeeId, string kind) =>
        host.Get<IPunchHandler>().HandleAsync(new PunchRequest(employeeId, kind), default);

    public static async Task<AttendanceRecordDto> PunchAsync(this TestHost host, int employeeId, string kind, int hour, int minute)
    {
        host.Clock.TimeNow = At(hour, minute);
        return ResultAssert.Succeeded(await host.Punch(employeeId, kind));
    }

    public static Task<Result<AdjustmentDto>> CreateAdjustment(
        this TestHost host,
        int employeeId,
        DateOnly? date = null,
        TimeOnly? checkIn = null,
        TimeOnly? checkOut = null,
        string? status = null,
        string reason = "Forgot to punch") =>
        host.Get<ICreateHandler<AdjustmentCreateRequest, AdjustmentDto>>()
            .HandleAsync(new AdjustmentCreateRequest(employeeId, date ?? Monday, checkIn, checkOut, status, reason), default);

    public static async Task<AdjustmentDto> CreateAdjustmentAsync(
        this TestHost host,
        int employeeId,
        TimeOnly? checkIn = null,
        TimeOnly? checkOut = null,
        string? status = null) =>
        ResultAssert.Succeeded(await host.CreateAdjustment(employeeId, Monday, checkIn, checkOut, status));

    public static Task<Result<AdjustmentDto>> ApproveAdjustment(this TestHost host, AdjustmentDto adjustment, string? remarks = null) =>
        host.Command<ApproveAdjustmentCommand, AdjustmentDto>(new ApproveAdjustmentCommand(adjustment.Id, Requests.Version(adjustment.RowVersion), remarks));

    public static Task<Result<AdjustmentDto>> RejectAdjustment(this TestHost host, AdjustmentDto adjustment, string? remarks = "Not supported") =>
        host.Command<RejectAdjustmentCommand, AdjustmentDto>(new RejectAdjustmentCommand(adjustment.Id, Requests.Version(adjustment.RowVersion), remarks));

    public static Task<Result<OvertimeDto>> CreateOvertime(
        this TestHost host,
        int employeeId,
        TimeOnly start,
        TimeOnly end,
        DateOnly? date = null) =>
        host.Get<ICreateHandler<OvertimeCreateRequest, OvertimeDto>>()
            .HandleAsync(new OvertimeCreateRequest(employeeId, date ?? Monday, start, end, "Month end close"), default);

    public static async Task<OvertimeDto> CreateOvertimeAsync(this TestHost host, int employeeId, TimeOnly start, TimeOnly end) =>
        ResultAssert.Succeeded(await host.CreateOvertime(employeeId, start, end));

    public static Task<Result<OvertimeDto>> ApproveOvertime(this TestHost host, OvertimeDto overtime, string? remarks = null) =>
        host.Command<ApproveOvertimeCommand, OvertimeDto>(new ApproveOvertimeCommand(overtime.Id, Requests.Version(overtime.RowVersion), remarks));

    public static Task<Result<OvertimeDto>> RejectOvertime(this TestHost host, OvertimeDto overtime, string? remarks = "Not authorised") =>
        host.Command<RejectOvertimeCommand, OvertimeDto>(new RejectOvertimeCommand(overtime.Id, Requests.Version(overtime.RowVersion), remarks));
}
