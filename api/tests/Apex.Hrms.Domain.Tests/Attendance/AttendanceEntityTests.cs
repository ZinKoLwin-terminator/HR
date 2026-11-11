using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Tests.Attendance;

public sealed class AttendanceEntityTests
{
    private static readonly ShiftRules Day = new(new TimeOnly(9, 0), new TimeOnly(18, 0), 60, 15, false);
    private static readonly ShiftRules Night = new(new TimeOnly(22, 0), new TimeOnly(6, 0), 60, 10, true);
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static TimeOnly At(int hour, int minute) => new(hour, minute);

    private static AttendanceRecord NewRecord(AttendanceStatus status = AttendanceStatus.Absent) =>
        AttendanceRecord.Open(1, Today, 1, status);

    private static void AssertFailure(Result result, ErrorType type, string? field = null)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(type, result.Error.Type);
        if (field is not null)
        {
            Assert.Contains(field, result.Error.FieldErrors!.Keys);
        }
    }

    // ---- Punches ----

    [Fact]
    public void A_check_in_stamps_the_time_and_derives_late_minutes()
    {
        var record = NewRecord();

        Assert.True(record.Punch(PunchKind.CheckIn, At(9, 40), Day, null).IsSuccess);

        Assert.Equal(At(9, 40), record.CheckIn);
        Assert.Equal(AttendanceStatus.Late, record.Status);
        Assert.Equal(40, record.LateMinutes);
        Assert.Equal("Late arrival", record.Remarks);
        Assert.Equal(AttendanceSource.Web, record.Source);
    }

    [Fact]
    public void A_check_in_keeps_only_the_minutes_of_the_server_time()
    {
        var record = NewRecord();

        record.Punch(PunchKind.CheckIn, new TimeOnly(8, 58, 59), Day, null);

        Assert.Equal(At(8, 58), record.CheckIn);
    }

    [Fact]
    public void A_second_check_in_is_a_conflict()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(9, 0), Day, null);

        var again = record.Punch(PunchKind.CheckIn, At(9, 5), Day, null);

        AssertFailure(again, ErrorType.Conflict);
        Assert.Equal("Already checked in at 09:00.", again.Error.Message);
    }

    [Fact]
    public void A_check_out_needs_a_check_in_first()
    {
        var result = NewRecord().Punch(PunchKind.CheckOut, At(18, 0), Day, null);

        AssertFailure(result, ErrorType.Conflict);
        Assert.Equal("Check in before checking out.", result.Error.Message);
    }

    [Fact]
    public void A_check_out_derives_the_worked_overtime_and_status()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(9, 0), Day, null);

        Assert.True(record.Punch(PunchKind.CheckOut, At(19, 0), Day, null).IsSuccess);

        Assert.Equal(At(19, 0), record.CheckOut);
        Assert.Equal(540, record.WorkedMinutes);
        Assert.Equal(60, record.OvertimeMinutes);
        Assert.Equal(AttendanceStatus.Present, record.Status);
    }

    [Fact]
    public void A_second_check_out_is_a_conflict()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(9, 0), Day, null);
        record.Punch(PunchKind.CheckOut, At(18, 0), Day, null);

        var again = record.Punch(PunchKind.CheckOut, At(19, 0), Day, null);

        AssertFailure(again, ErrorType.Conflict);
        Assert.Equal("Already checked out at 18:00.", again.Error.Message);
    }

    [Fact]
    public void A_day_shift_check_out_must_be_after_the_check_in()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(9, 0), Day, null);

        AssertFailure(record.Punch(PunchKind.CheckOut, At(9, 0), Day, null), ErrorType.Validation);
        Assert.Null(record.CheckOut);
    }

    [Fact]
    public void A_night_shift_check_out_may_be_earlier_than_the_check_in()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(22, 0), Night, null);

        Assert.True(record.Punch(PunchKind.CheckOut, At(6, 0), Night, null).IsSuccess);

        Assert.Equal(At(6, 0), record.CheckOut);
        Assert.Equal(420, record.WorkedMinutes);
    }

    [Fact]
    public void A_day_of_approved_leave_cannot_be_punched()
    {
        var result = NewRecord(AttendanceStatus.OnLeave).Punch(PunchKind.CheckIn, At(9, 0), Day, null);

        AssertFailure(result, ErrorType.Conflict);
    }

    [Fact]
    public void Working_on_a_day_off_makes_the_day_present_without_late_or_early_minutes()
    {
        var record = NewRecord(AttendanceStatus.Weekend);

        record.Punch(PunchKind.CheckIn, At(11, 0), Day, AttendanceStatus.Weekend);
        record.Punch(PunchKind.CheckOut, At(15, 0), Day, AttendanceStatus.Weekend);

        Assert.Equal(AttendanceStatus.Present, record.Status);
        Assert.Equal(0, record.LateMinutes);
        Assert.Equal(0, record.EarlyLeaveMinutes);
        Assert.Equal("Worked on a weekend", record.Remarks);
        Assert.Equal(240, record.WorkedMinutes); // a half day by the rules: the break is not deducted
    }

    [Fact]
    public void Working_on_a_holiday_says_so_in_the_remarks()
    {
        var record = NewRecord(AttendanceStatus.Holiday);

        record.Punch(PunchKind.CheckIn, At(9, 0), Day, AttendanceStatus.Holiday);

        Assert.Equal("Worked on a holiday", record.Remarks);
        Assert.Equal(AttendanceStatus.Present, record.Status);
    }

    // ---- Applying an adjustment ----

    [Fact]
    public void An_adjustment_replaces_the_requested_time_and_derives_again()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(10, 0), Day, null);
        record.Punch(PunchKind.CheckOut, At(18, 0), Day, null);
        Assert.Equal(AttendanceStatus.Late, record.Status);

        record.ApplyAdjustment(At(9, 0), null, null, Day, "ADJ-0007");

        Assert.Equal(At(9, 0), record.CheckIn);
        Assert.Equal(At(18, 0), record.CheckOut);
        Assert.Equal(AttendanceStatus.Present, record.Status);
        Assert.Equal(0, record.LateMinutes);
        Assert.Equal(AttendanceSource.Manual, record.Source);
        Assert.Equal("Adjusted (ADJ-0007)", record.Remarks);
    }

    [Fact]
    public void An_adjustment_with_a_status_forces_it()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(9, 0), Day, null);

        record.ApplyAdjustment(null, null, AttendanceStatus.HalfDay, Day, "ADJ-0008");

        Assert.Equal(AttendanceStatus.HalfDay, record.Status);
    }

    // ---- Shifts ----

    private static ShiftDetails ShiftOf(
        string code = "GEN",
        TimeOnly? start = null,
        TimeOnly? end = null,
        bool night = false,
        int[]? days = null,
        string color = "#3f6fd8") =>
        new(code, "General", start ?? At(9, 0), end ?? At(18, 0), 60, 15, days ?? [1, 2, 3, 4, 5], night, color);

    [Fact]
    public void A_shift_needs_at_least_one_working_day()
    {
        var result = Shift.Create(ShiftOf(days: []), RecordStatus.Active);

        AssertFailure(result, ErrorType.Validation, "workDays");
        Assert.Equal("Choose at least one working day.", result.Error.Message);
    }

    [Fact]
    public void A_working_day_must_be_a_weekday_number()
    {
        AssertFailure(Shift.Create(ShiftOf(days: [7]), RecordStatus.Active), ErrorType.Validation, "workDays");
        AssertFailure(Shift.Create(ShiftOf(days: [-1]), RecordStatus.Active), ErrorType.Validation, "workDays");
    }

    [Fact]
    public void A_day_shift_must_end_after_it_starts_but_a_night_shift_may_not()
    {
        var day = Shift.Create(ShiftOf(start: At(18, 0), end: At(6, 0)), RecordStatus.Active);
        var equal = Shift.Create(ShiftOf(start: At(9, 0), end: At(9, 0)), RecordStatus.Active);
        var night = Shift.Create(ShiftOf(start: At(22, 0), end: At(6, 0), night: true), RecordStatus.Active);

        AssertFailure(day, ErrorType.Validation, "endTime");
        AssertFailure(equal, ErrorType.Validation, "endTime");
        Assert.True(night.IsSuccess);
    }

    [Fact]
    public void A_shift_code_and_colour_follow_the_database_rules()
    {
        AssertFailure(Shift.Create(ShiftOf(code: "gen"), RecordStatus.Active), ErrorType.Validation, "code");
        AssertFailure(Shift.Create(ShiftOf(color: "blue"), RecordStatus.Active), ErrorType.Validation, "color");
    }

    [Fact]
    public void A_shift_reports_its_sorted_working_days_and_whether_a_date_is_one_of_them()
    {
        var shift = Shift.Create(ShiftOf(days: [5, 1, 3, 1]), RecordStatus.Active).Value;

        Assert.Equal([1, 3, 5], shift.WorkDayNumbers);
        Assert.True(shift.WorksOn(new DateOnly(2026, 11, 9))); // Monday
        Assert.False(shift.WorksOn(new DateOnly(2026, 11, 10))); // Tuesday
    }

    [Fact]
    public void Updating_a_shift_adds_and_removes_working_days()
    {
        var shift = Shift.Create(ShiftOf(days: [1, 2, 3]), RecordStatus.Active).Value;

        Assert.True(shift.Update(ShiftOf(days: [2, 3, 4, 6])).IsSuccess);

        Assert.Equal([2, 3, 4, 6], shift.WorkDayNumbers);
    }

    [Fact]
    public void A_failed_shift_update_changes_nothing()
    {
        var shift = Shift.Create(ShiftOf(days: [1, 2, 3]), RecordStatus.Active).Value;

        AssertFailure(shift.Update(ShiftOf(days: [])), ErrorType.Validation);

        Assert.Equal([1, 2, 3], shift.WorkDayNumbers);
    }

    // ---- Holidays ----

    [Fact]
    public void A_holiday_needs_a_name_and_a_known_type()
    {
        AssertFailure(Holiday.Create(new HolidayDetails(Today, " ", "Public")), ErrorType.Validation, "name");
        AssertFailure(Holiday.Create(new HolidayDetails(Today, "Water Festival", "Religious")), ErrorType.Validation, "type");
        Assert.True(Holiday.Create(new HolidayDetails(Today, "Water Festival", "Company")).IsSuccess);
    }

    // ---- Adjustment requests ----

    private static AttendanceAdjustmentDetails AdjustmentOf(
        TimeOnly? checkIn = null,
        TimeOnly? checkOut = null,
        AttendanceStatus? status = null,
        DateOnly? date = null,
        string reason = "Forgot to punch") =>
        new(1, date ?? Today, checkIn, checkOut, status, reason);

    [Fact]
    public void An_adjustment_must_ask_for_a_time_or_a_status()
    {
        var record = NewRecord();

        var result = AttendanceAdjustment.Create(AdjustmentOf(), record, false, Today);

        AssertFailure(result, ErrorType.Validation, "requestedCheckIn");
        Assert.Equal("Request at least one change: a time or a status.", result.Error.Message);
    }

    [Fact]
    public void An_adjustment_cannot_be_for_a_future_date()
    {
        var result = AttendanceAdjustment.Create(AdjustmentOf(status: AttendanceStatus.Present, date: Today.AddDays(1)), NewRecord(), false, Today);

        AssertFailure(result, ErrorType.Validation, "date");
    }

    [Fact]
    public void An_adjustment_needs_a_reason()
    {
        AssertFailure(
            AttendanceAdjustment.Create(AdjustmentOf(status: AttendanceStatus.Present, reason: " "), NewRecord(), false, Today),
            ErrorType.Validation,
            "reason");
    }

    [Fact]
    public void An_adjusted_check_out_must_be_after_the_check_in_unless_the_shift_is_a_night_shift()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(9, 0), Day, null);

        var dayShift = AttendanceAdjustment.Create(AdjustmentOf(checkOut: At(8, 0)), record, false, Today);
        var nightShift = AttendanceAdjustment.Create(AdjustmentOf(checkOut: At(8, 0)), record, true, Today);

        AssertFailure(dayShift, ErrorType.Validation, "requestedCheckOut");
        Assert.True(nightShift.IsSuccess);
    }

    [Fact]
    public void A_new_adjustment_is_pending_and_keeps_the_times_the_record_had()
    {
        var record = NewRecord();
        record.Punch(PunchKind.CheckIn, At(9, 30), Day, null);

        var adjustment = AttendanceAdjustment.Create(AdjustmentOf(checkIn: At(9, 0)), record, false, Today).Value;

        Assert.Equal(ApprovalStatus.Pending, adjustment.Status);
        Assert.Equal(At(9, 30), adjustment.CurrentCheckIn);
        Assert.Null(adjustment.CurrentCheckOut);
        Assert.Equal(At(9, 0), adjustment.RequestedCheckIn);
        Assert.Equal(Today, adjustment.RequestedOn);
    }

    // ---- Overtime requests ----

    private static OvertimeDetails OvertimeOf(TimeOnly start, TimeOnly end, DateOnly? date = null) =>
        new(1, date ?? Today, start, end, "Month end close");

    [Theory]
    [InlineData(18, 0, 18, 14, false)]
    [InlineData(18, 0, 18, 15, true)]
    [InlineData(18, 0, 6, 0, true)]
    [InlineData(18, 0, 6, 1, false)]
    [InlineData(9, 0, 9, 0, false)]
    public void Overtime_must_last_between_fifteen_minutes_and_twelve_hours(int startHour, int startMinute, int endHour, int endMinute, bool allowed)
    {
        var result = OvertimeRecord.Create(OvertimeOf(At(startHour, startMinute), At(endHour, endMinute)), Today);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            AssertFailure(result, ErrorType.Validation, "endTime");
        }
    }

    [Fact]
    public void The_overtime_minutes_are_derived_and_roll_over_midnight()
    {
        var overtime = OvertimeRecord.Create(OvertimeOf(At(22, 0), At(1, 30)), Today).Value;

        Assert.Equal(210, overtime.Minutes);
        Assert.Equal(ApprovalStatus.Pending, overtime.Status);
    }

    [Fact]
    public void Overtime_cannot_be_for_a_future_date()
    {
        AssertFailure(
            OvertimeRecord.Create(OvertimeOf(At(18, 0), At(19, 0), Today.AddDays(1)), Today),
            ErrorType.Validation,
            "date");
    }

    // ---- Deciding ----

    private static OvertimeRecord PendingOvertime() => OvertimeRecord.Create(OvertimeOf(At(18, 0), At(19, 0)), Today).Value;

    private static readonly ApprovalActor Manager = new(10, 2, "Test Manager");

    [Fact]
    public void Approving_records_who_decided_and_when_with_default_remarks()
    {
        var overtime = PendingOvertime();

        Assert.True(overtime.Decide(ApprovalDecision.Approve, Manager, null, Today).IsSuccess);

        Assert.Equal(ApprovalStatus.Approved, overtime.Status);
        Assert.Equal("Test Manager", overtime.DecidedByName);
        Assert.Equal(10, overtime.DecidedByUserId);
        Assert.Equal(Today, overtime.DecidedOn);
        Assert.Equal("Approved.", overtime.Remarks);
    }

    [Fact]
    public void Rejecting_needs_remarks()
    {
        var overtime = PendingOvertime();

        AssertFailure(overtime.Decide(ApprovalDecision.Reject, Manager, "  ", Today), ErrorType.Validation, "remarks");

        Assert.Equal(ApprovalStatus.Pending, overtime.Status);
        Assert.True(overtime.Decide(ApprovalDecision.Reject, Manager, "Not authorised", Today).IsSuccess);
        Assert.Equal("Not authorised", overtime.Remarks);
    }

    [Fact]
    public void A_decided_request_cannot_be_decided_again()
    {
        var overtime = PendingOvertime();
        overtime.Decide(ApprovalDecision.Approve, Manager, null, Today);

        var again = overtime.Decide(ApprovalDecision.Reject, Manager, "Changed my mind", Today);

        AssertFailure(again, ErrorType.Conflict);
        Assert.Equal("This request was already approved.", again.Error.Message);
    }

    [Fact]
    public void Nobody_decides_their_own_request_but_the_administrator_may()
    {
        var overtime = PendingOvertime();

        AssertFailure(overtime.Decide(ApprovalDecision.Approve, new ApprovalActor(11, 1, "Requester"), null, Today), ErrorType.Conflict);
        Assert.True(overtime.Decide(ApprovalDecision.Approve, new ApprovalActor(1, null, "Administrator"), null, Today).IsSuccess);
    }

    [Fact]
    public void Only_a_pending_request_can_be_deleted()
    {
        var overtime = PendingOvertime();
        Assert.True(overtime.EnsureCanDelete().IsSuccess);

        overtime.Decide(ApprovalDecision.Approve, Manager, null, Today);

        AssertFailure(overtime.EnsureCanDelete(), ErrorType.Conflict);
    }
}
