using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Team and salary scoping over HTTP against the test database. A manager with two direct reports and one colleague who is not a
/// report, an HR Officer, a payroll caller with only View and one with Approve. The roles carry the grants of the seed roles
/// of the same kind; the accounts, roles and employees carry the TST tag and are removed with it.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class TeamAndSalaryScopingHttpTests : PayrollTestBase, IDisposable
{
    private static readonly string[] ManagerGrants =
        ["dashboard:View", "employees:View", "leave:View", "leave:Approve", "attendance:View", "attendance:Approve"];

    private static readonly string[] HrOfficerGrants =
    [
        "dashboard:View", "employees:View", "employees:Create", "employees:Edit", "leave:View", "leave:Create", "leave:Edit", "leave:Approve",
        "attendance:View", "attendance:Create", "attendance:Edit", "attendance:Approve", "payroll:View",
    ];

    private static readonly string[] PayrollViewGrants = ["dashboard:View", "employees:View", "payroll:View"];

    private static readonly string[] PayrollApproveGrants =
        ["dashboard:View", "employees:View", "payroll:View", "payroll:Create", "payroll:Edit", "payroll:Approve"];

    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];

    public TeamAndSalaryScopingHttpTests() => factory.EnsurePointsAtTestDatabase();

    protected override void Cleanup()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }

        clients.Clear();
        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private async Task<HttpClient> SignedInAsync(TestAccount account)
    {
        var client = factory.CreateBrowserClient(out _);
        clients.Add(client);
        await client.SignInAsync(account);
        return client;
    }

    private async Task<HttpClient> SignedInAsRoleAsync(string suffix, int? employeeId, params string[] grants)
    {
        var role = $"{Tag}-{suffix}";
        var roleId = await Sql.ScalarAsync<int>("INSERT INTO sec.Role (Name, IsSystem, Status) OUTPUT INSERTED.Id VALUES (@name, 0, 'Active');", ("@name", role));
        foreach (var grant in grants.OrderBy(grant => grant.EndsWith(":View", StringComparison.Ordinal) ? 0 : 1))
        {
            var parts = grant.Split(':');
            await Sql.ExecuteAsync(
                "INSERT INTO sec.RolePermission (RoleId, ModuleKey, Action) VALUES (@role, @module, @action);",
                ("@role", roleId),
                ("@module", parts[0]),
                ("@action", parts[1]));
        }

        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, $"{suffix}-login"), roleName: role, employeeId: employeeId);
        return await SignedInAsync(account);
    }

    private static DateOnly Monday(int weeksAhead)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7 * weeksAhead);
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static async Task<HashSet<int>> EmployeeIdsAsync(HttpClient client, string url) =>
        [.. (await ReadAsync(await client.GetAsync(url))).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("employeeId").GetInt32())];

    /// <summary>Maya manages Ria and Rex; Nora is a colleague of the same department who reports to nobody.</summary>
    private async Task<(EmployeeDto Maya, EmployeeDto Ria, EmployeeDto Rex, EmployeeDto Nora)> SeedTeamAsync()
    {
        var org = await SeedOrganizationAsync();
        var maya = await SeedEmployeeAsync(org, "1", "maya");
        Task<EmployeeDto> Report(string suffix, string local) => SeedReportAsync(org, maya.Id, suffix, local);
        var ria = await Report("2", "ria");
        var rex = await Report("3", "rex");
        var nora = await SeedEmployeeAsync(org, "4", "nora");
        return (maya, ria, rex, nora);
    }

    private async Task<EmployeeDto> SeedReportAsync(
        (CompanyDto Company, BranchDto Branch, DepartmentDto Department, JobGradeDto Grade, PositionDto Position) org,
        int managerId,
        string suffix,
        string local) =>
        Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, suffix, local) with { ManagerId = managerId }));

    [IntegrationFact]
    public async Task A_manager_sees_and_decides_leave_of_direct_reports_only_and_hr_sees_everyone()
    {
        var (maya, ria, rex, nora) = await SeedTeamAsync();
        var type = await SeedLeaveTypeAsync();
        var mayaLeave = await SeedSubmittedLeaveAsync(maya.Id, type.Id, Monday(3));
        var riaLeave = await SeedSubmittedLeaveAsync(ria.Id, type.Id, Monday(4));
        var rexLeave = await SeedSubmittedLeaveAsync(rex.Id, type.Id, Monday(5));
        var noraLeave = await SeedSubmittedLeaveAsync(nora.Id, type.Id, Monday(6));
        var manager = await SignedInAsRoleAsync("Manager", maya.Id, ManagerGrants);
        var hr = await SignedInAsRoleAsync("HrOfficer", nora.Id, HrOfficerGrants);

        var team = await EmployeeIdsAsync(manager, "/api/leave-applications/query?pageSize=200");
        var everyone = await EmployeeIdsAsync(hr, "/api/leave-applications/query?pageSize=200");
        var approveReport = await manager.PostAsJsonAsync($"/api/leave-applications/{riaLeave.Id}/approve", new { rowVersion = Requests.Version(riaLeave.RowVersion) });
        var rejectReport = await manager.PostAsJsonAsync($"/api/leave-applications/{rexLeave.Id}/reject", new { rowVersion = Requests.Version(rexLeave.RowVersion), remarks = "Busy period" });
        var approveStranger = await manager.PostAsJsonAsync($"/api/leave-applications/{noraLeave.Id}/approve", new { rowVersion = Requests.Version(noraLeave.RowVersion) });
        var getStranger = await manager.GetAsync($"/api/leave-applications/{noraLeave.Id}");
        var approveOwn = await manager.PostAsJsonAsync($"/api/leave-applications/{mayaLeave.Id}/approve", new { rowVersion = Requests.Version(mayaLeave.RowVersion) });
        var balanceStranger = await manager.GetAsync($"/api/leave/balances?employeeId={nora.Id}");
        var balanceReport = await manager.GetAsync($"/api/leave/balances?employeeId={ria.Id}");
        var filedForStranger = await manager.PostAsJsonAsync("/api/leave-applications", Requests.LeaveApplication(nora.Id, type.Id, Monday(7)));

        Assert.Equal([maya.Id, ria.Id, rex.Id], team.Where(id => id != nora.Id).ToHashSet());
        Assert.DoesNotContain(nora.Id, team);
        Assert.Contains(nora.Id, everyone);
        Assert.Contains(ria.Id, everyone);
        Assert.Equal(HttpStatusCode.OK, approveReport.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejectReport.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, approveStranger.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getStranger.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, approveOwn.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, balanceStranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, balanceReport.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, filedForStranger.StatusCode);

        var hrApproves = await hr.PostAsJsonAsync($"/api/leave-applications/{mayaLeave.Id}/approve", new { rowVersion = Requests.Version(mayaLeave.RowVersion) });
        Assert.Equal(HttpStatusCode.OK, hrApproves.StatusCode);
    }

    [IntegrationFact]
    public async Task The_employees_list_shows_a_manager_the_team_and_hr_and_payroll_staff_everyone()
    {
        var (maya, ria, rex, nora) = await SeedTeamAsync();
        var manager = await SignedInAsRoleAsync("Manager", maya.Id, ManagerGrants);
        var hr = await SignedInAsRoleAsync("HrOfficer", nora.Id, HrOfficerGrants);
        var payroll = await SignedInAsRoleAsync("PayrollView", null, PayrollViewGrants);
        const string Url = "/api/employees/query?pageSize=200";

        async Task<HashSet<int>> Ids(HttpClient client) =>
            [.. (await ReadAsync(await client.GetAsync(Url))).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32())];

        var team = await Ids(manager);
        var all = await Ids(hr);
        var payrollSees = await Ids(payroll);
        var strangerForManager = await manager.GetAsync($"/api/employees/{nora.Id}");
        var reportForManager = await manager.GetAsync($"/api/employees/{ria.Id}");

        Assert.Equal(new HashSet<int> { maya.Id, ria.Id, rex.Id }, team.Where(id => new[] { maya.Id, ria.Id, rex.Id, nora.Id }.Contains(id)).ToHashSet());
        Assert.DoesNotContain(nora.Id, team);
        Assert.Equal(4, new[] { maya.Id, ria.Id, rex.Id, nora.Id }.Count(all.Contains));
        Assert.Equal(4, new[] { maya.Id, ria.Id, rex.Id, nora.Id }.Count(payrollSees.Contains));
        Assert.Equal(HttpStatusCode.NotFound, strangerForManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reportForManager.StatusCode);
    }

    [IntegrationFact]
    public async Task Payroll_view_sees_only_its_own_payslip_assignment_and_salary_summary_and_payroll_approve_sees_all()
    {
        var world = await SeedPayrollAsync("ada", "1");
        var ben = await SeedSecondEmployeeAsync(world);
        var admin = await SignedInAsync(TestAdministrator.Account);
        var created = await admin.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month = 1 });
        var run = (await created.Content.ReadFromJsonAsync<PayrollRunDto>())!;
        var processed = await admin.PostAsJsonAsync($"/api/payroll-runs/{run.Id}/process", new { rowVersion = Requests.Version(run.RowVersion) });
        Assert.Equal(HttpStatusCode.OK, processed.StatusCode);
        var viewer = await SignedInAsRoleAsync("PayrollView", ben.Id, PayrollViewGrants);
        var approver = await SignedInAsRoleAsync("PayrollApprove", ben.Id, PayrollApproveGrants);
        var adaSlipId = (await admin.GetFromJsonAsync<PagedResult<PayslipDto>>($"/api/payslips/query?runId={run.Id}&pageSize=50"))!
            .Items.Single(item => item.EmployeeId == world.Employee.Id).Id;

        var viewerSlips = await EmployeeIdsAsync(viewer, $"/api/payslips/query?runId={run.Id}&pageSize=50");
        var viewerAssignments = await EmployeeIdsAsync(viewer, "/api/salary-assignments/query?pageSize=200");
        var viewerRun = await viewer.GetAsync($"/api/payroll-runs/{run.Id}");
        var viewerSlipOfAda = await viewer.GetAsync($"/api/payslips/{adaSlipId}");
        var viewerSummaryAda = await viewer.GetAsync($"/api/employees/{world.Employee.Id}/salary-summary");
        var viewerSummaryOwn = await viewer.GetAsync($"/api/employees/{ben.Id}/salary-summary");
        var approverSlips = await EmployeeIdsAsync(approver, $"/api/payslips/query?runId={run.Id}&pageSize=50");
        var approverAssignments = await EmployeeIdsAsync(approver, "/api/salary-assignments/query?pageSize=200");
        var approverSlipOfAda = await approver.GetAsync($"/api/payslips/{adaSlipId}");
        var approverSummaryAda = await approver.GetAsync($"/api/employees/{world.Employee.Id}/salary-summary");

        Assert.Equal([ben.Id], viewerSlips);
        Assert.Equal([ben.Id], viewerAssignments.Where(id => id == ben.Id || id == world.Employee.Id).ToHashSet());
        Assert.DoesNotContain(world.Employee.Id, viewerAssignments);
        Assert.Equal(HttpStatusCode.OK, viewerRun.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, viewerSlipOfAda.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, viewerSummaryAda.StatusCode);
        Assert.Equal(HttpStatusCode.OK, viewerSummaryOwn.StatusCode);
        Assert.Equal([world.Employee.Id, ben.Id], approverSlips);
        Assert.Contains(world.Employee.Id, approverAssignments);
        Assert.Contains(ben.Id, approverAssignments);
        Assert.Equal(HttpStatusCode.OK, approverSlipOfAda.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approverSummaryAda.StatusCode);
    }

    [IntegrationFact]
    public async Task An_hr_officer_without_payroll_approve_sees_only_its_own_payslip_and_still_lists_every_employee()
    {
        var world = await SeedPayrollAsync("ada", "1");
        var ben = await SeedSecondEmployeeAsync(world);
        var admin = await SignedInAsync(TestAdministrator.Account);
        var run = (await (await admin.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month = 2 })).Content.ReadFromJsonAsync<PayrollRunDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/payroll-runs/{run.Id}/process", new { rowVersion = Requests.Version(run.RowVersion) })).StatusCode);
        var hr = await SignedInAsRoleAsync("HrOfficer", world.Employee.Id, HrOfficerGrants);

        var slips = await EmployeeIdsAsync(hr, $"/api/payslips/query?runId={run.Id}&pageSize=50");
        var employees = await ReadAsync(await hr.GetAsync("/api/employees/query?pageSize=200"));

        Assert.Equal([world.Employee.Id], slips);
        var ids = employees.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToHashSet();
        Assert.Contains(world.Employee.Id, ids);
        Assert.Contains(ben.Id, ids);
    }
}
