using System.Globalization;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>
/// One employee's day (att.AttendanceRecord). Records are created and changed only by punches and by approved
/// adjustments: there is no way to write the times, minutes or status directly. The minutes and the status are
/// derived by <see cref="AttendanceRules"/> and stored.
/// </summary>
public sealed class AttendanceRecord : AuditedEntity
{
    public const int RemarksMaxLength = 250;

    private AttendanceRecord()
    {
    }

    public int EmployeeId { get; private set; }

    public DateOnly Date { get; private set; }

    public int? ShiftId { get; private set; }

    public TimeOnly? CheckIn { get; private set; }

    public TimeOnly? CheckOut { get; private set; }

    public int WorkedMinutes { get; private set; }

    public int LateMinutes { get; private set; }

    public int EarlyLeaveMinutes { get; private set; }

    public int OvertimeMinutes { get; private set; }

    public AttendanceStatus Status { get; private set; }

    public AttendanceSource Source { get; private set; }

    public string? Remarks { get; private set; }

    /// <summary>
    /// Starts the record of a day before its first punch. <paramref name="initialStatus"/> is Absent on a working day,
    /// Weekend or Holiday on a day off.
    /// </summary>
    public static AttendanceRecord Open(int employeeId, DateOnly date, int? shiftId, AttendanceStatus initialStatus) =>
        new()
        {
            EmployeeId = employeeId,
            Date = date,
            ShiftId = shiftId,
            Status = initialStatus,
            Source = AttendanceSource.Web,
        };

    /// <summary>
    /// Records a check-in or a check-out at <paramref name="time"/> (the server's time of day, to the minute).
    /// <paramref name="calendarDay"/> is Weekend or Holiday when the calendar says the day is not a working day for
    /// the employee, otherwise null; working on such a day makes the record Present with no late or early minutes.
    /// </summary>
    public Result Punch(PunchKind kind, TimeOnly time, ShiftRules shift, AttendanceStatus? calendarDay)
    {
        if (Status == AttendanceStatus.OnLeave)
        {
            return Result.Failure(AppError.Conflict("You are on approved leave on this day."));
        }

        var minute = AttendanceRules.Truncate(time);
        var dayOff = Status is AttendanceStatus.Weekend or AttendanceStatus.Holiday ? Status : calendarDay;
        if (kind == PunchKind.CheckIn)
        {
            if (CheckIn is { } already)
            {
                return Result.Failure(AppError.Conflict($"Already checked in at {Format(already)}."));
            }

            var derived = AttendanceRules.Derive(shift, minute, null);
            Apply(derived);
            CheckIn = minute;
            Source = AttendanceSource.Web;
            Remarks = dayOff is { } off
                ? $"Worked on a {AttendanceStatusText.ToText(off).ToLowerInvariant()}"
                : derived.Status == AttendanceStatus.Late ? "Late arrival" : null;
        }
        else
        {
            if (CheckIn is not { } arrival)
            {
                return Result.Failure(AppError.Conflict("Check in before checking out."));
            }

            if (CheckOut is { } left)
            {
                return Result.Failure(AppError.Conflict($"Already checked out at {Format(left)}."));
            }

            if (!shift.IsNightShift && minute <= arrival)
            {
                return Result.Failure(AppError.Validation("The check-out time must be after the check-in time."));
            }

            Apply(AttendanceRules.Derive(shift, arrival, minute));
            CheckOut = minute;
        }

        if (dayOff is not null)
        {
            Status = AttendanceStatus.Present;
            LateMinutes = 0;
            EarlyLeaveMinutes = 0;
        }

        return Result.Success();
    }

    /// <summary>
    /// Applies an approved adjustment: a requested time replaces the stored one, then the minutes and the status are
    /// derived again (a requested status wins over the derived one). The record becomes Manual.
    /// </summary>
    public Result ApplyAdjustment(
        TimeOnly? requestedCheckIn,
        TimeOnly? requestedCheckOut,
        AttendanceStatus? requestedStatus,
        ShiftRules shift,
        string label)
    {
        var checkIn = requestedCheckIn ?? CheckIn;
        var checkOut = requestedCheckOut ?? CheckOut;
        Apply(AttendanceRules.Derive(shift, checkIn, checkOut, requestedStatus));
        CheckIn = checkIn;
        CheckOut = checkOut;
        Source = AttendanceSource.Manual;
        var remarks = $"Adjusted ({label})";
        Remarks = remarks.Length > RemarksMaxLength ? remarks[..RemarksMaxLength] : remarks;
        return Result.Success();
    }

    public static string Format(TimeOnly time) => time.ToString("HH':'mm", CultureInfo.InvariantCulture);

    private void Apply(DerivedAttendance derived)
    {
        Status = derived.Status;
        WorkedMinutes = derived.WorkedMinutes;
        LateMinutes = derived.LateMinutes;
        EarlyLeaveMinutes = derived.EarlyLeaveMinutes;
        OvertimeMinutes = derived.OvertimeMinutes;
    }
}
