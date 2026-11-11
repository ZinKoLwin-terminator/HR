using Apex.Hrms.Application.Attendance.Holidays;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using static Apex.Hrms.Application.Tests.Fakes.AttendanceFixtures;

namespace Apex.Hrms.Application.Tests.Attendance;

/// <summary>Shifts, holidays and punches. Today is Monday 2026-11-09 in the fake clock.</summary>
public sealed class ShiftHolidayAndPunchTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    // ---- Shifts ----

    [Fact]
    public async Task A_shift_code_must_be_unique()
    {
        await host.CreateShiftAsync();

        var duplicate = await host.CreateShift(NewShift(name: "Other"));

        var error = ResultAssert.FailedOnField(duplicate, "code");
        Assert.Equal("Shift code \"GEN\" already exists.", error.Message);
    }

    [Fact]
    public async Task A_shift_needs_a_working_day_and_an_end_after_the_start()
    {
        ResultAssert.FailedOnField(await host.CreateShift(NewShift(workDays: [])), "workDays");
        ResultAssert.FailedOnField(await host.CreateShift(NewShift(start: At(18, 0), end: At(9, 0))), "endTime");
        ResultAssert.Succeeded(await host.CreateShift(NewShift(code: "NIGHT", start: At(22, 0), end: At(6, 0), night: true)));
    }

    [Fact]
    public async Task A_shift_is_returned_with_sorted_work_days_and_updated_with_its_row_version()
    {
        var shift = await host.CreateShiftAsync(NewShift(workDays: [5, 1, 3]));
        Assert.Equal([1, 3, 5], shift.WorkDays);

        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<ShiftUpdateRequest, ShiftDto>>()
            .HandleAsync(shift.Id, ShiftUpdateOf(shift, "Renamed"), default));

        Assert.Equal("Renamed", updated.Name);
        Assert.NotEqual(shift.RowVersion, updated.RowVersion);
    }

    [Fact]
    public async Task A_stale_shift_update_is_a_conflict()
    {
        var shift = await host.CreateShiftAsync();
        ResultAssert.Succeeded(await host.Get<IUpdateHandler<ShiftUpdateRequest, ShiftDto>>().HandleAsync(shift.Id, ShiftUpdateOf(shift, "First"), default));

        var stale = await host.Get<IUpdateHandler<ShiftUpdateRequest, ShiftDto>>().HandleAsync(shift.Id, ShiftUpdateOf(shift, "Second"), default);

        ResultAssert.Failed(stale, ErrorType.Conflict);
    }

    [Fact]
    public async Task A_shift_can_be_deactivated_and_activated_again()
    {
        var shift = await host.CreateShiftAsync();

        var off = ResultAssert.Succeeded(await host.Command<DeactivateCommand, ShiftDto>(new DeactivateCommand(shift.Id, Requests.Version(shift.RowVersion))));
        var on = ResultAssert.Succeeded(await host.Command<ActivateCommand, ShiftDto>(new ActivateCommand(shift.Id, Requests.Version(off.RowVersion))));

        Assert.Equal("Inactive", off.Status);
        Assert.Equal("Active", on.Status);
    }

    [Fact]
    public async Task A_shift_used_by_an_employee_cannot_be_deleted()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        await host.CreateWorkerAsync(org, "mia@example.test", shift.Id);

        var result = await host.Get<IDeleteHandler<ShiftDto>>().HandleAsync(shift.Id, Requests.Version(shift.RowVersion), default);

        var error = ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Equal(
            "\"General Shift\" cannot be deleted because it is used by 1 assigned employee(s). Deactivate it instead.",
            error.Message);
    }

    [Fact]
    public async Task A_shift_used_by_an_attendance_record_cannot_be_deleted()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test"); // no shift of her own: she works the first shift
        await host.PunchAsync(employee.Id, "CheckIn", 9, 0);

        var result = await host.Get<IDeleteHandler<ShiftDto>>().HandleAsync(shift.Id, Requests.Version(shift.RowVersion), default);

        var error = ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Contains("1 attendance record(s)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unused_shift_can_be_deleted()
    {
        var shift = await host.CreateShiftAsync();

        var result = await host.Get<IDeleteHandler<ShiftDto>>().HandleAsync(shift.Id, Requests.Version(shift.RowVersion), default);

        Assert.True(result.IsSuccess);
    }

    // ---- Holidays ----

    [Fact]
    public async Task Holidays_are_unique_by_date()
    {
        await host.CreateHolidayAsync(new DateOnly(2026, 11, 20));

        var duplicate = await host.CreateHoliday(new DateOnly(2026, 11, 20), "Another");

        var error = ResultAssert.FailedOnField(duplicate, "date");
        Assert.Equal("A holiday already exists on 2026-11-20.", error.Message);
    }

    [Fact]
    public async Task A_holiday_may_keep_its_date_when_updated_but_not_take_another_holidays_date()
    {
        var first = await host.CreateHolidayAsync(new DateOnly(2026, 11, 20));
        await host.CreateHolidayAsync(new DateOnly(2026, 11, 21), "Second");
        var update = host.Get<IUpdateHandler<HolidayUpdateRequest, HolidayDto>>();

        var same = await update.HandleAsync(first.Id, new HolidayUpdateRequest(first.Date, "Renamed", "Company", Requests.Version(first.RowVersion)), default);
        var clash = await update.HandleAsync(first.Id, new HolidayUpdateRequest(new DateOnly(2026, 11, 21), "Renamed", "Company", Requests.Version(ResultAssert.Succeeded(same).RowVersion)), default);

        Assert.Equal("Company", same.Value.Type);
        ResultAssert.FailedOnField(clash, "date");
    }

    [Fact]
    public async Task The_holiday_query_filters_by_date_range()
    {
        await host.CreateHolidayAsync(new DateOnly(2026, 10, 30), "Before");
        await host.CreateHolidayAsync(new DateOnly(2026, 11, 12), "Inside");
        await host.CreateHolidayAsync(new DateOnly(2026, 12, 25), "After");

        var page = ResultAssert.Succeeded(await host.Get<IQueryHandler<HolidayDto, HolidayFilter>>().HandleAsync(
            new PageRequest<HolidayFilter>(0, 50, null, null, new HolidayFilter(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))), default));

        Assert.Equal("Inside", Assert.Single(page.Items).Name);
    }

    // ---- Punches ----

    private async Task<(OrgSeed Org, EmployeeDto Employee, ShiftDto Shift)> SeedWorkerAsync(ShiftCreateRequest? shift = null)
    {
        var org = await host.SeedOrgAsync();
        var created = await host.CreateShiftAsync(shift);
        var employee = await host.CreateWorkerAsync(org, "mia@example.test", created.Id);
        return (org, employee, created);
    }

    [Fact]
    public async Task A_punch_is_stamped_with_the_server_date_and_time()
    {
        var (_, employee, shift) = await SeedWorkerAsync();

        var record = await host.PunchAsync(employee.Id, "CheckIn", 9, 5);

        Assert.Equal(Monday, record.Date);
        Assert.Equal(At(9, 5), record.CheckIn);
        Assert.Null(record.CheckOut);
        Assert.Equal(shift.Id, record.ShiftId);
        Assert.Equal("Present", record.Status);
        Assert.Equal("Web", record.Source);
    }

    [Fact]
    public async Task A_late_check_in_derives_late_minutes_and_status()
    {
        var (_, employee, _) = await SeedWorkerAsync();

        var record = await host.PunchAsync(employee.Id, "CheckIn", 9, 40);

        Assert.Equal("Late", record.Status);
        Assert.Equal(40, record.LateMinutes);
        Assert.Equal("Late arrival", record.Remarks);
    }

    [Fact]
    public async Task A_check_out_completes_the_day_and_derives_the_minutes()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var checkedIn = await host.PunchAsync(employee.Id, "CheckIn", 9, 0);

        var record = await host.PunchAsync(employee.Id, "CheckOut", 19, 0);

        Assert.Equal(checkedIn.Id, record.Id);
        Assert.Equal(At(19, 0), record.CheckOut);
        Assert.Equal(540, record.WorkedMinutes);
        Assert.Equal(60, record.OvertimeMinutes);
    }

    [Fact]
    public async Task A_second_check_in_the_same_day_is_a_conflict_and_there_is_one_record_per_day()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 0);
        host.Clock.TimeNow = At(9, 30);

        var again = await host.Punch(employee.Id, "CheckIn");

        ResultAssert.Failed(again, ErrorType.Conflict, "Already checked in at 09:00.");
        Assert.Single(host.Repository<Apex.Hrms.Domain.Attendance.AttendanceRecord>().Items);
    }

    [Fact]
    public async Task A_check_out_without_a_check_in_is_a_conflict()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        host.Clock.TimeNow = At(18, 0);

        var result = await host.Punch(employee.Id, "CheckOut");

        ResultAssert.Failed(result, ErrorType.Conflict, "Check in before checking out.");
    }

    [Fact]
    public async Task A_day_shift_check_out_at_the_check_in_time_is_refused()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 10, 0);
        host.Clock.TimeNow = At(10, 0);

        var result = await host.Punch(employee.Id, "CheckOut");

        ResultAssert.Failed(result, ErrorType.Validation, "The check-out time must be after the check-in time.");
    }

    [Fact]
    public async Task A_night_shift_check_out_may_be_earlier_than_the_check_in()
    {
        var (_, employee, _) = await SeedWorkerAsync(NewShift(code: "NIGHT", start: At(22, 0), end: At(6, 0), night: true));
        await host.PunchAsync(employee.Id, "CheckIn", 22, 0);

        var record = await host.PunchAsync(employee.Id, "CheckOut", 6, 0);

        Assert.Equal(At(6, 0), record.CheckOut);
        Assert.Equal(420, record.WorkedMinutes);
    }

    [Fact]
    public async Task A_night_shift_check_out_after_midnight_closes_the_record_of_the_day_before()
    {
        var (_, employee, _) = await SeedWorkerAsync(NewShift(code: "NIGHT", start: At(22, 0), end: At(6, 0), night: true, workDays: [1, 2, 3, 4, 5]));
        var opened = await host.PunchAsync(employee.Id, "CheckIn", 22, 0);
        host.Clock.Today = Monday.AddDays(1);

        var closed = await host.PunchAsync(employee.Id, "CheckOut", 6, 0);

        Assert.Equal(opened.Id, closed.Id);
        Assert.Equal(Monday, closed.Date);
        Assert.Equal(At(6, 0), closed.CheckOut);
        Assert.Single(host.Repository<Apex.Hrms.Domain.Attendance.AttendanceRecord>().Items);
    }

    [Fact]
    public async Task A_check_out_after_midnight_of_a_day_shift_is_not_attached_to_yesterday()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 0);
        host.Clock.Today = Monday.AddDays(1);
        host.Clock.TimeNow = At(1, 0);

        ResultAssert.Failed(await host.Punch(employee.Id, "CheckOut"), ErrorType.Conflict, "Check in before checking out.");
    }

    [Fact]
    public async Task The_client_cannot_choose_the_kind_of_a_punch_freely()
    {
        var (_, employee, _) = await SeedWorkerAsync();

        var result = await host.Get<IPunchHandler>().HandleAsync(new PunchRequest(employee.Id, "in"), default);

        ResultAssert.FailedOnField(result, "kind");
    }

    [Fact]
    public async Task A_punch_for_an_unknown_employee_is_not_found()
    {
        await SeedWorkerAsync();

        ResultAssert.Failed(await host.Punch(9999, "CheckIn"), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_signed_in_employee_can_punch_only_for_themselves()
    {
        var (org, employee, _) = await SeedWorkerAsync();
        var other = await host.CreateWorkerAsync(org, "ben@example.test");
        host.User.EmployeeId = employee.Id;

        ResultAssert.Failed(await host.Punch(other.Id, "CheckIn"), ErrorType.Forbidden);
        ResultAssert.Succeeded(await host.Punch(employee.Id, "CheckIn"));
    }

    [Fact]
    public async Task The_administrator_who_is_no_employee_may_punch_for_anyone_and_nobody_signed_out_may_punch()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        host.User.EmployeeId = null;
        ResultAssert.Succeeded(await host.Punch(employee.Id, "CheckIn"));

        host.User.UserId = null;
        host.User.IsAuthenticated = false;

        ResultAssert.Failed(await host.Punch(employee.Id, "CheckOut"), ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Punching_without_any_shift_is_a_conflict()
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test");

        ResultAssert.Failed(await host.Punch(employee.Id, "CheckIn"), ErrorType.Conflict);
    }

    [Fact]
    public async Task Working_on_a_weekend_is_present_with_a_remark()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        host.Clock.Today = Saturday;

        var record = await host.PunchAsync(employee.Id, "CheckIn", 11, 0);

        Assert.Equal("Present", record.Status);
        Assert.Equal(0, record.LateMinutes);
        Assert.Equal("Worked on a weekend", record.Remarks);
    }

    [Fact]
    public async Task Working_on_a_holiday_is_present_with_a_remark()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.CreateHolidayAsync(Monday);

        var record = await host.PunchAsync(employee.Id, "CheckIn", 9, 50);

        Assert.Equal("Present", record.Status);
        Assert.Equal("Worked on a holiday", record.Remarks);
    }

    [Fact]
    public async Task A_day_of_approved_leave_cannot_be_punched()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test", shift.Id);
        var type = await host.CreateTypeAsync();
        var leave = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, Monday));
        ResultAssert.Succeeded(await host.Approve(leave));

        var result = await host.Punch(employee.Id, "CheckIn");

        ResultAssert.Failed(result, ErrorType.Conflict, "You are on approved leave on this day.");
    }

    [Fact]
    public async Task Records_are_listed_with_names_and_filtered()
    {
        var (org, employee, shift) = await SeedWorkerAsync();
        var other = await host.CreateWorkerAsync(org, "ben@example.test", shift.Id);
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        await host.PunchAsync(other.Id, "CheckIn", 9, 0);
        var query = host.Get<IQueryHandler<AttendanceListItemDto, AttendanceFilter>>();

        var late = ResultAssert.Succeeded(await query.HandleAsync(
            new PageRequest<AttendanceFilter>(0, 20, null, null, new AttendanceFilter(LateOnly: true)), default));
        var byDepartment = ResultAssert.Succeeded(await query.HandleAsync(
            new PageRequest<AttendanceFilter>(0, 20, null, null, new AttendanceFilter(DepartmentId: org.Department.Id, DateFrom: Monday, DateTo: Monday)), default));
        var otherDay = ResultAssert.Succeeded(await query.HandleAsync(
            new PageRequest<AttendanceFilter>(0, 20, null, null, new AttendanceFilter(DateFrom: Monday.AddDays(1))), default));

        var item = Assert.Single(late.Items);
        Assert.Equal(employee.Id, item.EmployeeId);
        Assert.Equal("General Shift", item.ShiftName);
        Assert.Equal(org.Department.Name, item.DepartmentName);
        Assert.Equal(org.Branch.Name, item.BranchName);
        Assert.Equal(2, byDepartment.Total);
        Assert.Empty(otherDay.Items);
    }

    [Fact]
    public async Task A_record_is_read_by_id_and_a_missing_one_is_not_found()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        var record = await host.PunchAsync(employee.Id, "CheckIn", 9, 0);
        var get = host.Get<IGetByIdHandler<AttendanceListItemDto>>();

        var found = ResultAssert.Succeeded(await get.HandleAsync(record.Id, default));

        Assert.Equal(record.Id, found.Id);
        Assert.Equal(employee.EmployeeCode, found.EmployeeCode);
        ResultAssert.Failed(await get.HandleAsync(9999, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task An_employee_with_attendance_records_cannot_be_deleted()
    {
        var (_, employee, _) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 0);
        var current = ResultAssert.Succeeded(await host.GetEmployee(employee.Id));

        var result = await host.Get<IDeleteHandler<EmployeeDto>>().HandleAsync(employee.Id, Requests.Version(current.RowVersion), default);

        var error = ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Contains("1 attendance record(s)", error.Message, StringComparison.Ordinal);
    }
}
