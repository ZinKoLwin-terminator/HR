using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Attendance.Records;

public interface IPunchHandler
{
    Task<Result<AttendanceRecordDto>> HandleAsync(PunchRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Records a check-in or check-out. The server decides the date and the time: today and now in the business time zone,
/// never a value from the client. The first punch of a day creates the record of that day; the day's record is then
/// updated by the next punch. Rules, in order:
/// <list type="bullet">
/// <item>The caller may punch only for the employee linked to the account (the built-in administrator may punch for anyone).</item>
/// <item>The employee exists and has not left.</item>
/// <item>A day of approved (whole-day) leave cannot be punched; a holiday or a day off in the shift is punched as work on a day off.</item>
/// <item>Check-in once a day; check-out only after a check-in, once, and after the check-in time unless the shift is a night shift.</item>
/// </list>
/// </summary>
internal sealed class PunchHandler(
    IRepository<Employee> employees,
    IRepository<Holiday> holidays,
    IRepository<LeaveApplication> leave,
    IRepository<AttendanceRecord> records,
    AttendanceLookup lookup,
    IClock clock,
    ICurrentUser user,
    IUnitOfWork unitOfWork,
    AttendanceRecordMapping mapper) : IPunchHandler
{
    public async Task<Result<AttendanceRecordDto>> HandleAsync(PunchRequest request, CancellationToken cancellationToken)
    {
        if (user is not { IsAuthenticated: true, UserId: not null })
        {
            return Result.Failure<AttendanceRecordDto>(AppError.Unauthorized("Sign in to record attendance."));
        }

        var invalid = Validate(request, out var kind);
        if (invalid is not null)
        {
            return Result.Failure<AttendanceRecordDto>(invalid);
        }

        if (user.EmployeeId is { } own && own != request.EmployeeId)
        {
            return Result.Failure<AttendanceRecordDto>(AppError.Forbidden("You can only record your own attendance."));
        }

        var employee = await employees.FindAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Result.Failure<AttendanceRecordDto>(AppError.NotFound($"Employee #{request.EmployeeId} was not found."));
        }

        if (employee.HasLeft)
        {
            return Result.Failure<AttendanceRecordDto>(AppError.Conflict("This employee has left and cannot record attendance."));
        }

        var shift = await lookup.ShiftForAsync(employee.ShiftId, cancellationToken);
        if (shift is null)
        {
            return Result.Failure<AttendanceRecordDto>(AppError.Conflict("No shift exists yet. Create a shift before recording attendance."));
        }

        var today = clock.Today;
        var now = clock.TimeNow;
        var record = await lookup.RecordOfAsync(employee.Id, today, cancellationToken);
        if (record is null && kind == PunchKind.CheckOut)
        {
            // A night shift that started yesterday ends after midnight: that check-out closes yesterday's open record.
            var previous = await lookup.RecordOfAsync(employee.Id, today.AddDays(-1), cancellationToken);
            if (previous is { CheckIn: not null, CheckOut: null }
                && await lookup.ShiftForAsync(previous.ShiftId, cancellationToken) is { IsNightShift: true })
            {
                record = previous;
            }
        }

        // An existing record keeps the shift it was opened with, so a later change of the employee's shift does not
        // change how the day is judged.
        var punchShift = record?.ShiftId is { } recordShiftId && recordShiftId != shift.Id
            ? await lookup.ShiftForAsync(recordShiftId, cancellationToken) ?? shift
            : shift;
        var calendarDay = await CalendarDayAsync(punchShift, record?.Date ?? today, cancellationToken);
        var isNew = record is null;
        if (record is null)
        {
            if (await IsOnLeaveAsync(employee.Id, today, cancellationToken))
            {
                return Result.Failure<AttendanceRecordDto>(AppError.Conflict("You are on approved leave on this day."));
            }

            record = AttendanceRecord.Open(employee.Id, today, shift.Id, calendarDay ?? AttendanceStatus.Absent);
        }

        var punched = record.Punch(kind, now, punchShift.Rules, calendarDay);
        if (punched.IsFailure)
        {
            return Result.Failure<AttendanceRecordDto>(punched.Error);
        }

        if (isNew)
        {
            records.Add(record);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<AttendanceRecordDto>(saved.Error)
            : Result.Success(mapper.ToDto(record));
    }

    private static AppError? Validate(PunchRequest request, out PunchKind kind)
    {
        kind = default;
        if (request.EmployeeId <= 0)
        {
            return AppError.Validation("Choose an employee.", "employeeId", "Choose an employee.");
        }

        return PunchKindText.TryParse(request.Kind, out kind)
            ? null
            : AppError.Validation(
                $"Kind must be {PunchKindText.Map.OneOf}.",
                "kind",
                $"Kind must be {PunchKindText.Map.OneOf}.");
    }

    /// <summary>Holiday when att.Holiday has the date, Weekend when the shift does not work that weekday, otherwise null.</summary>
    private async Task<AttendanceStatus?> CalendarDayAsync(Shift shift, DateOnly date, CancellationToken cancellationToken)
    {
        if (await holidays.AnyAsync(holiday => holiday.Date == date, cancellationToken))
        {
            return AttendanceStatus.Holiday;
        }

        return shift.WorksOn(date) ? null : AttendanceStatus.Weekend;
    }

    private Task<bool> IsOnLeaveAsync(int employeeId, DateOnly date, CancellationToken cancellationToken) =>
        leave.AnyAsync(
            application => application.EmployeeId == employeeId
                && application.Status == LeaveStatus.Approved
                && !application.HalfDay
                && application.StartDate <= date
                && application.EndDate >= date,
            cancellationToken);
}
