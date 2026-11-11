using Apex.Hrms.Domain.Attendance;

namespace Apex.Hrms.Domain.Tests.Attendance;

/// <summary>The derivation rules (a port of attendance-calc.ts), with the thresholds on both sides of the boundary.</summary>
public sealed class AttendanceRulesTests
{
    // General day shift 09:00-18:00, 60 minute break, 15 minutes grace.
    private static readonly ShiftRules Day = new(new TimeOnly(9, 0), new TimeOnly(18, 0), 60, 15, false);

    // Night shift 22:00-06:00, 60 minute break, 10 minutes grace.
    private static readonly ShiftRules Night = new(new TimeOnly(22, 0), new TimeOnly(6, 0), 60, 10, true);

    private static TimeOnly At(int hour, int minute) => new(hour, minute);

    [Fact]
    public void Arriving_within_the_grace_period_is_not_late()
    {
        var derived = AttendanceRules.Derive(Day, At(9, 15), null);

        Assert.Equal(AttendanceStatus.Present, derived.Status);
        Assert.Equal(0, derived.LateMinutes);
    }

    [Fact]
    public void Arriving_after_the_grace_period_is_late_and_counted_from_the_shift_start()
    {
        var derived = AttendanceRules.Derive(Day, At(9, 16), null);

        Assert.Equal(AttendanceStatus.Late, derived.Status);
        Assert.Equal(16, derived.LateMinutes);
    }

    [Fact]
    public void Without_a_grace_period_one_minute_after_the_start_is_late()
    {
        var strict = Day with { GraceMinutes = 0 };

        Assert.Equal(0, AttendanceRules.Derive(strict, At(9, 0), null).LateMinutes);
        Assert.Equal(1, AttendanceRules.Derive(strict, At(9, 1), null).LateMinutes);
    }

    [Fact]
    public void Overtime_counts_only_after_thirty_minutes_past_the_shift_end_and_then_in_full()
    {
        Assert.Equal(0, AttendanceRules.Derive(Day, At(9, 0), At(18, 30)).OvertimeMinutes);
        Assert.Equal(31, AttendanceRules.Derive(Day, At(9, 0), At(18, 31)).OvertimeMinutes);
        Assert.Equal(120, AttendanceRules.Derive(Day, At(9, 0), At(20, 0)).OvertimeMinutes);
    }

    [Fact]
    public void A_day_is_a_half_day_when_the_worked_minutes_are_under_240()
    {
        // 09:00 to 14:00 is 300 minutes gross, 240 after the 60 minute break: a full day. One minute less is 239: a half day.
        var half = AttendanceRules.Derive(Day, At(9, 0), At(13, 59));
        var full = AttendanceRules.Derive(Day, At(9, 0), At(14, 0));

        Assert.Equal(AttendanceStatus.HalfDay, half.Status);
        Assert.Equal(AttendanceStatus.Present, full.Status);
        Assert.Equal(240, full.WorkedMinutes);
    }

    [Fact]
    public void A_half_day_keeps_the_whole_time_as_worked_and_has_no_early_leave()
    {
        var derived = AttendanceRules.Derive(Day, At(9, 0), At(12, 0));

        Assert.Equal(AttendanceStatus.HalfDay, derived.Status);
        Assert.Equal(180, derived.WorkedMinutes);
        Assert.Equal(0, derived.EarlyLeaveMinutes);
    }

    [Fact]
    public void Leaving_before_the_end_is_early_leave_on_a_full_day()
    {
        var derived = AttendanceRules.Derive(Day, At(9, 0), At(17, 0));

        Assert.Equal(AttendanceStatus.Present, derived.Status);
        Assert.Equal(60, derived.EarlyLeaveMinutes);
        Assert.Equal(420, derived.WorkedMinutes);
    }

    [Fact]
    public void A_late_arrival_that_still_works_a_full_day_stays_late()
    {
        var derived = AttendanceRules.Derive(Day, At(9, 30), At(18, 0));

        Assert.Equal(AttendanceStatus.Late, derived.Status);
        Assert.Equal(30, derived.LateMinutes);
    }

    [Fact]
    public void A_night_shift_rolls_the_end_and_the_check_out_into_the_next_day()
    {
        var derived = AttendanceRules.Derive(Night, At(22, 0), At(6, 45));

        Assert.Equal(AttendanceStatus.Present, derived.Status);
        Assert.Equal((8 * 60) + 45 - 60, derived.WorkedMinutes);
        Assert.Equal(45, derived.OvertimeMinutes);
        Assert.Equal(0, derived.EarlyLeaveMinutes);
    }

    [Fact]
    public void A_night_shift_check_in_after_the_start_is_late_and_early_leave_is_measured_across_midnight()
    {
        var derived = AttendanceRules.Derive(Night, At(22, 30), At(5, 0));

        Assert.Equal(30, derived.LateMinutes);
        Assert.Equal(60, derived.EarlyLeaveMinutes);
    }

    [Fact]
    public void No_check_in_means_absent_unless_a_status_is_forced()
    {
        Assert.Equal(AttendanceStatus.Absent, AttendanceRules.Derive(Day, null, null).Status);
        var forced = AttendanceRules.Derive(Day, null, null, AttendanceStatus.OnLeave);
        Assert.Equal(AttendanceStatus.OnLeave, forced.Status);
        Assert.Equal(0, forced.WorkedMinutes);
    }

    [Fact]
    public void A_forced_status_wins_but_the_minutes_are_still_derived()
    {
        var derived = AttendanceRules.Derive(Day, At(9, 0), At(18, 0), AttendanceStatus.Late);

        Assert.Equal(AttendanceStatus.Late, derived.Status);
        Assert.Equal(480, derived.WorkedMinutes);
    }

    [Fact]
    public void A_forced_half_day_does_not_deduct_the_break()
    {
        var derived = AttendanceRules.Derive(Day, At(9, 0), At(18, 0), AttendanceStatus.HalfDay);

        Assert.Equal(AttendanceStatus.HalfDay, derived.Status);
        Assert.Equal(540, derived.WorkedMinutes);
        Assert.Equal(0, derived.EarlyLeaveMinutes);
    }

    [Fact]
    public void Minutes_between_rolls_over_midnight()
    {
        Assert.Equal(120, AttendanceRules.MinutesBetween(At(18, 0), At(20, 0)));
        Assert.Equal(360, AttendanceRules.MinutesBetween(At(22, 0), At(4, 0)));
        Assert.Equal(0, AttendanceRules.MinutesBetween(At(8, 0), At(8, 0)));
    }

    [Fact]
    public void Seconds_are_ignored()
    {
        Assert.Equal(new TimeOnly(9, 15), AttendanceRules.Truncate(new TimeOnly(9, 15, 59)));
    }
}
