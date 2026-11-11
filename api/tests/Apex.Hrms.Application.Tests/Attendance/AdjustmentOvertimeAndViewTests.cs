using Apex.Hrms.Application.Attendance.Adjustments;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using static Apex.Hrms.Application.Tests.Fakes.AttendanceFixtures;

namespace Apex.Hrms.Application.Tests.Attendance;

/// <summary>Adjustment requests, overtime requests and the overview and schedule views. Today is Monday 2026-11-09.</summary>
public sealed class AdjustmentOvertimeAndViewTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private async Task<(OrgSeed Org, EmployeeDto Employee, ShiftDto Shift)> SeedWorkerAsync()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test", shift.Id);
        return (org, employee, shift);
    }

    // ---- Adjustments ----

    [Fact]
    public async Task An_adjustment_needs_an_attendance_record_for_that_day()
    {
        var (_, employee, _) = await SeedWorkerAsync();

        var result = await host.CreateAdjustment(employee.Id, Monday, At(9, 0));

        var error = ResultAssert.FailedOnField(result, "date");
        Assert.Equal("There is no attendance record for this employee on that date.", error.Message);
    }

    [Fact]
    public async Task An_adjustment_cannot_be_for_a_future_date()
    {
        var (_, employee, _) = await SeedWorkerAsync();

        var result = await host.CreateAdjustment(employee.Id, Monday.AddDays(1), At(9, 0));

        ResultAssert.FailedOnField(result, "date", "The date cannot be in the future.");
    }

    [Fact]
    public async Task An_adjustment_must_ask_for_something()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);

        var result = await host.CreateAdjustment(employee.Id);

        ResultAssert.FailedOnField(result, "requestedCheckIn", "Request at least one change: a time or a status.");
    }

    [Fact]
    public async Task An_adjustment_with_an_unknown_status_is_refused()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);

        ResultAssert.FailedOnField(await host.CreateAdjustment(employee.Id, status: "Sick"), "requestedStatus");
    }

    [Fact]
    public async Task An_adjusted_check_out_must_follow_the_check_in()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 0);

        var result = await host.CreateAdjustment(employee.Id, checkOut: At(8, 0));

        ResultAssert.FailedOnField(result, "requestedCheckOut");
    }

    [Fact]
    public async Task A_new_adjustment_is_pending_and_snapshots_the_current_times()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);

        var adjustment = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));

        Assert.Equal("Pending", adjustment.Status);
        Assert.Equal(At(9, 40), adjustment.CurrentCheckIn);
        Assert.Equal(At(9, 0), adjustment.RequestedCheckIn);
        Assert.Equal(Monday, adjustment.RequestedOn);
        Assert.Null(adjustment.DecidedBy);
        Assert.Equal(string.Empty, adjustment.Remarks);
    }

    [Fact]
    public async Task A_second_pending_adjustment_for_the_same_employee_and_date_is_refused()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));

        var duplicate = await host.CreateAdjustment(employee.Id, Monday, At(9, 5));

        var error = ResultAssert.FailedOnField(duplicate, "date");
        Assert.Equal("A pending request already exists for this employee and date.", error.Message);
    }

    [Fact]
    public async Task A_new_request_is_allowed_once_the_pending_one_has_been_decided()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        var first = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));
        ResultAssert.Succeeded(await host.RejectAdjustment(first));

        ResultAssert.Succeeded(await host.CreateAdjustment(employee.Id, Monday, At(9, 5)));
    }

    [Fact]
    public async Task Approving_an_adjustment_applies_it_to_the_record_and_derives_again()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var record = await host.PunchAsync(employee.Id, "CheckIn", 10, 0);
        await host.PunchAsync(employee.Id, "CheckOut", 18, 0);
        host.User.DisplayName = "Hana Approver";
        var adjustment = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));

        var approved = ResultAssert.Succeeded(await host.ApproveAdjustment(adjustment, "Verified with the gate log"));

        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Hana Approver", approved.DecidedBy);
        Assert.Equal(Monday, approved.DecidedOn);
        Assert.Equal("Verified with the gate log", approved.Remarks);
        var changed = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<AttendanceListItemDto>>().HandleAsync(record.Id, default));
        Assert.Equal(At(9, 0), changed.CheckIn);
        Assert.Equal(At(18, 0), changed.CheckOut);
        Assert.Equal("Present", changed.Status);
        Assert.Equal(0, changed.LateMinutes);
        Assert.Equal(480, changed.WorkedMinutes);
        Assert.Equal("Manual", changed.Source);
        Assert.Equal($"Adjusted (ADJ-{adjustment.Id:0000})", changed.Remarks);
    }

    [Fact]
    public async Task An_approved_status_wins_over_the_derived_one()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var record = await host.PunchAsync(employee.Id, "CheckIn", 9, 0);
        var adjustment = await host.CreateAdjustmentAsync(employee.Id, status: "Half Day");

        ResultAssert.Succeeded(await host.ApproveAdjustment(adjustment));

        var changed = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<AttendanceListItemDto>>().HandleAsync(record.Id, default));
        Assert.Equal("Half Day", changed.Status);
    }

    [Fact]
    public async Task Rejecting_an_adjustment_needs_remarks_and_leaves_the_record_alone()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var record = await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        var adjustment = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));

        ResultAssert.FailedOnField(await host.RejectAdjustment(adjustment, " "), "remarks");
        var rejected = ResultAssert.Succeeded(await host.RejectAdjustment(adjustment, "No evidence"));

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("No evidence", rejected.Remarks);
        var unchanged = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<AttendanceListItemDto>>().HandleAsync(record.Id, default));
        Assert.Equal(At(9, 40), unchanged.CheckIn);
        Assert.Equal("Web", unchanged.Source);
    }

    [Fact]
    public async Task A_decided_adjustment_cannot_be_decided_again()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        var adjustment = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));
        var approved = ResultAssert.Succeeded(await host.ApproveAdjustment(adjustment));

        var again = await host.ApproveAdjustment(approved);

        ResultAssert.Failed(again, ErrorType.Conflict, "This request was already approved.");
    }

    [Fact]
    public async Task Nobody_approves_their_own_adjustment()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        var adjustment = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));
        host.User.EmployeeId = employee.Id;

        var result = await host.ApproveAdjustment(adjustment);

        ResultAssert.Failed(result, ErrorType.Conflict);
    }

    [Fact]
    public async Task An_approval_that_no_longer_fits_the_record_is_refused()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 0);
        var adjustment = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(18, 0)); // fits: there is no check-out yet
        await host.PunchAsync(employee.Id, "CheckOut", 17, 0);

        var result = await host.ApproveAdjustment(adjustment);

        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Equal("Pending", ResultAssert.Succeeded(await host.Get<IGetByIdHandler<AdjustmentDto>>().HandleAsync(adjustment.Id, default)).Status);
    }

    [Fact]
    public async Task Only_a_pending_adjustment_can_be_deleted()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        var pending = await host.CreateAdjustmentAsync(employee.Id, checkIn: At(9, 0));
        var delete = host.Get<IDeleteHandler<AdjustmentDto>>();
        var approved = ResultAssert.Succeeded(await host.ApproveAdjustment(pending));

        ResultAssert.Failed(await delete.HandleAsync(approved.Id, Requests.Version(approved.RowVersion), default), ErrorType.Conflict);

        var another = ResultAssert.Succeeded(await host.CreateAdjustment(employee.Id, Monday, At(9, 5)));
        Assert.True((await delete.HandleAsync(another.Id, Requests.Version(another.RowVersion), default)).IsSuccess);
    }

    // ---- Overtime ----

    [Fact]
    public async Task Overtime_derives_the_minutes_and_starts_pending()
    {
        var (_, employee, _) = await SeedWorkerAsync();

        var overtime = await host.CreateOvertimeAsync(employee.Id, At(18, 0), At(20, 30));

        Assert.Equal(150, overtime.Minutes);
        Assert.Equal("Pending", overtime.Status);
        Assert.Equal(Monday, overtime.RequestedOn);
    }

    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(720, true)]
    [InlineData(721, false)]
    public async Task Overtime_lasts_between_fifteen_minutes_and_twelve_hours(int minutes, bool allowed)
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var start = At(6, 0);
        var end = start.AddMinutes(minutes);

        var result = await host.CreateOvertime(employee.Id, start, end);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            ResultAssert.FailedOnField(result, "endTime");
        }
    }

    [Fact]
    public async Task Overtime_is_recorded_once_per_employee_and_date()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.CreateOvertimeAsync(employee.Id, At(18, 0), At(19, 0));

        var duplicate = await host.CreateOvertime(employee.Id, At(20, 0), At(21, 0));

        var error = ResultAssert.FailedOnField(duplicate, "date");
        Assert.Equal("Overtime is already recorded for this employee on that date.", error.Message);
    }

    [Fact]
    public async Task Overtime_cannot_be_for_a_future_date()
    {
        var (_, employee, _) = await SeedWorkerAsync();

        ResultAssert.FailedOnField(await host.CreateOvertime(employee.Id, At(18, 0), At(19, 0), Monday.AddDays(1)), "date");
    }

    [Fact]
    public async Task Overtime_is_approved_with_the_name_of_the_approver()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var overtime = await host.CreateOvertimeAsync(employee.Id, At(18, 0), At(19, 0));
        host.User.DisplayName = "Hana Approver";

        var approved = ResultAssert.Succeeded(await host.ApproveOvertime(overtime));

        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Hana Approver", approved.DecidedBy);
        Assert.Equal(Monday, approved.DecidedOn);
        Assert.Equal("Approved.", approved.Remarks);
    }

    [Fact]
    public async Task Rejecting_overtime_needs_remarks_and_a_decided_entry_cannot_be_changed_or_deleted()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var overtime = await host.CreateOvertimeAsync(employee.Id, At(18, 0), At(19, 0));

        ResultAssert.FailedOnField(await host.RejectOvertime(overtime, null), "remarks");
        var rejected = ResultAssert.Succeeded(await host.RejectOvertime(overtime, "Not authorised"));

        Assert.Equal("Rejected", rejected.Status);
        ResultAssert.Failed(await host.ApproveOvertime(rejected), ErrorType.Conflict, "This request was already rejected.");
        ResultAssert.Failed(
            await host.Get<IDeleteHandler<OvertimeDto>>().HandleAsync(rejected.Id, Requests.Version(rejected.RowVersion), default),
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Nobody_approves_their_own_overtime()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var overtime = await host.CreateOvertimeAsync(employee.Id, At(18, 0), At(19, 0));
        host.User.EmployeeId = employee.Id;

        ResultAssert.Failed(await host.ApproveOvertime(overtime), ErrorType.Conflict);
    }

    // ---- Overview ----

    private Task<Result<AttendanceOverviewDto>> Overview(DateOnly date, int? branchId = null) =>
        host.Get<IAttendanceOverviewHandler>().HandleAsync(date, branchId, default);

    [Fact]
    public async Task The_overview_counts_present_late_and_absent_employees_of_the_day()
    {
        var (org, mia, shift) = await SeedWorkerAsync();
        var ben = await host.CreateWorkerAsync(org, "ben@example.test", shift.Id);
        await host.CreateWorkerAsync(org, "cho@example.test", shift.Id);
        await host.PunchAsync(mia.Id, "CheckIn", 9, 0);
        await host.PunchAsync(ben.Id, "CheckIn", 9, 40);

        var overview = ResultAssert.Succeeded(await Overview(Monday));

        Assert.Equal(Monday, overview.Summary.Date);
        Assert.Equal(3, overview.Summary.Scheduled);
        Assert.Equal(1, overview.Summary.Present);
        Assert.Equal(1, overview.Summary.Late);
        Assert.Equal(1, overview.Summary.Absent);
        Assert.Equal(0, overview.Summary.OnLeave);
        Assert.Equal(66.7m, overview.Summary.AttendanceRate);
        var department = Assert.Single(overview.ByDepartment);
        Assert.Equal(org.Department.Id, department.DepartmentId);
        Assert.Equal(3, department.Total);
        var late = Assert.Single(overview.Late);
        Assert.Equal(ben.Id, late.EmployeeId);
        Assert.Equal("Checked in 09:40 · 40 min late", late.Detail);
        var absent = Assert.Single(overview.Absent);
        Assert.Equal("No check-in recorded", absent.Detail);
        // Fourteen working days up to the date; the days before today have nobody recorded, so all three were absent.
        Assert.Equal(14, overview.Trend.Count);
        var trendDay = overview.Trend[^1];
        Assert.Equal(Monday, trendDay.Date);
        Assert.Equal(1, trendDay.Present);
        Assert.Equal(1, trendDay.Late);
        Assert.Equal(1, trendDay.Absent);
        Assert.Equal(3, overview.Trend[0].Absent);
        Assert.DoesNotContain(overview.Trend, day => day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    [Fact]
    public async Task A_weekend_and_a_holiday_have_nobody_scheduled()
    {
        var (_, _, _) = await SeedWorkerAsync();
        await host.CreateHolidayAsync(Monday.AddDays(1));

        var weekend = ResultAssert.Succeeded(await Overview(Saturday));
        var holiday = ResultAssert.Succeeded(await Overview(Monday.AddDays(1)));

        Assert.Equal(0, weekend.Summary.Scheduled);
        Assert.Equal(0, holiday.Summary.Scheduled);
        Assert.Equal(0m, weekend.Summary.AttendanceRate);
    }

    [Fact]
    public async Task A_future_day_has_no_absences()
    {
        await SeedWorkerAsync();

        var overview = ResultAssert.Succeeded(await Overview(Monday.AddDays(1)));

        Assert.Equal(0, overview.Summary.Absent);
    }

    [Fact]
    public async Task The_overview_can_be_limited_to_a_branch()
    {
        var (org, _, _) = await SeedWorkerAsync();

        Assert.Equal(1, ResultAssert.Succeeded(await Overview(Monday, org.Branch.Id)).Summary.Scheduled);
        Assert.Equal(0, ResultAssert.Succeeded(await Overview(Monday, org.Branch.Id + 100)).Summary.Scheduled);
    }

    [Fact]
    public async Task An_employee_on_approved_leave_counts_as_on_leave()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var type = await host.CreateTypeAsync();
        var leave = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, Monday));
        ResultAssert.Succeeded(await host.Approve(leave));

        var overview = ResultAssert.Succeeded(await Overview(Monday));

        Assert.Equal(1, overview.Summary.OnLeave);
        Assert.Equal(0, overview.Summary.Absent);
    }

    // ---- Schedule ----

    [Fact]
    public async Task The_schedule_has_a_row_per_employee_and_a_cell_per_day()
    {
        var (org, employee, shift) = await SeedWorkerAsync();
        await host.CreateHolidayAsync(Monday.AddDays(2), "Water Festival");
        var type = await host.CreateTypeAsync();
        var leave = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, Monday.AddDays(3)));
        ResultAssert.Succeeded(await host.Approve(leave));

        var week = ResultAssert.Succeeded(await host.Get<IWorkScheduleHandler>().HandleAsync(new WorkScheduleRequest(Monday), default));

        Assert.Equal(Monday, week.WeekStart);
        Assert.Equal(7, week.Days.Count);
        var row = Assert.Single(week.Rows);
        Assert.Equal(employee.Id, row.EmployeeId);
        Assert.Equal(org.Department.Name, row.Department);
        Assert.Equal(shift.Name, row.ShiftName);
        Assert.Equal(shift.Color, row.ShiftColor);
        Assert.Equal(7, row.Cells.Count);
        Assert.Equal(new ScheduleCellDto(Monday, "Shift", "09:00–18:00"), row.Cells[0]);
        Assert.Equal(new ScheduleCellDto(Monday.AddDays(2), "Holiday", "Water Festival"), row.Cells[2]);
        Assert.Equal(new ScheduleCellDto(Monday.AddDays(3), "Leave", "On leave"), row.Cells[3]);
        Assert.Equal(new ScheduleCellDto(Monday.AddDays(5), "Off", "Off"), row.Cells[5]);
    }

    [Fact]
    public async Task The_schedule_can_be_filtered_by_department_and_branch()
    {
        var (org, _, _) = await SeedWorkerAsync();
        var handler = host.Get<IWorkScheduleHandler>();

        Assert.Single(ResultAssert.Succeeded(await handler.HandleAsync(new WorkScheduleRequest(Monday, org.Department.Id, org.Branch.Id), default)).Rows);
        Assert.Empty(ResultAssert.Succeeded(await handler.HandleAsync(new WorkScheduleRequest(Monday, org.Department.Id + 100), default)).Rows);
        Assert.Empty(ResultAssert.Succeeded(await handler.HandleAsync(new WorkScheduleRequest(Monday, null, org.Branch.Id + 100), default)).Rows);
    }
}
