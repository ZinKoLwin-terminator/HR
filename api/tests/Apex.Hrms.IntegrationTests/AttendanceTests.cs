using Apex.Hrms.Application;
using Apex.Hrms.Application.Attendance.Adjustments;
using Apex.Hrms.Application.Attendance.Approvals;
using Apex.Hrms.Application.Attendance.Holidays;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The attendance module against SQL Server: the handlers with the real EF mapping, the time(0) and bigint columns, the
/// check constraints, the unique indexes and the audit stamps. The clock is a fixed instant: 08:00 UTC on Monday 2026-11-09,
/// which is 14:30 on that Monday in the business time zone (Asia/Yangon, UTC+6:30), so the punch time is predictable.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class AttendanceTests : DatabaseTest
{
    private static readonly DateOnly Monday = new(2026, 11, 9);

    // A 14:00-22:00 day shift: a punch at 14:30 is 30 minutes late (15 minutes grace).
    private ShiftCreateRequest NewShift(string suffix = "S1", int[]? workDays = null, bool night = false, TimeOnly? start = null, TimeOnly? end = null) => new(
        Code(suffix),
        Name("Afternoon"),
        start ?? new TimeOnly(14, 0),
        end ?? new TimeOnly(22, 0),
        30,
        15,
        workDays ?? [1, 2, 3, 4, 5],
        night,
        "#3f6fd8",
        "Active");

    private async Task<ShiftDto> SeedShiftAsync(ShiftCreateRequest? request = null) =>
        Succeeded(await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<ShiftCreateRequest, ShiftDto>>().HandleAsync(request ?? NewShift(), default)));

    private async Task<EmployeeDto> SeedWorkerAsync(
        (CompanyDto Company, BranchDto Branch, DepartmentDto Department, JobGradeDto Grade, PositionDto Position) org,
        int? shiftId,
        string codeSuffix = "1",
        string local = "mia") =>
        Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, codeSuffix, local) with { ShiftId = shiftId }));

    private Task<Result<AttendanceRecordDto>> Punch(int employeeId, string kind) =>
        InScopeAsync(sp => sp.GetRequiredService<IPunchHandler>().HandleAsync(new PunchRequest(employeeId, kind), default));

    private Task<Result<AdjustmentDto>> CreateAdjustment(int employeeId, TimeOnly? checkIn = null, TimeOnly? checkOut = null, string? status = null, DateOnly? date = null) =>
        InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<AdjustmentCreateRequest, AdjustmentDto>>()
            .HandleAsync(new AdjustmentCreateRequest(employeeId, date ?? Monday, checkIn, checkOut, status, "Forgot to punch"), default));

    private Task<Result<OvertimeDto>> CreateOvertime(int employeeId, TimeOnly start, TimeOnly end) =>
        InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<OvertimeCreateRequest, OvertimeDto>>()
            .HandleAsync(new OvertimeCreateRequest(employeeId, Monday, start, end, "Month end close"), default));

    private Task<Result<AttendanceListItemDto>> GetRecord(int id) => Get<AttendanceListItemDto>(id);

    // ---- Shifts ----

    [IntegrationFact]
    public async Task A_shift_is_saved_with_its_work_days_and_audit_stamps_and_updated_with_a_new_row_version()
    {
        var shift = await SeedShiftAsync(NewShift(workDays: [5, 1, 3]));

        Assert.Equal([1, 3, 5], shift.WorkDays);
        Assert.Equal(new TimeOnly(14, 0), shift.StartTime);
        Assert.Equal(8, shift.RowVersion.Length);
        var stored = await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM att.ShiftWorkDay WHERE ShiftId = @id", ("@id", shift.Id));
        Assert.Equal(3, stored);

        var update = new ShiftUpdateRequest(shift.Code, Name("Renamed"), shift.StartTime, shift.EndTime, 45, 10, [2, 3, 4, 6], false, "#112233", Requests.Version(shift.RowVersion));
        var changed = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<ShiftUpdateRequest, ShiftDto>>().HandleAsync(shift.Id, update, default)));

        Assert.Equal([2, 3, 4, 6], changed.WorkDays);
        Assert.NotEqual(shift.RowVersion, changed.RowVersion);
        Assert.Equal(4, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM att.ShiftWorkDay WHERE ShiftId = @id", ("@id", shift.Id)));
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT CreatedByUserId FROM att.Shift WHERE Id = @id", ("@id", shift.Id)));
    }

    [IntegrationFact]
    public async Task A_duplicate_shift_code_is_a_validation_error_on_code()
    {
        await SeedShiftAsync();

        var duplicate = await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<ShiftCreateRequest, ShiftDto>>().HandleAsync(NewShift(), default));

        FailedOnField(duplicate, "code");
    }

    [IntegrationFact]
    public async Task A_shift_used_by_an_employee_or_an_attendance_record_cannot_be_deleted()
    {
        var org = await SeedOrganizationAsync();
        var used = await SeedShiftAsync(NewShift("S1"));
        var withRecords = await SeedShiftAsync(NewShift("S2"));
        await SeedWorkerAsync(org, used.Id);
        var other = await SeedWorkerAsync(org, withRecords.Id, "2", "ben");
        Succeeded(await Punch(other.Id, "CheckIn"));

        var byEmployee = await Delete<ShiftDto>(used.Id, Requests.Version(used.RowVersion));
        var byRecord = await Delete<ShiftDto>(withRecords.Id, Requests.Version(withRecords.RowVersion));

        Assert.Contains("1 assigned employee(s)", Failed(byEmployee, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("1 attendance record(s)", Failed(byRecord, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task An_unused_shift_is_deleted_with_its_work_days()
    {
        var shift = await SeedShiftAsync();

        Assert.True((await Delete<ShiftDto>(shift.Id, Requests.Version(shift.RowVersion))).IsSuccess);

        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM att.ShiftWorkDay WHERE ShiftId = @id", ("@id", shift.Id)));
        Failed(await Get<ShiftDto>(shift.Id), ErrorType.NotFound);
    }

    [IntegrationFact]
    public async Task A_shift_can_be_deactivated_and_activated()
    {
        var shift = await SeedShiftAsync();

        var off = Succeeded(await Deactivate<ShiftDto>(shift.Id, Requests.Version(shift.RowVersion)));
        var on = Succeeded(await Activate<ShiftDto>(shift.Id, Requests.Version(off.RowVersion)));

        Assert.Equal("Inactive", off.Status);
        Assert.Equal("Active", on.Status);
    }

    // ---- Holidays ----

    [IntegrationFact]
    public async Task Holidays_are_unique_by_date_and_queried_by_date_range()
    {
        var holiday = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<HolidayCreateRequest, HolidayDto>>()
            .HandleAsync(new HolidayCreateRequest(new DateOnly(2041, 3, 5), Code("HOL"), "Public"), default)));

        var duplicate = await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<HolidayCreateRequest, HolidayDto>>()
            .HandleAsync(new HolidayCreateRequest(new DateOnly(2041, 3, 5), Code("HOL2"), "Company"), default));
        var page = Succeeded(await Query<HolidayDto, HolidayFilter>(
            new PageRequest<HolidayFilter>(0, 20, null, Tag, new HolidayFilter(new DateOnly(2041, 3, 1), new DateOnly(2041, 3, 31)))));

        FailedOnField(duplicate, "date");
        Assert.Equal(holiday.Id, Assert.Single(page.Items).Id);
        Assert.Empty(Succeeded(await Query<HolidayDto, HolidayFilter>(
            new PageRequest<HolidayFilter>(0, 20, null, Tag, new HolidayFilter(new DateOnly(2041, 4, 1), null)))).Items);
    }

    // ---- Punches ----

    [IntegrationFact]
    public async Task A_punch_in_and_out_is_stamped_with_server_time_and_the_minutes_are_stored()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);

        var checkedIn = Succeeded(await Punch(employee.Id, "CheckIn"));

        Assert.Equal(Monday, checkedIn.Date);
        Assert.Equal(new TimeOnly(14, 30), checkedIn.CheckIn);
        Assert.Equal("Late", checkedIn.Status);
        Assert.Equal(30, checkedIn.LateMinutes);
        Assert.Equal("Web", checkedIn.Source);
        Assert.Equal(shift.Id, checkedIn.ShiftId);

        Time.Advance(TimeSpan.FromHours(8));
        var checkedOut = Succeeded(await Punch(employee.Id, "CheckOut"));

        Assert.Equal(checkedIn.Id, checkedOut.Id);
        Assert.Equal(new TimeOnly(22, 30), checkedOut.CheckOut);
        Assert.Equal(450, checkedOut.WorkedMinutes);
        Assert.Equal(0, checkedOut.OvertimeMinutes);
        Assert.NotEqual(checkedIn.RowVersion, checkedOut.RowVersion);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM att.AttendanceRecord WHERE EmployeeId = @id", ("@id", employee.Id)));
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT ModifiedByUserId FROM att.AttendanceRecord WHERE Id = @id", ("@id", checkedIn.Id)));
    }

    [IntegrationFact]
    public async Task Overtime_after_a_long_day_is_derived_when_it_exceeds_the_threshold()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);
        Succeeded(await Punch(employee.Id, "CheckIn"));

        Time.Advance(TimeSpan.FromHours(8) + TimeSpan.FromMinutes(1));
        var record = Succeeded(await Punch(employee.Id, "CheckOut"));

        Assert.Equal(31, record.OvertimeMinutes);
    }

    [IntegrationFact]
    public async Task A_second_check_in_and_a_check_out_before_a_check_in_are_conflicts()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);

        Failed(await Punch(employee.Id, "CheckOut"), ErrorType.Conflict);
        Succeeded(await Punch(employee.Id, "CheckIn"));
        Failed(await Punch(employee.Id, "CheckIn"), ErrorType.Conflict);
    }

    [IntegrationFact]
    public async Task A_night_shift_check_out_earlier_than_the_check_in_is_stored()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync(NewShift("N1", night: true, start: new TimeOnly(14, 0), end: new TimeOnly(6, 0)));
        var employee = await SeedWorkerAsync(org, shift.Id);
        Succeeded(await Punch(employee.Id, "CheckIn"));

        Time.Advance(TimeSpan.FromHours(10)); // 00:30 on Tuesday: the check-out time is earlier than the check-in time
        var record = Succeeded(await Punch(employee.Id, "CheckOut"));

        Assert.Equal(new TimeOnly(0, 30), record.CheckOut);
        Assert.Equal(Monday, record.Date); // the check-out closes the record the night shift opened on Monday
    }

    [IntegrationFact]
    public async Task One_record_per_employee_per_day_is_enforced_by_the_database()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);
        Succeeded(await Punch(employee.Id, "CheckIn"));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecuteAsync(
            "INSERT INTO att.AttendanceRecord (EmployeeId, [Date], ShiftId, Status, Source) VALUES (@id, '2026-11-09', @shift, 'Absent', 'Web')",
            ("@id", employee.Id),
            ("@shift", shift.Id)));
        Assert.Contains("UQ_AttendanceRecord_EmployeeId_Date", error.Message, StringComparison.Ordinal);
    }

    // ---- Adjustments ----

    [IntegrationFact]
    public async Task An_approved_adjustment_is_applied_to_the_record_and_derived_again()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);
        var record = Succeeded(await Punch(employee.Id, "CheckIn")); // 14:30, late by 30
        Time.Advance(TimeSpan.FromHours(8));
        Succeeded(await Punch(employee.Id, "CheckOut")); // 22:30
        var adjustment = Succeeded(await CreateAdjustment(employee.Id, checkIn: new TimeOnly(14, 0)));
        Assert.Equal("Pending", adjustment.Status);
        Assert.Equal(new TimeOnly(14, 30), adjustment.CurrentCheckIn);

        var approved = Succeeded(await RunCommand<ApproveAdjustmentCommand, AdjustmentDto>(
            new ApproveAdjustmentCommand(adjustment.Id, Requests.Version(adjustment.RowVersion), "Checked the gate log")));

        Assert.Equal("Approved", approved.Status);
        Assert.Equal($"{Tag} Test User", approved.DecidedBy);
        Assert.Equal(Monday, approved.DecidedOn);
        var changed = Succeeded(await GetRecord(record.Id));
        Assert.Equal(new TimeOnly(14, 0), changed.CheckIn);
        Assert.Equal(new TimeOnly(22, 30), changed.CheckOut);
        Assert.Equal(0, changed.LateMinutes);
        Assert.Equal("Present", changed.Status);
        Assert.Equal(480, changed.WorkedMinutes); // 14:00 to 22:30 is 510 minutes, less the 30 minute break
        Assert.Equal("Manual", changed.Source);
        Assert.Equal($"Adjusted (ADJ-{adjustment.Id:0000})", changed.Remarks);
        Assert.NotEqual(record.RowVersion, changed.RowVersion);
    }

    [IntegrationFact]
    public async Task A_duplicate_pending_adjustment_is_refused_and_a_decided_one_allows_a_new_request()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);
        Succeeded(await Punch(employee.Id, "CheckIn"));
        var first = Succeeded(await CreateAdjustment(employee.Id, checkIn: new TimeOnly(14, 0)));

        FailedOnField(await CreateAdjustment(employee.Id, checkIn: new TimeOnly(14, 5)), "date");
        Succeeded(await RunCommand<RejectAdjustmentCommand, AdjustmentDto>(
            new RejectAdjustmentCommand(first.Id, Requests.Version(first.RowVersion), "No evidence")));

        Succeeded(await CreateAdjustment(employee.Id, checkIn: new TimeOnly(14, 5)));
        Assert.Equal(1, await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM att.AttendanceAdjustment WHERE EmployeeId = @id AND Status = 'Pending'", ("@id", employee.Id)));
    }

    [IntegrationFact]
    public async Task An_adjustment_needs_an_existing_record_and_a_rejection_needs_remarks()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);

        FailedOnField(await CreateAdjustment(employee.Id, checkIn: new TimeOnly(14, 0)), "date");

        Succeeded(await Punch(employee.Id, "CheckIn"));
        var adjustment = Succeeded(await CreateAdjustment(employee.Id, status: "Half Day"));
        FailedOnField(
            await RunCommand<RejectAdjustmentCommand, AdjustmentDto>(new RejectAdjustmentCommand(adjustment.Id, Requests.Version(adjustment.RowVersion), null)),
            "remarks");
    }

    [IntegrationFact]
    public async Task A_stale_adjustment_decision_is_a_conflict()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);
        Succeeded(await Punch(employee.Id, "CheckIn"));
        var adjustment = Succeeded(await CreateAdjustment(employee.Id, status: "Half Day"));
        Succeeded(await RunCommand<RejectAdjustmentCommand, AdjustmentDto>(
            new RejectAdjustmentCommand(adjustment.Id, Requests.Version(adjustment.RowVersion), "No")));

        var stale = await RunCommand<ApproveAdjustmentCommand, AdjustmentDto>(
            new ApproveAdjustmentCommand(adjustment.Id, Requests.Version(adjustment.RowVersion), null));

        Failed(stale, ErrorType.Conflict);
    }

    // ---- Overtime ----

    [IntegrationFact]
    public async Task Overtime_is_requested_with_derived_minutes_and_approved_with_the_approver_name()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedWorkerAsync(org, null);

        var overtime = Succeeded(await CreateOvertime(employee.Id, new TimeOnly(18, 0), new TimeOnly(20, 30)));

        Assert.Equal(150, overtime.Minutes);
        Assert.Equal("Pending", overtime.Status);
        var approved = Succeeded(await RunCommand<ApproveOvertimeCommand, OvertimeDto>(
            new ApproveOvertimeCommand(overtime.Id, Requests.Version(overtime.RowVersion), null)));
        Assert.Equal("Approved", approved.Status);
        Assert.Equal($"{Tag} Test User", approved.DecidedBy);
        Assert.Equal(Monday, approved.DecidedOn);
        Assert.Equal("Approved.", approved.Remarks);
        Assert.Equal((short)150, await Sql.ScalarAsync<short>("SELECT [Minutes] FROM att.OvertimeRecord WHERE Id = @id", ("@id", overtime.Id)));
    }

    [IntegrationFact]
    public async Task Overtime_bounds_and_one_entry_per_day_are_enforced()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedWorkerAsync(org, null);

        FailedOnField(await CreateOvertime(employee.Id, new TimeOnly(18, 0), new TimeOnly(18, 14)), "endTime");
        FailedOnField(await CreateOvertime(employee.Id, new TimeOnly(18, 0), new TimeOnly(6, 1)), "endTime");
        Succeeded(await CreateOvertime(employee.Id, new TimeOnly(18, 0), new TimeOnly(6, 0))); // 12 hours across midnight
        FailedOnField(await CreateOvertime(employee.Id, new TimeOnly(9, 0), new TimeOnly(10, 0)), "date");
    }

    // ---- Delete protection ----

    [IntegrationFact]
    public async Task An_employee_with_attendance_records_cannot_be_deleted()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);
        Succeeded(await Punch(employee.Id, "CheckIn"));
        var current = Succeeded(await Get<EmployeeDto>(employee.Id));

        var result = await Delete<EmployeeDto>(employee.Id, Requests.Version(current.RowVersion));

        Assert.Contains("attendance record(s)", Failed(result, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    // ---- Views ----

    [IntegrationFact]
    public async Task The_overview_and_schedule_are_built_from_the_stored_records()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var mia = await SeedWorkerAsync(org, shift.Id);
        await SeedWorkerAsync(org, shift.Id, "2", "ben");
        Succeeded(await Punch(mia.Id, "CheckIn"));

        var overview = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IAttendanceOverviewHandler>().HandleAsync(Monday, org.Branch.Id, default)));
        var week = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IWorkScheduleHandler>().HandleAsync(new WorkScheduleRequest(Monday, null, org.Branch.Id), default)));

        Assert.Equal(2, overview.Summary.Scheduled);
        Assert.Equal(1, overview.Summary.Late);
        Assert.Equal(1, overview.Summary.Absent);
        Assert.Equal(org.Department.Id, Assert.Single(overview.ByDepartment).DepartmentId);
        Assert.Equal("Checked in 14:30 · 30 min late", Assert.Single(overview.Late).Detail);
        Assert.Equal(2, week.Rows.Count);
        Assert.Equal("14:00–22:00", week.Rows[0].Cells[0].Label);
        Assert.Equal("Off", week.Rows[0].Cells[5].Kind); // Saturday
    }

    [IntegrationFact]
    public async Task Records_are_listed_and_filtered_in_pages()
    {
        var org = await SeedOrganizationAsync();
        var shift = await SeedShiftAsync();
        var employee = await SeedWorkerAsync(org, shift.Id);
        var record = Succeeded(await Punch(employee.Id, "CheckIn"));

        var page = Succeeded(await Query<AttendanceListItemDto, AttendanceFilter>(
            new PageRequest<AttendanceFilter>(0, 10, new SortState("date", "desc"), employee.EmployeeCode, new AttendanceFilter(BranchId: org.Branch.Id, LateOnly: true))));

        var item = Assert.Single(page.Items);
        Assert.Equal(record.Id, item.Id);
        Assert.Equal(org.Department.Name, item.DepartmentName);
        Assert.Equal(shift.Name, item.ShiftName);
        Assert.Empty(Succeeded(await Query<AttendanceListItemDto, AttendanceFilter>(
            new PageRequest<AttendanceFilter>(0, 10, null, null, new AttendanceFilter(EmployeeId: employee.Id, OvertimeOnly: true)))).Items);
    }
}
