namespace Apex.Hrms.Domain.Attendance;

/// <summary>The parts of a shift the attendance calculation needs.</summary>
public sealed record ShiftRules(TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes, int GraceMinutes, bool IsNightShift);

/// <summary>What the rules make of one day's times.</summary>
public sealed record DerivedAttendance(
    AttendanceStatus Status,
    int WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes);

/// <summary>
/// Sample attendance rules (a port of attendance-calc.ts), shared by punches and approved adjustments. They are
/// demonstrations, not a statutory or contractual calculation:
/// <list type="bullet">
/// <item>Late: checked in after start + grace; the late minutes are counted from the shift start.</item>
/// <item>Worked = (check-out - check-in) - break; a day is a half day when that is under 240 minutes (4 hours).</item>
/// <item>Early leave: minutes before the scheduled end. Overtime: the minutes after the end, only when they exceed 30.</item>
/// <item>Night shifts roll the end (and a check-out earlier than the check-in) into the next day.</item>
/// </list>
/// Times count whole minutes; seconds are ignored.
/// </summary>
public static class AttendanceRules
{
    /// <summary>Minutes after the scheduled end before overtime starts counting.</summary>
    public const int OvertimeThresholdMinutes = 30;

    /// <summary>Worked time below this (with a check-out) is treated as a half day.</summary>
    public const int HalfDayMaxMinutes = 240;

    public const int OvertimeMinMinutes = 15;

    public const int OvertimeMaxMinutes = 12 * 60;

    private const int MinutesPerDay = 1440;

    public static int ToMinutes(TimeOnly time) => (time.Hour * 60) + time.Minute;

    /// <summary>Drops the seconds.</summary>
    public static TimeOnly Truncate(TimeOnly time) => new(time.Hour, time.Minute);

    /// <summary>Minutes between two times; rolls over midnight when the end is before the start.</summary>
    public static int MinutesBetween(TimeOnly start, TimeOnly end)
    {
        var difference = ToMinutes(end) - ToMinutes(start);
        return difference >= 0 ? difference : difference + MinutesPerDay;
    }

    public static DerivedAttendance Derive(
        ShiftRules shift,
        TimeOnly? checkIn,
        TimeOnly? checkOut,
        AttendanceStatus? forcedStatus = null)
    {
        if (checkIn is not { } arrival)
        {
            return new DerivedAttendance(forcedStatus ?? AttendanceStatus.Absent, 0, 0, 0, 0);
        }

        var start = ToMinutes(shift.StartTime);
        var end = ToMinutes(shift.EndTime);
        if (shift.IsNightShift && end <= start)
        {
            end += MinutesPerDay;
        }

        var inAt = ToMinutes(arrival);
        var late = inAt > start + shift.GraceMinutes ? inAt - start : 0;

        if (checkOut is not { } departure)
        {
            return new DerivedAttendance(
                forcedStatus ?? (late > 0 ? AttendanceStatus.Late : AttendanceStatus.Present),
                0,
                late,
                0,
                0);
        }

        var outAt = ToMinutes(departure);
        if (outAt < inAt)
        {
            outAt += MinutesPerDay; // crossed midnight
        }

        var gross = outAt - inAt;
        var halfDay = forcedStatus == AttendanceStatus.HalfDay
            || (forcedStatus is null && gross - shift.BreakMinutes < HalfDayMaxMinutes);
        var worked = Math.Max(0, gross - (halfDay ? 0 : shift.BreakMinutes));
        var after = outAt - end;
        var status = forcedStatus
            ?? (halfDay ? AttendanceStatus.HalfDay : late > 0 ? AttendanceStatus.Late : AttendanceStatus.Present);
        return new DerivedAttendance(
            status,
            worked,
            late,
            halfDay ? 0 : Math.Max(0, end - outAt),
            after > OvertimeThresholdMinutes ? after : 0);
    }
}
