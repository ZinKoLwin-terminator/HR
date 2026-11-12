using Apex.Hrms.Application.Attendance.Adjustments;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Security;

/// <summary>
/// Security re-review M1: a caller without the module's Approve permission sees and acts only for their own employee; a
/// caller with it (HR, managers, administrators) keeps the full view. Today is Monday 2026-11-09 in the fake clock.
/// </summary>
public sealed class OwnRecordScopingTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private async Task<(EmployeeDto Mia, EmployeeDto Noah, LeaveTypeDto Type)> SeedLeaveAsync()
    {
        var org = await host.SeedOrgAsync();
        var mia = await host.CreateLeaveEmployeeAsync(org, "mia@example.test");
        var noah = await host.CreateLeaveEmployeeAsync(org, "noah@example.test");
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(days: 15m));
        return (mia, noah, type);
    }

    private void SignInAsEmployee(EmployeeDto employee, params string[] grants)
    {
        host.User.EmployeeId = employee.Id;
        host.Permissions.Only(grants);
    }

    private Task<Result<PagedResult<LeaveApplicationDto>>> ListLeave() =>
        host.Get<IQueryHandler<LeaveApplicationDto, LeaveApplicationFilter>>()
            .HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 50, null, null, new LeaveApplicationFilter()), default);

    // ---- Leave ----

    [Fact]
    public async Task An_employee_lists_only_their_own_leave_applications()
    {
        var (mia, noah, type) = await SeedLeaveAsync();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(mia.Id, type.Id, D(11, 16)));
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(noah.Id, type.Id, D(11, 17)));
        SignInAsEmployee(mia, "leave:View", "leave:Create");

        var page = ResultAssert.Succeeded(await ListLeave());

        Assert.Equal(mia.Id, Assert.Single(page.Items).EmployeeId);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task An_approver_lists_every_leave_application()
    {
        var (mia, noah, type) = await SeedLeaveAsync();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(mia.Id, type.Id, D(11, 16)));
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(noah.Id, type.Id, D(11, 17)));
        SignInAsEmployee(mia, "leave:View", "leave:Approve", "employees:Edit");

        Assert.Equal(2, ResultAssert.Succeeded(await ListLeave()).Total);
    }

    [Fact]
    public async Task A_caller_without_a_linked_employee_and_without_approve_sees_no_leave()
    {
        var (mia, _, type) = await SeedLeaveAsync();
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(mia.Id, type.Id, D(11, 16)));
        host.User.EmployeeId = null;
        host.Permissions.Only("leave:View");

        Assert.Equal(0, ResultAssert.Succeeded(await ListLeave()).Total);
    }

    [Fact]
    public async Task Another_employees_leave_application_is_not_found_and_cannot_be_cancelled()
    {
        var (mia, noah, type) = await SeedLeaveAsync();
        var theirs = await host.SubmittedAsync(LeaveFixtures.NewApplication(noah.Id, type.Id, D(11, 17)));
        SignInAsEmployee(mia, "leave:View", "leave:Create");

        ResultAssert.Failed(await host.GetApplication(theirs.Id), ErrorType.NotFound);
        ResultAssert.Failed(await host.Cancel(theirs), ErrorType.NotFound);
    }

    [Fact]
    public async Task An_employee_cannot_create_or_move_leave_for_a_colleague_but_can_for_themself()
    {
        var (mia, noah, type) = await SeedLeaveAsync();
        SignInAsEmployee(mia, "leave:View", "leave:Create", "leave:Edit");

        var forColleague = await host.CreateApplication(LeaveFixtures.NewApplication(noah.Id, type.Id, D(11, 17)));
        var own = await host.CreateApplicationAsync(LeaveFixtures.NewApplication(mia.Id, type.Id, D(11, 18)));
        var moved = await host.Get<IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>>()
            .HandleAsync(own.Id, LeaveFixtures.ApplicationUpdateOf(own) with { EmployeeId = noah.Id }, default);

        ResultAssert.Failed(forColleague, ErrorType.Forbidden);
        ResultAssert.Failed(moved, ErrorType.Forbidden);
        Assert.Equal(mia.Id, own.EmployeeId);
    }

    [Fact]
    public async Task An_approver_can_create_leave_for_another_employee()
    {
        var (mia, noah, type) = await SeedLeaveAsync();
        SignInAsEmployee(mia, "leave:View", "leave:Create", "leave:Approve", "employees:Edit");

        ResultAssert.Succeeded(await host.CreateApplication(LeaveFixtures.NewApplication(noah.Id, type.Id, D(11, 17))));
    }

    [Fact]
    public async Task An_employee_reads_only_their_own_balance_and_a_colleagues_looks_like_a_missing_employee()
    {
        var (mia, noah, _) = await SeedLeaveAsync();
        SignInAsEmployee(mia, "leave:View");

        ResultAssert.Succeeded(await host.Balances(mia.Id));
        ResultAssert.Failed(await host.Balances(noah.Id), ErrorType.NotFound);
    }

    [Fact]
    public async Task The_leave_calendar_of_an_employee_shows_only_their_own_leave()
    {
        var (mia, noah, type) = await SeedLeaveAsync();
        await host.SubmittedAsync(LeaveFixtures.NewApplication(mia.Id, type.Id, D(11, 16)));
        await host.SubmittedAsync(LeaveFixtures.NewApplication(noah.Id, type.Id, D(11, 16)));
        SignInAsEmployee(mia, "leave:View");

        var calendar = ResultAssert.Succeeded(await host.Calendar(new LeaveCalendarRequest(D(11, 16), D(11, 16))));

        Assert.Equal(mia.Id, Assert.Single(calendar.Days.Single().Entries).EmployeeId);
    }

    // ---- Attendance ----

    private async Task<(EmployeeDto Mia, EmployeeDto Noah)> SeedAttendanceAsync()
    {
        var org = await host.SeedOrgAsync();
        await host.CreateShiftAsync();
        var mia = await host.CreateWorkerAsync(org, "mia@example.test");
        var noah = await host.CreateWorkerAsync(org, "noah@example.test");
        return (mia, noah);
    }

    private Task<Result<PagedResult<AttendanceListItemDto>>> ListRecords() =>
        host.Get<IQueryHandler<AttendanceListItemDto, AttendanceFilter>>()
            .HandleAsync(new PageRequest<AttendanceFilter>(0, 50, null, null, new AttendanceFilter()), default);

    [Fact]
    public async Task An_employee_with_only_view_and_create_punches_for_themself_but_not_for_a_colleague()
    {
        var (mia, noah) = await SeedAttendanceAsync();
        SignInAsEmployee(mia, "attendance:View", "attendance:Create");
        host.Clock.TimeNow = AttendanceFixtures.At(9, 0);

        ResultAssert.Succeeded(await host.Punch(mia.Id, "CheckIn"));
        ResultAssert.Failed(await host.Punch(noah.Id, "CheckIn"), ErrorType.Forbidden);
    }

    [Fact]
    public async Task An_employee_lists_only_their_own_attendance_records()
    {
        var (mia, noah) = await SeedAttendanceAsync();
        await host.PunchAsync(mia.Id, "CheckIn", 9, 0);
        await host.PunchAsync(noah.Id, "CheckIn", 9, 5);
        SignInAsEmployee(mia, "attendance:View", "attendance:Create");

        var page = ResultAssert.Succeeded(await ListRecords());

        Assert.Equal(mia.Id, Assert.Single(page.Items).EmployeeId);
    }

    [Fact]
    public async Task An_attendance_approver_lists_every_record()
    {
        var (mia, noah) = await SeedAttendanceAsync();
        await host.PunchAsync(mia.Id, "CheckIn", 9, 0);
        await host.PunchAsync(noah.Id, "CheckIn", 9, 5);
        SignInAsEmployee(mia, "attendance:View", "attendance:Approve", "employees:Edit");

        Assert.Equal(2, ResultAssert.Succeeded(await ListRecords()).Total);
    }

    [Fact]
    public async Task An_employee_cannot_file_an_adjustment_or_overtime_for_a_colleague_and_sees_only_their_own()
    {
        var (mia, noah) = await SeedAttendanceAsync();
        await host.PunchAsync(mia.Id, "CheckIn", 9, 40);
        await host.PunchAsync(noah.Id, "CheckIn", 9, 40);
        var theirs = await host.CreateAdjustmentAsync(noah.Id, AttendanceFixtures.At(9, 0));
        var theirOvertime = await host.CreateOvertimeAsync(noah.Id, AttendanceFixtures.At(18, 0), AttendanceFixtures.At(20, 0));
        SignInAsEmployee(mia, "attendance:View", "attendance:Create", "attendance:Delete");

        ResultAssert.Failed(await host.CreateAdjustment(noah.Id, checkIn: AttendanceFixtures.At(9, 0)), ErrorType.Forbidden);
        ResultAssert.Failed(await host.CreateOvertime(noah.Id, AttendanceFixtures.At(18, 0), AttendanceFixtures.At(20, 0), D(11, 6)), ErrorType.Forbidden);
        var own = await host.CreateAdjustmentAsync(mia.Id, AttendanceFixtures.At(9, 0));
        var ownOvertime = await host.CreateOvertimeAsync(mia.Id, AttendanceFixtures.At(18, 0), AttendanceFixtures.At(20, 0));

        var adjustments = ResultAssert.Succeeded(await host.Get<IQueryHandler<AdjustmentDto, AdjustmentFilter>>()
            .HandleAsync(new PageRequest<AdjustmentFilter>(0, 50, null, null, new AdjustmentFilter()), default));
        var overtime = ResultAssert.Succeeded(await host.Get<IQueryHandler<OvertimeDto, OvertimeFilter>>()
            .HandleAsync(new PageRequest<OvertimeFilter>(0, 50, null, null, new OvertimeFilter()), default));
        Assert.Equal(own.Id, Assert.Single(adjustments.Items).Id);
        Assert.Equal(ownOvertime.Id, Assert.Single(overtime.Items).Id);

        // A colleague's pending request is outside the scope (not found); the employee's own can be deleted.
        ResultAssert.Failed(
            await host.Get<IDeleteHandler<AdjustmentDto>>().HandleAsync(theirs.Id, Requests.Version(theirs.RowVersion), default),
            ErrorType.NotFound);
        Assert.True((await host.Get<IDeleteHandler<OvertimeDto>>().HandleAsync(theirOvertime.Id, Requests.Version(theirOvertime.RowVersion), default)).IsFailure);
        Assert.True((await host.Get<IDeleteHandler<OvertimeDto>>().HandleAsync(ownOvertime.Id, Requests.Version(ownOvertime.RowVersion), default)).IsSuccess);
    }

    [Fact]
    public async Task The_attendance_overview_of_an_employee_counts_only_themself()
    {
        var (mia, _) = await SeedAttendanceAsync();
        SignInAsEmployee(mia, "attendance:View");

        var overview = ResultAssert.Succeeded(await host.Get<IAttendanceOverviewHandler>().HandleAsync(AttendanceFixtures.Monday, null, default));

        Assert.Equal(1, overview.Summary.Scheduled);
    }
}
