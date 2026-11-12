using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The permission check against the real host and the test database: grants are read from sec.RolePermission for the role of
/// the signed-in account, HR Administrator holds every grant through the seed, a leaver loses the account and the sessions,
/// and the rules that protect the last administrator hold under concurrent requests. Every account, role and employee carries
/// the tag of the test, so the cleanup finds it.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class PermissionsHttpTests : DatabaseTest, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];
    private (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position)? organization;

    public PermissionsHttpTests() => factory.EnsurePointsAtTestDatabase();

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

    /// <summary>An active, tagged role with exactly these grants (View of a module is written before its other actions).</summary>
    private async Task<int> RoleWithAsync(string suffix, params string[] grants)
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

        return roleId;
    }

    private async Task<(int Id, string WorkEmail)> SeedPersonAsync(string local)
    {
        organization ??= await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(organization.Value, codeSuffix: null, local);
        return (employee.Id, Requests.WorkEmail(local));
    }

    private static async Task<string> EmployeeVersionAsync(HttpClient client, int id)
    {
        var employee = (await client.GetFromJsonAsync<EmployeeDto>($"/api/employees/{id}"))!;
        return Convert.ToBase64String(employee.RowVersion);
    }

    private static async Task<string> AccountVersionAsync(int accountId) =>
        Convert.ToBase64String(await Sql.ScalarAsync<byte[]>("SELECT RowVersion FROM sec.AppUser WHERE Id = @id", ("@id", accountId)));

    // ---- The administrator role holds every grant (the account the other integration tests sign in as) ----

    [IntegrationFact]
    public async Task The_HR_Administrator_role_holds_every_module_and_action_of_the_permission_model()
    {
        var missing = await Sql.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sec.PermissionModuleAction a
            WHERE NOT EXISTS (SELECT 1 FROM sec.RolePermission p JOIN sec.Role r ON r.Id = p.RoleId
                              WHERE r.Name = N'HR Administrator' AND p.ModuleKey = a.ModuleKey AND p.Action = a.Action);
            """);
        Assert.Equal(0, missing);

        foreach (var key in Apex.Hrms.Api.Authorization.PermissionModule.All.SelectMany(module => module.Permissions.Select(permission => (module.Key, Action: permission.ToString()))))
        {
            Assert.Equal(1, await Sql.ScalarAsync<int>(
                "SELECT COUNT(*) FROM sec.PermissionModuleAction WHERE ModuleKey = @module AND Action = @action",
                ("@module", key.Key),
                ("@action", key.Action)));
        }

        var admin = await SignedInAsync(TestAdministrator.Account);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/companies/query")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/activity/query")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/excel/modules")).StatusCode);
    }

    // ---- A limited role ----

    [IntegrationFact]
    public async Task A_user_with_a_limited_role_is_refused_on_ViewBank_and_ViewSensitive_and_allowed_on_View()
    {
        var person = await SeedPersonAsync("mia");
        await RoleWithAsync("Limited", "employees:View");
        var limited = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "limited"), roleName: $"{Tag}-Limited");
        var client = await SignedInAsync(limited);

        // employees:View is allowed, but the account has no linked employee, so its team is empty: the list
        // answers 200 and another employee reads as not found (team scoping). The personal-data policies still refuse.
        var list = await client.GetAsync("/api/employees/query");
        var detail = await client.GetAsync($"/api/employees/{person.Id}");
        var bank = await client.GetAsync($"/api/employees/{person.Id}/bank-account");
        var personal = await client.GetAsync($"/api/employees/{person.Id}/personal");
        var company = await client.GetAsync("/api/companies/query");
        var bankWrite = await client.PutAsJsonAsync($"/api/employees/{person.Id}/bank-account", new { });

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bank.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, personal.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, company.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bankWrite.StatusCode);
        Assert.Equal("Forbidden", (await bank.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [IntegrationFact]
    public async Task A_grant_saved_through_the_settings_API_is_seen_at_once_by_the_same_host_despite_the_cache()
    {
        var person = await SeedPersonAsync("mia");
        var roleId = await RoleWithAsync("Limited", "employees:View");
        var limited = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "limited"), roleName: $"{Tag}-Limited");
        var user = await SignedInAsync(limited);
        var admin = await SignedInAsync(TestAdministrator.Account);

        // Two refusals, so the empty answer is certainly cached before the grant exists.
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/employees/{person.Id}/bank-account")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/employees/{person.Id}/bank-account")).StatusCode);

        var current = await admin.GetFromJsonAsync<JsonElement>($"/api/role-permissions/{roleId}");
        var save = await admin.PutAsJsonAsync(
            $"/api/role-permissions/{roleId}",
            new { grants = new Dictionary<string, string[]> { ["employees"] = ["View", "ViewBank"] }, rowVersion = current.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var allowed = await user.GetAsync($"/api/employees/{person.Id}/bank-account");
        Assert.Equal(HttpStatusCode.NotFound, allowed.StatusCode);

        // And a removed grant is gone at once as well.
        var after = await admin.GetFromJsonAsync<JsonElement>($"/api/role-permissions/{roleId}");
        Assert.Equal(
            HttpStatusCode.OK,
            (await admin.PutAsJsonAsync(
                $"/api/role-permissions/{roleId}",
                new { grants = new Dictionary<string, string[]> { ["employees"] = ["View"] }, rowVersion = after.GetProperty("rowVersion").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/employees/{person.Id}/bank-account")).StatusCode);
    }

    [IntegrationFact]
    public async Task A_user_whose_role_is_deactivated_or_whose_account_is_inactive_loses_access_at_once()
    {
        await RoleWithAsync("Reader", "employees:View");
        var reader = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "reader"), roleName: $"{Tag}-Reader");
        var client = await SignedInAsync(reader);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees/query?pageSize=1")).StatusCode);

        await Sql.ExecuteAsync("UPDATE sec.Role SET Status = 'Inactive' WHERE Name = @name", ("@name", $"{Tag}-Reader"));

        Assert.True((await client.GetAsync("/api/employees/query?pageSize=1")).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }

    // ---- Leavers ----

    [IntegrationFact]
    public async Task Exit_deactivates_the_linked_account_changes_the_security_stamp_and_ends_the_open_session()
    {
        var person = await SeedPersonAsync("mia");
        await RoleWithAsync("Staff", "employees:View");
        var leaver = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "leaver"), roleName: $"{Tag}-Staff", employeeId: person.Id);
        var session = await SignedInAsync(leaver);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/auth/me")).StatusCode);
        var stampBefore = await Sql.ScalarAsync<string>("SELECT SecurityStamp FROM auth.[User] WHERE Id = @id", ("@id", leaver.AuthUserId));
        var admin = await SignedInAsync(TestAdministrator.Account);

        var exit = await admin.PostAsJsonAsync(
            $"/api/employees/{person.Id}/exit",
            new { rowVersion = await EmployeeVersionAsync(admin, person.Id), exitDate = "2026-11-01", status = "Resigned" });

        Assert.Equal(HttpStatusCode.OK, exit.StatusCode);
        Assert.Equal("Inactive", await Sql.ScalarAsync<string>("SELECT Status FROM sec.AppUser WHERE Id = @id", ("@id", leaver.AccountId)));
        Assert.NotEqual(stampBefore, await Sql.ScalarAsync<string>("SELECT SecurityStamp FROM auth.[User] WHERE Id = @id", ("@id", leaver.AuthUserId)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateBrowserClient(out _).PostAsJsonAsync("/api/auth/login", new { email = leaver.Email, password = leaver.Password, rememberMe = false })).StatusCode);
    }

    [IntegrationFact]
    public async Task An_account_that_is_switched_on_again_still_cannot_sign_in_while_its_employee_has_an_exit_date()
    {
        var person = await SeedPersonAsync("mia");
        await RoleWithAsync("Staff", "employees:View");
        var leaver = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "leaver"), roleName: $"{Tag}-Staff", employeeId: person.Id);
        var admin = await SignedInAsync(TestAdministrator.Account);
        Assert.Equal(
            HttpStatusCode.OK,
            (await admin.PostAsJsonAsync(
                $"/api/employees/{person.Id}/exit",
                new { rowVersion = await EmployeeVersionAsync(admin, person.Id), exitDate = "2026-11-01", status = "Resigned" })).StatusCode);

        // Someone switches the account on again (here with plain SQL; an administrator could do it in the settings screen).
        await Sql.ExecuteAsync("UPDATE sec.AppUser SET Status = 'Active' WHERE Id = @id", ("@id", leaver.AccountId));
        var response = await factory.CreateBrowserClient(out _).PostAsJsonAsync("/api/auth/login", new { email = leaver.Email, password = leaver.Password, rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Unauthorized", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [IntegrationFact]
    public async Task Exit_of_the_last_active_administrator_is_refused_and_nothing_changes()
    {
        await using var only = await OnlyAdministratorsAsync();
        var person = await SeedPersonAsync("mia");
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "last"), employeeId: person.Id);
        var employee = Succeeded(await Get<EmployeeDto>(person.Id));

        var result = await RunCommand<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(person.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 11, 1), "Resigned"));

        var error = Failed(result, ErrorType.Conflict);
        Assert.Equal("There must always be one active HR Administrator.", error.Message);
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT Status FROM sec.AppUser WHERE Id = @id", ("@id", account.AccountId)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.Employee WHERE Id = @id AND ExitDate IS NOT NULL", ("@id", person.Id)));

        // With a second active administrator the same exit goes through and switches the account off.
        await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "second"));
        var again = Succeeded(await RunCommand<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(person.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 11, 1), "Resigned")));
        Assert.Equal("Resigned", again.EmploymentStatus);
        Assert.Equal("Inactive", await Sql.ScalarAsync<string>("SELECT Status FROM sec.AppUser WHERE Id = @id", ("@id", account.AccountId)));
    }

    // ---- One transaction for the login and the account ----

    [IntegrationFact]
    public async Task When_the_account_save_fails_no_identity_user_is_left_behind()
    {
        var person = await SeedPersonAsync("mia");
        await RoleWithAsync("Staff", "employees:View");
        var roleId = await Sql.ScalarAsync<int>("SELECT Id FROM sec.Role WHERE Name = @name", ("@name", $"{Tag}-Staff"));

        // Another session takes the email between the pre-checks and the save: the unique index refuses the account.
        BeforeNextSave = () => Sql.ExecuteAsync(
            "INSERT INTO sec.AppUser (EmployeeId, DisplayName, Email, RoleId, Status, ExternalSubject) VALUES (NULL, N'TST- Rival', @email, @role, 'Active', NULL);",
            ("@email", person.WorkEmail),
            ("@role", roleId));

        var result = await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<UserCreateRequest, UserDto>>()
            .HandleAsync(new UserCreateRequest(person.Id, roleId, null, TestAccounts.NewPassword()), default));

        // The unique index on the email refuses the account (the unique violation is reported as a validation problem).
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM auth.[User] WHERE NormalizedEmail = UPPER(@email)", ("@email", person.WorkEmail)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE EmployeeId = @id", ("@id", person.Id)));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE Email = @email", ("@email", person.WorkEmail)));
    }

    [IntegrationFact]
    public async Task A_successful_create_leaves_one_login_and_one_account_and_a_delete_removes_both()
    {
        var person = await SeedPersonAsync("mia");
        var roleId = await RoleWithAsync("Staff", "employees:View");

        var created = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<UserCreateRequest, UserDto>>()
            .HandleAsync(new UserCreateRequest(person.Id, roleId, null, TestAccounts.NewPassword()), default)));

        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM auth.[User] WHERE NormalizedEmail = UPPER(@email)", ("@email", person.WorkEmail)));
        var deleted = await InScopeAsync(sp => sp.GetRequiredService<IDeleteHandler<UserDto>>()
            .HandleAsync(created.Id, Convert.ToBase64String(created.RowVersion), default));

        Assert.True(deleted.IsSuccess);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM auth.[User] WHERE NormalizedEmail = UPPER(@email)", ("@email", person.WorkEmail)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE Id = @id", ("@id", created.Id)));
    }

    // ---- Concurrency ----

    [IntegrationFact]
    public async Task Two_concurrent_demotions_of_the_only_two_administrators_cannot_both_succeed()
    {
        await using var only = await OnlyAdministratorsAsync();
        await RoleWithAsync("Staff", "employees:View");
        var staffId = await Sql.ScalarAsync<int>("SELECT Id FROM sec.Role WHERE Name = @name", ("@name", $"{Tag}-Staff"));
        var adminRoleId = await Sql.ScalarAsync<int>("SELECT Id FROM sec.Role WHERE Name = N'HR Administrator'");
        var first = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "admin-a"));
        var second = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "admin-b"));

        for (var round = 0; round < 3; round++)
        {
            await Sql.ExecuteAsync(
                "UPDATE sec.AppUser SET RoleId = @admin WHERE Id IN (@a, @b)",
                ("@admin", adminRoleId),
                ("@a", first.AccountId),
                ("@b", second.AccountId));
            var versions = (A: await AccountVersionAsync(first.AccountId), B: await AccountVersionAsync(second.AccountId));

            var results = await Task.WhenAll(
                InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<UserUpdateRequest, UserDto>>()
                    .HandleAsync(first.AccountId, new UserUpdateRequest(staffId, versions.A), default)),
                InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<UserUpdateRequest, UserDto>>()
                    .HandleAsync(second.AccountId, new UserUpdateRequest(staffId, versions.B), default)));

            Assert.Single(results, result => result.IsSuccess);
            var refused = Assert.Single(results, result => result.IsFailure);
            Assert.Equal(ErrorType.Conflict, refused.Error.Type);
            Assert.Equal("There must always be one active HR Administrator.", refused.Error.Message);
            Assert.Equal(
                1,
                await Sql.ScalarAsync<int>(
                    "SELECT COUNT(*) FROM sec.AppUser WHERE Id IN (@a, @b) AND RoleId = @admin",
                    ("@a", first.AccountId),
                    ("@b", second.AccountId),
                    ("@admin", adminRoleId)));
        }
    }

    /// <summary>
    /// Makes the TST- accounts of HR Administrator inactive for the length of a test (the fixture account the HTTP tests sign in
    /// as) so that the accounts the test creates are the only active administrators; switches them on again at the end. The
    /// database is the dedicated test database: any other active administrator in it is a setup error, not something to hide.
    /// </summary>
    private static async Task<OnlyAdministrators> OnlyAdministratorsAsync()
    {
        const string AdministratorAccounts =
            "FROM sec.AppUser u JOIN sec.Role r ON r.Id = u.RoleId WHERE r.Name = N'HR Administrator' AND u.Status = 'Active' AND u.ExternalSubject IS NOT NULL";
        var foreign = await Sql.ScalarAsync<int>($"SELECT COUNT(*) {AdministratorAccounts} AND u.Email NOT LIKE 'TST-%'");
        Assert.True(foreign == 0, "The test database holds an active HR Administrator that is not a TST- test account; it must hold only test data.");

        var ids = new List<int>();
        await using (var connection = new Microsoft.Data.SqlClient.SqlConnection(SqlServerEnvironment.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new Microsoft.Data.SqlClient.SqlCommand($"SELECT u.Id {AdministratorAccounts} AND u.Email LIKE 'TST-%'", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetInt32(0));
            }
        }

        foreach (var id in ids)
        {
            await Sql.ExecuteAsync("UPDATE sec.AppUser SET Status = 'Inactive' WHERE Id = @id", ("@id", id));
        }

        return new OnlyAdministrators(ids);
    }

    private sealed class OnlyAdministrators(List<int> switchedOff) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var id in switchedOff)
            {
                await Sql.ExecuteAsync("UPDATE sec.AppUser SET Status = 'Active' WHERE Id = @id", ("@id", id));
            }
        }
    }
}
