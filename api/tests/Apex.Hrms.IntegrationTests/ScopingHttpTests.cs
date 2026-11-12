using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The security re-review fixes over HTTP against the test database: an employee-role account sees and files only its own
/// leave and attendance, an approver sees all, a non-administrator cannot record the exit of an administrator, nobody records
/// their own exit, and deactivating a user ends the open session. Every account, role, employee and shift carries the tag.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ScopingHttpTests : DatabaseTest, IDisposable
{
    private static readonly string[] EmployeeGrants = ["leave:View", "leave:Create", "attendance:View", "attendance:Create"];

    private static readonly int[] EveryDay = [0, 1, 2, 3, 4, 5, 6];

    private static string Day(int offset) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];

    public ScopingHttpTests() => factory.EnsurePointsAtTestDatabase();

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

    private async Task<string> RoleWithAsync(string suffix, params string[] grants)
    {
        var name = $"{Tag}-{suffix}";
        var roleId = await Sql.ScalarAsync<int>("INSERT INTO sec.Role (Name, IsSystem, Status) OUTPUT INSERTED.Id VALUES (@name, 0, 'Active');", ("@name", name));
        foreach (var grant in grants.OrderBy(grant => grant.EndsWith(":View", StringComparison.Ordinal) ? 0 : 1))
        {
            var parts = grant.Split(':');
            await Sql.ExecuteAsync(
                "INSERT INTO sec.RolePermission (RoleId, ModuleKey, Action) VALUES (@role, @module, @action);",
                ("@role", roleId),
                ("@module", parts[0]),
                ("@action", parts[1]));
        }

        return name;
    }

    private static DateOnly Monday(int weeksAhead = 3)
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

    private static async Task<string> AccountVersionAsync(int accountId) =>
        Convert.ToBase64String(await Sql.ScalarAsync<byte[]>("SELECT RowVersion FROM sec.AppUser WHERE Id = @id", ("@id", accountId)));

    private static async Task<string> EmployeeVersionAsync(HttpClient client, int id) =>
        (await ReadAsync(await client.GetAsync($"/api/employees/{id}"))).GetProperty("rowVersion").GetString()!;

    [IntegrationFact]
    public async Task An_employee_role_account_sees_only_its_own_leave_and_cannot_file_leave_for_a_colleague()
    {
        var org = await SeedOrganizationAsync();
        var mia = await SeedEmployeeAsync(org, "1", "mia");
        var noah = await SeedEmployeeAsync(org, "2", "noah");
        var type = await SeedLeaveTypeAsync();
        var miaLeave = await SeedSubmittedLeaveAsync(mia.Id, type.Id, Monday(3));
        var noahLeave = await SeedSubmittedLeaveAsync(noah.Id, type.Id, Monday(4));
        var role = await RoleWithAsync("Employee", EmployeeGrants);
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "mia-login"), roleName: role, employeeId: mia.Id);
        var employee = await SignedInAsync(account);
        var approver = await SignedInAsync(TestAdministrator.Account);

        var list = await employee.GetFromJsonAsync<PagedResult<LeaveApplicationDto>>($"/api/leave-applications/query?pageSize=50");
        var theirs = await employee.GetAsync($"/api/leave-applications/{noahLeave.Id}");
        var forColleague = await employee.PostAsJsonAsync("/api/leave-applications", Requests.LeaveApplication(noah.Id, type.Id, Monday(5)));
        var cancelTheirs = await employee.PostAsJsonAsync($"/api/leave-applications/{noahLeave.Id}/cancel", new { rowVersion = Requests.Version(noahLeave.RowVersion) });
        var ownBalance = await employee.GetAsync($"/api/leave/balances?employeeId={mia.Id}");
        var theirBalance = await employee.GetAsync($"/api/leave/balances?employeeId={noah.Id}");
        var ownCreate = await employee.PostAsJsonAsync("/api/leave-applications", Requests.LeaveApplication(mia.Id, type.Id, Monday(6)));

        Assert.Contains(list!.Items, item => item.Id == miaLeave.Id);
        Assert.DoesNotContain(list!.Items, item => item.EmployeeId != mia.Id);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forColleague.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cancelTheirs.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownBalance.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, theirBalance.StatusCode);
        Assert.Equal(HttpStatusCode.Created, ownCreate.StatusCode);

        var all = await approver.GetFromJsonAsync<PagedResult<LeaveApplicationDto>>("/api/leave-applications/query?pageSize=200");
        Assert.Contains(all!.Items, item => item.Id == miaLeave.Id);
        Assert.Contains(all.Items, item => item.Id == noahLeave.Id);
        Assert.Equal(HttpStatusCode.OK, (await approver.GetAsync($"/api/leave/balances?employeeId={noah.Id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task An_employee_role_account_punches_for_itself_sees_only_its_own_attendance_and_cannot_create_shifts_or_holidays()
    {
        var approver = await SignedInAsync(TestAdministrator.Account);
        var shiftResponse = await approver.PostAsJsonAsync("/api/shifts", new
        {
            code = Code("S1"), name = Name("General"), startTime = "09:00", endTime = "18:00", breakMinutes = 60, graceMinutes = 15,
            workDays = EveryDay, isNightShift = false, color = "#3f6fd8", status = "Active",
        });
        Assert.Equal(HttpStatusCode.Created, shiftResponse.StatusCode);
        var shiftId = (await ReadAsync(shiftResponse)).GetProperty("id").GetInt32();
        var org = await SeedOrganizationAsync();
        var mia = Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "1", "mia") with { ShiftId = shiftId }));
        var noah = Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "2", "noah") with { ShiftId = shiftId }));
        var role = await RoleWithAsync("Employee", EmployeeGrants);
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "mia-login"), roleName: role, employeeId: mia.Id);
        var employee = await SignedInAsync(account);

        // The administrator has no linked employee and may punch for anyone; the employee only for itself.
        Assert.Equal(HttpStatusCode.OK, (await approver.PostAsJsonAsync("/api/attendance/punches", new { employeeId = noah.Id, kind = "CheckIn" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsJsonAsync("/api/attendance/punches", new { employeeId = mia.Id, kind = "CheckIn" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/attendance/punches", new { employeeId = noah.Id, kind = "CheckOut" })).StatusCode);

        var records = await ReadAsync(await employee.GetAsync($"/api/attendance-records/query?pageSize=100&search={Tag[TestRows.TagFamily.Length..]}"));
        var ids = records.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("employeeId").GetInt32()).Distinct().ToList();
        Assert.Equal([mia.Id], ids);
        var allRecords = await ReadAsync(await approver.GetAsync($"/api/attendance-records/query?pageSize=100&search={Tag[TestRows.TagFamily.Length..]}"));
        Assert.Equal(2, allRecords.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("employeeId").GetInt32()).Distinct().Count());

        var adjustmentForColleague = await employee.PostAsJsonAsync("/api/attendance-adjustments", new { employeeId = noah.Id, date = Day(0), requestedCheckIn = "09:00", reason = "Forgot" });
        var overtimeForColleague = await employee.PostAsJsonAsync("/api/overtime-records", new { employeeId = noah.Id, date = Day(0), startTime = "18:00", endTime = "20:00", reason = "Close" });
        Assert.Equal(HttpStatusCode.Forbidden, adjustmentForColleague.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, overtimeForColleague.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/shifts", new { code = Code("S2") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/holidays", new { date = "2041-05-01", name = Name("Day"), type = "Company" })).StatusCode);
    }

    [IntegrationFact]
    public async Task A_non_administrator_cannot_record_the_exit_of_an_administrator_and_nobody_records_their_own_exit()
    {
        var org = await SeedOrganizationAsync();
        var adminEmployee = await SeedEmployeeAsync(org, "1", "boss");
        var officerEmployee = await SeedEmployeeAsync(org, "2", "officer");
        await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "boss-login"), employeeId: adminEmployee.Id);
        var role = await RoleWithAsync("Officer", "employees:View", "employees:Edit");
        var officerAccount = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "officer-login"), roleName: role, employeeId: officerEmployee.Id);
        var officer = await SignedInAsync(officerAccount);
        var exitDate = Day(-1);

        var exitAdmin = await officer.PostAsJsonAsync($"/api/employees/{adminEmployee.Id}/exit", new { rowVersion = await EmployeeVersionAsync(officer, adminEmployee.Id), exitDate, status = "Resigned" });
        var exitSelf = await officer.PostAsJsonAsync($"/api/employees/{officerEmployee.Id}/exit", new { rowVersion = await EmployeeVersionAsync(officer, officerEmployee.Id), exitDate, status = "Resigned" });

        Assert.Equal(HttpStatusCode.Forbidden, exitAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, exitSelf.StatusCode);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.Employee WHERE Id IN (@a, @b) AND ExitDate IS NOT NULL", ("@a", adminEmployee.Id), ("@b", officerEmployee.Id)));
    }

    [IntegrationFact]
    public async Task Activating_the_account_of_a_leaver_is_refused_and_an_administrator_can_record_the_exit()
    {
        var org = await SeedOrganizationAsync();
        var leaver = await SeedEmployeeAsync(org, "1", "leaver");
        var role = await RoleWithAsync("Staff", "employees:View");
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "leaver-login"), roleName: role, employeeId: leaver.Id);
        var admin = await SignedInAsync(TestAdministrator.Account);
        var exitDate = Day(-1);

        var exit = await admin.PostAsJsonAsync($"/api/employees/{leaver.Id}/exit", new { rowVersion = await EmployeeVersionAsync(admin, leaver.Id), exitDate, status = "Resigned" });
        var activate = await admin.PostAsJsonAsync($"/api/users/{account.AccountId}/activate", new { rowVersion = await AccountVersionAsync(account.AccountId) });

        Assert.Equal(HttpStatusCode.OK, exit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
        Assert.Equal("Inactive", await Sql.ScalarAsync<string>("SELECT Status FROM sec.AppUser WHERE Id = @id", ("@id", account.AccountId)));
    }

    [IntegrationFact]
    public async Task Deactivating_a_user_in_settings_ends_its_open_session_at_the_next_request()
    {
        var role = await RoleWithAsync("Staff", "employees:View");
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "staff-login"), roleName: role);
        var staff = await SignedInAsync(account);
        var admin = await SignedInAsync(TestAdministrator.Account);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/auth/me")).StatusCode);

        var deactivate = await admin.PostAsJsonAsync($"/api/users/{account.AccountId}/deactivate", new { rowVersion = await AccountVersionAsync(account.AccountId) });

        // The security stamp changed: even after the account is active again (checked before any request could drop the cookie)
        // the old session is dead. Without the stamp change the status check alone would let it live again.
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var reactivate = await admin.PostAsJsonAsync($"/api/users/{account.AccountId}/activate", new { rowVersion = await AccountVersionAsync(account.AccountId) });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/auth/me")).StatusCode);
    }
}
