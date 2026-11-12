using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Attendance.Adjustments;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Employees.Documents;
using Apex.Hrms.Application.Employees.Salary;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Tests.Security;

/// <summary>
/// The tiers of the row-level scoping: HR staff (Approve and employees:Edit) see everything of leave and attendance, a manager
/// (Approve only) sees themselves and their direct reports, anyone else sees only themselves; the employees module shows
/// everyone to HR and payroll staff and the caller's team to the rest; salary figures need payroll:Approve for anyone but the
/// employee. The cast: Maya manages Ria and Rex; Nora is a colleague who does not report to Maya. Today is Monday 2026-11-09.
/// </summary>
public sealed class TeamAndSalaryScopingTests : IDisposable
{
    private static readonly string[] LeaveManager = ["leave:View", "leave:Approve"];
    private static readonly string[] LeaveHr = ["leave:View", "leave:Approve", "employees:Edit"];
    private static readonly string[] AttendanceManager = ["attendance:View", "attendance:Approve"];
    private static readonly string[] AttendanceHr = ["attendance:View", "attendance:Approve", "employees:Edit"];

    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private sealed record Cast(EmployeeDto Maya, EmployeeDto Ria, EmployeeDto Rex, EmployeeDto Nora);

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static EmployeeDto[] Reports(Cast cast) => [cast.Ria, cast.Rex];

    private async Task<Cast> SeedCastAsync()
    {
        var org = await host.SeedOrgAsync();
        return new Cast(
            await host.CreateLeaveEmployeeAsync(org, "maya@example.test"),
            await host.CreateLeaveEmployeeAsync(org, "ria@example.test"),
            await host.CreateLeaveEmployeeAsync(org, "rex@example.test"),
            await host.CreateLeaveEmployeeAsync(org, "nora@example.test"));
    }

    private void SignInAs(EmployeeDto caller, EmployeeDto[] reports, params string[] grants)
    {
        host.User.EmployeeId = caller.Id;
        host.Team.Set([.. reports.Select(report => report.Id)]);
        host.Permissions.Only(grants);
    }

    // ---- Leave ----

    private async Task SeedLeaveOfEveryoneAsync(Cast cast)
    {
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(days: 15m));
        var day = 16;
        foreach (var employee in new[] { cast.Maya, cast.Ria, cast.Rex, cast.Nora })
        {
            await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, day++)));
        }
    }

    private async Task<HashSet<int>> LeaveOwnersAsync() =>
        [.. ResultAssert.Succeeded(await host.Get<IQueryHandler<LeaveApplicationDto, LeaveApplicationFilter>>()
            .HandleAsync(new PageRequest<LeaveApplicationFilter>(0, 50, null, null, new LeaveApplicationFilter()), default))
            .Items.Select(item => item.EmployeeId)];

    [Fact]
    public async Task Leave_hr_staff_see_everyone_a_manager_sees_the_team_and_an_employee_only_themself()
    {
        var cast = await SeedCastAsync();
        await SeedLeaveOfEveryoneAsync(cast);

        SignInAs(cast.Maya, Reports(cast), LeaveHr);
        Assert.Equal([cast.Maya.Id, cast.Ria.Id, cast.Rex.Id, cast.Nora.Id], await LeaveOwnersAsync());

        SignInAs(cast.Maya, Reports(cast), LeaveManager);
        Assert.Equal([cast.Maya.Id, cast.Ria.Id, cast.Rex.Id], await LeaveOwnersAsync());

        SignInAs(cast.Maya, Reports(cast), "leave:View");
        Assert.Equal([cast.Maya.Id], await LeaveOwnersAsync());
    }

    [Fact]
    public async Task A_manager_with_no_reports_sees_only_their_own_leave()
    {
        var cast = await SeedCastAsync();
        await SeedLeaveOfEveryoneAsync(cast);

        SignInAs(cast.Nora, [], LeaveManager);

        Assert.Equal([cast.Nora.Id], await LeaveOwnersAsync());
    }

    [Fact]
    public async Task A_manager_approves_and_rejects_a_direct_reports_request_but_a_non_reports_request_is_not_found()
    {
        var cast = await SeedCastAsync();
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(days: 15m));
        var report = await host.SubmittedAsync(LeaveFixtures.NewApplication(cast.Ria.Id, type.Id, D(11, 16)));
        var other = await host.SubmittedAsync(LeaveFixtures.NewApplication(cast.Rex.Id, type.Id, D(11, 17)));
        var stranger = await host.SubmittedAsync(LeaveFixtures.NewApplication(cast.Nora.Id, type.Id, D(11, 18)));
        SignInAs(cast.Maya, Reports(cast), LeaveManager);

        Assert.Equal("Approved", ResultAssert.Succeeded(await host.Approve(report)).Status);
        Assert.Equal("Rejected", ResultAssert.Succeeded(await host.Reject(other)).Status);
        ResultAssert.Failed(await host.Approve(stranger), ErrorType.NotFound);
        ResultAssert.Failed(await host.Reject(stranger), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_manager_cannot_approve_their_own_request_but_hr_staff_still_approve_anyones()
    {
        var cast = await SeedCastAsync();
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(days: 15m));
        var own = await host.SubmittedAsync(LeaveFixtures.NewApplication(cast.Maya.Id, type.Id, D(11, 16)));
        var stranger = await host.SubmittedAsync(LeaveFixtures.NewApplication(cast.Nora.Id, type.Id, D(11, 17)));

        SignInAs(cast.Maya, Reports(cast), LeaveManager);
        Assert.Equal("You cannot decide your own leave request.", ResultAssert.Failed(await host.Approve(own), ErrorType.Conflict).Message);

        SignInAs(cast.Maya, Reports(cast), LeaveHr);
        Assert.Equal("Approved", ResultAssert.Succeeded(await host.Approve(stranger)).Status);
    }

    [Fact]
    public async Task A_manager_files_leave_for_a_direct_report_but_not_for_a_non_report()
    {
        var cast = await SeedCastAsync();
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(days: 15m));
        SignInAs(cast.Maya, Reports(cast), "leave:View", "leave:Create", "leave:Approve");

        ResultAssert.Succeeded(await host.CreateApplication(LeaveFixtures.NewApplication(cast.Ria.Id, type.Id, D(11, 16))));
        ResultAssert.Succeeded(await host.CreateApplication(LeaveFixtures.NewApplication(cast.Maya.Id, type.Id, D(11, 17))));
        ResultAssert.Failed(await host.CreateApplication(LeaveFixtures.NewApplication(cast.Nora.Id, type.Id, D(11, 18))), ErrorType.Forbidden);
    }

    [Fact]
    public async Task Leave_balances_and_calendar_follow_the_tier()
    {
        var cast = await SeedCastAsync();
        await SeedLeaveOfEveryoneAsync(cast);
        var week = new LeaveCalendarRequest(D(11, 16), D(11, 19), IncludePending: true);
        SignInAs(cast.Maya, Reports(cast), LeaveManager);

        ResultAssert.Succeeded(await host.Balances(cast.Ria.Id));
        ResultAssert.Succeeded(await host.Balances(cast.Maya.Id));
        ResultAssert.Failed(await host.Balances(cast.Nora.Id), ErrorType.NotFound);
        var team = ResultAssert.Succeeded(await host.Calendar(week));
        Assert.Equal(
            [cast.Maya.Id, cast.Ria.Id, cast.Rex.Id],
            team.Days.SelectMany(day => day.Entries).Select(entry => entry.EmployeeId).ToHashSet());

        SignInAs(cast.Maya, Reports(cast), LeaveHr);
        ResultAssert.Succeeded(await host.Balances(cast.Nora.Id));
        var all = ResultAssert.Succeeded(await host.Calendar(week));
        Assert.Equal(4, all.Days.SelectMany(day => day.Entries).Select(entry => entry.EmployeeId).ToHashSet().Count);

        SignInAs(cast.Maya, Reports(cast), "leave:View");
        ResultAssert.Failed(await host.Balances(cast.Ria.Id), ErrorType.NotFound);
    }

    // ---- Attendance ----

    private async Task<Cast> SeedAttendanceCastAsync()
    {
        var org = await host.SeedOrgAsync();
        await host.CreateShiftAsync();
        return new Cast(
            await host.CreateWorkerAsync(org, "maya@example.test"),
            await host.CreateWorkerAsync(org, "ria@example.test"),
            await host.CreateWorkerAsync(org, "rex@example.test"),
            await host.CreateWorkerAsync(org, "nora@example.test"));
    }

    private async Task<HashSet<int>> RecordOwnersAsync() =>
        [.. ResultAssert.Succeeded(await host.Get<IQueryHandler<AttendanceListItemDto, AttendanceFilter>>()
            .HandleAsync(new PageRequest<AttendanceFilter>(0, 50, null, null, new AttendanceFilter()), default))
            .Items.Select(item => item.EmployeeId)];

    [Fact]
    public async Task Attendance_records_follow_the_three_tiers()
    {
        var cast = await SeedAttendanceCastAsync();
        foreach (var employee in new[] { cast.Maya, cast.Ria, cast.Rex, cast.Nora })
        {
            await host.PunchAsync(employee.Id, "CheckIn", 9, 0);
        }

        SignInAs(cast.Maya, Reports(cast), AttendanceHr);
        Assert.Equal(4, (await RecordOwnersAsync()).Count);

        SignInAs(cast.Maya, Reports(cast), AttendanceManager);
        Assert.Equal([cast.Maya.Id, cast.Ria.Id, cast.Rex.Id], await RecordOwnersAsync());

        SignInAs(cast.Maya, Reports(cast), "attendance:View", "attendance:Create");
        Assert.Equal([cast.Maya.Id], await RecordOwnersAsync());
    }

    [Fact]
    public async Task A_manager_decides_adjustments_and_overtime_of_direct_reports_only_and_never_their_own()
    {
        var cast = await SeedAttendanceCastAsync();
        foreach (var employee in new[] { cast.Maya, cast.Ria, cast.Nora })
        {
            await host.PunchAsync(employee.Id, "CheckIn", 9, 40);
        }

        var reportAdjustment = await host.CreateAdjustmentAsync(cast.Ria.Id, AttendanceFixtures.At(9, 0));
        var strangerAdjustment = await host.CreateAdjustmentAsync(cast.Nora.Id, AttendanceFixtures.At(9, 0));
        var ownAdjustment = await host.CreateAdjustmentAsync(cast.Maya.Id, AttendanceFixtures.At(9, 0));
        var reportOvertime = await host.CreateOvertimeAsync(cast.Ria.Id, AttendanceFixtures.At(18, 0), AttendanceFixtures.At(20, 0));
        var strangerOvertime = await host.CreateOvertimeAsync(cast.Nora.Id, AttendanceFixtures.At(18, 0), AttendanceFixtures.At(20, 0));
        SignInAs(cast.Maya, Reports(cast), AttendanceManager);

        Assert.Equal("Approved", ResultAssert.Succeeded(await host.ApproveAdjustment(reportAdjustment)).Status);
        Assert.Equal("Approved", ResultAssert.Succeeded(await host.ApproveOvertime(reportOvertime)).Status);
        ResultAssert.Failed(await host.ApproveAdjustment(strangerAdjustment), ErrorType.NotFound);
        ResultAssert.Failed(await host.RejectOvertime(strangerOvertime), ErrorType.NotFound);
        Assert.Equal("You cannot decide your own request.", ResultAssert.Failed(await host.ApproveAdjustment(ownAdjustment), ErrorType.Conflict).Message);

        var adjustments = ResultAssert.Succeeded(await host.Get<IQueryHandler<AdjustmentDto, AdjustmentFilter>>()
            .HandleAsync(new PageRequest<AdjustmentFilter>(0, 50, null, null, new AdjustmentFilter()), default));
        var overtime = ResultAssert.Succeeded(await host.Get<IQueryHandler<OvertimeDto, OvertimeFilter>>()
            .HandleAsync(new PageRequest<OvertimeFilter>(0, 50, null, null, new OvertimeFilter()), default));
        Assert.Equal([cast.Maya.Id, cast.Ria.Id], adjustments.Items.Select(item => item.EmployeeId).ToHashSet());
        Assert.Equal([cast.Ria.Id], overtime.Items.Select(item => item.EmployeeId).ToHashSet());

        SignInAs(cast.Maya, Reports(cast), AttendanceHr);
        Assert.Equal("Approved", ResultAssert.Succeeded(await host.ApproveAdjustment(strangerAdjustment)).Status);
    }

    [Fact]
    public async Task A_manager_files_an_adjustment_for_a_direct_report_but_not_for_a_non_report()
    {
        var cast = await SeedAttendanceCastAsync();
        await host.PunchAsync(cast.Ria.Id, "CheckIn", 9, 40);
        await host.PunchAsync(cast.Nora.Id, "CheckIn", 9, 40);
        SignInAs(cast.Maya, Reports(cast), "attendance:View", "attendance:Create", "attendance:Approve");

        ResultAssert.Succeeded(await host.CreateAdjustment(cast.Ria.Id, checkIn: AttendanceFixtures.At(9, 0)));
        ResultAssert.Failed(await host.CreateAdjustment(cast.Nora.Id, checkIn: AttendanceFixtures.At(9, 0)), ErrorType.Forbidden);
    }

    [Fact]
    public async Task The_attendance_overview_and_the_work_schedule_follow_the_tiers()
    {
        var cast = await SeedAttendanceCastAsync();
        var overview = host.Get<IAttendanceOverviewHandler>();
        var schedule = host.Get<IWorkScheduleHandler>();
        var request = new WorkScheduleRequest(AttendanceFixtures.Monday);

        SignInAs(cast.Maya, Reports(cast), AttendanceHr);
        Assert.Equal(4, ResultAssert.Succeeded(await overview.HandleAsync(AttendanceFixtures.Monday, null, default)).Summary.Scheduled);
        Assert.Equal(4, ResultAssert.Succeeded(await schedule.HandleAsync(request, default)).Rows.Count);

        SignInAs(cast.Maya, Reports(cast), AttendanceManager);
        Assert.Equal(3, ResultAssert.Succeeded(await overview.HandleAsync(AttendanceFixtures.Monday, null, default)).Summary.Scheduled);
        Assert.Equal(3, ResultAssert.Succeeded(await schedule.HandleAsync(request, default)).Rows.Count);

        SignInAs(cast.Maya, Reports(cast), "attendance:View");
        Assert.Equal(1, ResultAssert.Succeeded(await overview.HandleAsync(AttendanceFixtures.Monday, null, default)).Summary.Scheduled);
        Assert.Single(ResultAssert.Succeeded(await schedule.HandleAsync(request, default)).Rows);
    }

    // ---- Employees ----

    private async Task<HashSet<int>> VisibleEmployeesAsync() =>
        [.. ResultAssert.Succeeded(await host.Get<IQueryHandler<EmployeeListItemDto, EmployeeFilter>>()
            .HandleAsync(new PageRequest<EmployeeFilter>(0, 50, null, null, new EmployeeFilter()), default))
            .Items.Select(item => item.Id)];

    [Theory]
    [InlineData("employees:Edit")]
    [InlineData("payroll:View")]
    public async Task Holders_of_employees_edit_or_payroll_view_list_every_employee(string grant)
    {
        var cast = await SeedCastAsync();
        SignInAs(cast.Maya, Reports(cast), "employees:View", grant);

        Assert.Equal(4, (await VisibleEmployeesAsync()).Count);
        ResultAssert.Succeeded(await host.GetEmployee(cast.Nora.Id));
    }

    [Fact]
    public async Task A_manager_with_only_employees_view_lists_themself_and_their_direct_reports()
    {
        var cast = await SeedCastAsync();
        SignInAs(cast.Maya, Reports(cast), "employees:View", "leave:Approve");

        Assert.Equal([cast.Maya.Id, cast.Ria.Id, cast.Rex.Id], await VisibleEmployeesAsync());
        ResultAssert.Succeeded(await host.GetEmployee(cast.Ria.Id));
        ResultAssert.Failed(await host.GetEmployee(cast.Nora.Id), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_caller_with_employees_view_and_no_team_sees_only_themself_and_one_without_an_employee_sees_nobody()
    {
        var cast = await SeedCastAsync();
        SignInAs(cast.Nora, [], "employees:View");
        Assert.Equal([cast.Nora.Id], await VisibleEmployeesAsync());

        host.User.EmployeeId = null;
        Assert.Empty(await VisibleEmployeesAsync());
    }

    [Fact]
    public async Task The_employee_scope_that_the_dashboard_and_the_export_use_follows_the_same_rule()
    {
        var cast = await SeedCastAsync();
        var scope = host.Get<IAccessScope<Employee>>();
        var employees = host.Repository<Employee>();

        async Task<HashSet<int>> Visible() =>
            [.. (await employees.ListAsync(scope.PredicateFor(host.User), default)).Select(employee => employee.Id)];

        SignInAs(cast.Maya, Reports(cast), "dashboard:View");
        Assert.Equal([cast.Maya.Id, cast.Ria.Id, cast.Rex.Id], await Visible());

        SignInAs(cast.Maya, Reports(cast), "dashboard:View", "payroll:View");
        Assert.Equal(4, (await Visible()).Count);
    }

    [Fact]
    public async Task Only_a_holder_of_employees_edit_reaches_a_non_report_to_change_it()
    {
        var cast = await SeedCastAsync();
        var update = host.Get<IUpdateHandler<EmployeeUpdateRequest, EmployeeDto>>();

        SignInAs(cast.Maya, Reports(cast), "employees:View", "employees:Edit");
        ResultAssert.Succeeded(await update.HandleAsync(cast.Nora.Id, EmployeeFixtures.UpdateOf(cast.Nora), default));

        SignInAs(cast.Maya, Reports(cast), "employees:View");
        ResultAssert.Failed(await update.HandleAsync(cast.Nora.Id, EmployeeFixtures.UpdateOf(cast.Nora), default), ErrorType.NotFound);
    }

    [Fact]
    public async Task Employee_documents_follow_the_employee_scope()
    {
        var cast = await SeedCastAsync();
        var create = host.Get<ICreateHandler<EmployeeDocumentCreateRequest, EmployeeDocumentDto>>();
        foreach (var employee in new[] { cast.Maya, cast.Ria, cast.Nora })
        {
            ResultAssert.Succeeded(await create.HandleAsync(
                new EmployeeDocumentCreateRequest(employee.Id, "Work Permit", "Work permit", "permit.pdf", null, null), default));
        }

        var documents = host.Get<IQueryHandler<EmployeeDocumentDto, EmployeeDocumentFilter>>();
        var everything = new PageRequest<EmployeeDocumentFilter>(0, 50, null, null, new EmployeeDocumentFilter());

        SignInAs(cast.Maya, Reports(cast), "employees:View");
        var team = ResultAssert.Succeeded(await documents.HandleAsync(everything, default));
        Assert.Equal([cast.Maya.Id, cast.Ria.Id], team.Items.Select(item => item.EmployeeId).ToHashSet());

        SignInAs(cast.Maya, Reports(cast), "employees:View", "employees:Edit");
        Assert.Equal(3, ResultAssert.Succeeded(await documents.HandleAsync(everything, default)).Total);
    }

    // ---- Salary figures ----

    private async Task<(PayrollSeed Seed, EmployeeDto Second)> SeedPayslipsAsync()
    {
        var seed = await host.SeedPayrollAsync();
        var second = await host.CreatePayrollEmployeeAsync(seed.Org, "bo.test@example.test");
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(second.Id, seed.Structure.Id, 2000m));
        await host.ProcessedAsync(await host.CreateRunAsync());
        return (seed, second);
    }

    private async Task<HashSet<int>> PayslipOwnersAsync() =>
        [.. ResultAssert.Succeeded(await host.Payslips()).Items.Select(item => item.EmployeeId)];

    private async Task<HashSet<int>> AssignmentOwnersAsync() =>
        [.. ResultAssert.Succeeded(await host.Get<IQueryHandler<SalaryAssignmentDto, SalaryAssignmentFilter>>()
            .HandleAsync(new PageRequest<SalaryAssignmentFilter>(0, 50, null, null, new SalaryAssignmentFilter()), default))
            .Items.Select(item => item.EmployeeId)];

    private Task<Result<EmployeeSalarySummaryDto>> Summary(int employeeId) =>
        host.Get<IGetByIdHandler<EmployeeSalarySummaryDto>>().HandleAsync(employeeId, default);

    [Fact]
    public async Task Payroll_approve_sees_every_payslip_assignment_and_salary_summary()
    {
        var (seed, second) = await SeedPayslipsAsync();
        SignInAs(seed.Employee, [], "payroll:View", "payroll:Approve");

        Assert.Equal([seed.Employee.Id, second.Id], await PayslipOwnersAsync());
        Assert.Equal([seed.Employee.Id, second.Id], await AssignmentOwnersAsync());
        ResultAssert.Succeeded(await Summary(second.Id));
    }

    [Fact]
    public async Task Payroll_view_without_approve_sees_only_the_callers_own_payslip_assignment_and_salary_summary()
    {
        var (seed, second) = await SeedPayslipsAsync();
        var theirs = ResultAssert.Succeeded(await host.Payslips()).Items.Single(item => item.EmployeeId == second.Id);
        SignInAs(seed.Employee, [], "payroll:View", "employees:View", "employees:Edit", "leave:Approve");

        Assert.Equal([seed.Employee.Id], await PayslipOwnersAsync());
        Assert.Equal([seed.Employee.Id], await AssignmentOwnersAsync());
        ResultAssert.Failed(await host.Get<IGetByIdHandler<PayslipDto>>().HandleAsync(theirs.Id, default), ErrorType.NotFound);
        ResultAssert.Succeeded(await Summary(seed.Employee.Id));
        ResultAssert.Failed(await Summary(second.Id), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_payroll_caller_without_a_linked_employee_and_without_approve_sees_no_salary_data()
    {
        await SeedPayslipsAsync();
        host.User.EmployeeId = null;
        host.Permissions.Only("payroll:View", "employees:View");

        Assert.Empty(await PayslipOwnersAsync());
        Assert.Empty(await AssignmentOwnersAsync());
    }

    [Fact]
    public async Task Processing_a_run_is_not_narrowed_by_the_scope_of_the_caller()
    {
        var seed = await host.SeedPayrollAsync();
        var second = await host.CreatePayrollEmployeeAsync(seed.Org, "bo.test@example.test");
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(second.Id, seed.Structure.Id, 2000m));
        var run = await host.CreateRunAsync();
        SignInAs(seed.Employee, [], "payroll:View", "payroll:Create", "payroll:Edit");

        var processed = ResultAssert.Succeeded(await host.Process(run));

        Assert.Equal(2, processed.EmployeeCount);
    }
}
