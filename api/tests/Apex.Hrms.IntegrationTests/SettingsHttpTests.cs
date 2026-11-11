using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.AspNetCore.Identity;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The settings module over HTTP against the real host and the test database: an administrator creates users, the new user signs
/// in, accounts are deactivated, unlocked and moved to another role, the permission matrix is saved, and the administrator-only
/// rule is enforced from the database. Every employee, account and role carries the tag of the test, so the cleanup finds them.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed partial class SettingsHttpTests : DatabaseTest, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];
    private HttpClient admin = null!;
    private (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position)? organization;

    public SettingsHttpTests() => factory.EnsurePointsAtTestDatabase();

    [GeneratedRegex(@"token=([^\s]+)")]
    private static partial Regex TokenInMail();

    public override async Task InitializeAsync()
    {
        admin = NewClient();
        await admin.SignInAsync(TestAdministrator.Account);
    }

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

    private HttpClient NewClient()
    {
        var client = factory.CreateBrowserClient(out _);
        clients.Add(client);
        return client;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private static T Read<T>(JsonElement element) => element.Deserialize<T>(WebOptions)!;

    private static async Task<T> SucceedAsync<T>(Task<HttpResponseMessage> pending, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await pending;
        Assert.True(response.StatusCode == expected, $"Expected {(int)expected} but got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return Read<T>(await ReadAsync(response));
    }

    private static Task<HttpResponseMessage> Command(HttpClient client, string url, string rowVersion) =>
        client.PostAsJsonAsync(url, new { rowVersion });

    private async Task<RoleDto> CreateRoleAsync(string suffix)
    {
        var name = $"{Tag}-{suffix}";
        return await SucceedAsync<RoleDto>(admin.PostAsJsonAsync("/api/roles", new { name, description = "Fictional test role", status = "Active" }), HttpStatusCode.Created);
    }

    private async Task<EmployeeSeed> SeedPersonAsync(string local)
    {
        organization ??= await SeedOrganizationAsync();
        var org = organization.Value;
        var employee = await SeedEmployeeAsync(org, codeSuffix: null, local);
        return new EmployeeSeed(employee.Id, Requests.WorkEmail(local));
    }

    private sealed record EmployeeSeed(int Id, string WorkEmail);

    private async Task<(UserDto User, string Password, string Email)> CreateUserAsync(string local, RoleDto role)
    {
        var person = await SeedPersonAsync(local);
        var password = TestAccounts.NewPassword();
        var user = await SucceedAsync<UserDto>(
            admin.PostAsJsonAsync("/api/users", new { employeeId = person.Id, roleId = role.Id, initialPassword = password }),
            HttpStatusCode.Created);
        return (user, password, person.WorkEmail);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password, rememberMe = false });

    private static string Version(UserDto user) => Convert.ToBase64String(user.RowVersion);

    private static string Version(RoleDto role) => Convert.ToBase64String(role.RowVersion);

    // ---- Users ----

    [IntegrationFact]
    public async Task An_administrator_creates_a_user_who_can_then_sign_in_and_the_password_is_stored_only_as_a_hash()
    {
        var role = await CreateRoleAsync("Staff");

        var (user, password, email) = await CreateUserAsync("mia", role);

        Assert.Equal(email.ToLowerInvariant(), user.Email);
        Assert.Equal("Mia Testwell", user.DisplayName);
        Assert.Equal("Active", user.Status);
        Assert.Equal(role.Id, user.RoleId);
        Assert.NotNull(user.EmployeeId);

        // The password is nowhere in the response; the database holds a verifiable hash, not the text.
        Assert.DoesNotContain(password, JsonSerializer.Serialize(user), StringComparison.Ordinal);
        var hash = await Sql.ScalarAsync<string>("SELECT PasswordHash FROM auth.[User] WHERE NormalizedEmail = UPPER(@email)", ("@email", email));
        Assert.NotEqual(password, hash);
        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<IdentityUser>().VerifyHashedPassword(new IdentityUser(), hash, password));

        // The activity feed names the display name only: no email, no password.
        var entries = await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM app.ActivityLog WHERE Category = 'system' AND Message = N'User \"Mia Testwell\" created' AND ActorUserId = @admin",
            ("@admin", TestAdministrator.AccountId));
        Assert.True(entries >= 1);
        var leaked = await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM app.ActivityLog WHERE Message LIKE '%' + @email + '%' OR Message LIKE '%' + @password + '%'",
            ("@email", email),
            ("@password", password));
        Assert.Equal(0, leaked);

        var newUser = NewClient();
        var signIn = await LoginAsync(newUser, email, password);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var me = await ReadAsync(await newUser.GetAsync("/api/auth/me"));
        Assert.Equal(role.Name, me.GetProperty("roleName").GetString());
    }

    [IntegrationFact]
    public async Task A_user_who_is_not_an_administrator_cannot_create_users_roles_permissions_or_settings()
    {
        var role = await CreateRoleAsync("Staff");
        var (_, password, email) = await CreateUserAsync("mia", role);
        var person = await SeedPersonAsync("lee");
        var staff = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(staff, email, password)).StatusCode);

        var create = await staff.PostAsJsonAsync("/api/users", new { employeeId = person.Id, roleId = role.Id, initialPassword = TestAccounts.NewPassword() });
        var roles = await staff.PostAsJsonAsync("/api/roles", new { name = $"{Tag}-Sneaky", status = "Active" });
        var read = await staff.GetAsync("/api/users/query");
        var matrix = await staff.GetAsync("/api/role-permissions");
        var settings = await staff.PutAsJsonAsync("/api/general-settings", new { organizationName = "Nope" });

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, roles.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, matrix.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, settings.StatusCode);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE EmployeeId = @id", ("@id", person.Id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.Role WHERE Name = @name", ("@name", $"{Tag}-Sneaky")));
    }

    [IntegrationFact]
    public async Task Nobody_signed_out_can_reach_the_settings_endpoints()
    {
        var anonymous = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/query")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/users", new { })).StatusCode);
    }

    [IntegrationFact]
    public async Task One_employee_gets_one_user_and_the_second_attempt_is_refused()
    {
        var role = await CreateRoleAsync("Staff");
        var person = await SeedPersonAsync("mia");
        var body = new { employeeId = person.Id, roleId = role.Id, initialPassword = TestAccounts.NewPassword() };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/users", body)).StatusCode);

        var second = await admin.PostAsJsonAsync("/api/users", body);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var problem = await ReadAsync(second);
        Assert.Equal("Validation", problem.GetProperty("code").GetString());
        Assert.Equal("A user already exists for this employee.", problem.GetProperty("errors").GetProperty("employeeId")[0].GetString());
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE EmployeeId = @id", ("@id", person.Id)));
    }

    [IntegrationFact]
    public async Task A_short_password_a_missing_employee_and_an_inactive_role_create_nothing()
    {
        var role = await CreateRoleAsync("Staff");
        var retired = await CreateRoleAsync("Retired");
        Assert.Equal(HttpStatusCode.OK, (await Command(admin, $"/api/roles/{retired.Id}/deactivate", Version(retired))).StatusCode);
        var person = await SeedPersonAsync("mia");

        var shortPassword = await admin.PostAsJsonAsync("/api/users", new { employeeId = person.Id, roleId = role.Id, initialPassword = "only-11-chr" });
        var noEmployee = await admin.PostAsJsonAsync("/api/users", new { employeeId = 999_999_999, roleId = role.Id, initialPassword = TestAccounts.NewPassword() });
        var inactiveRole = await admin.PostAsJsonAsync("/api/users", new { employeeId = person.Id, roleId = retired.Id, initialPassword = TestAccounts.NewPassword() });

        Assert.Equal("Use at least 12 characters.", (await ReadAsync(shortPassword)).GetProperty("errors").GetProperty("initialPassword")[0].GetString());
        Assert.Equal("Choose a current employee.", (await ReadAsync(noEmployee)).GetProperty("errors").GetProperty("employeeId")[0].GetString());
        Assert.Equal("Choose an active role.", (await ReadAsync(inactiveRole)).GetProperty("errors").GetProperty("roleId")[0].GetString());
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE EmployeeId = @id", ("@id", person.Id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM auth.[User] WHERE NormalizedEmail = UPPER(@email)", ("@email", person.WorkEmail)));
    }

    [IntegrationFact]
    public async Task Deactivating_a_user_refuses_sign_in_and_ends_the_open_session_and_activating_allows_it_again()
    {
        var role = await CreateRoleAsync("Staff");
        var (user, password, email) = await CreateUserAsync("mia", role);
        var session = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(session, email, password)).StatusCode);

        // Signing in writes the last-login time, and SQL Server moves a rowversion on every write: read the user again.
        var stale = await Command(admin, $"/api/users/{user.Id}/deactivate", Version(user));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        user = await SucceedAsync<UserDto>(admin.GetAsync($"/api/users/{user.Id}"));

        var off = await SucceedAsync<UserDto>(Command(admin, $"/api/users/{user.Id}/deactivate", Version(user)));

        Assert.Equal("Inactive", off.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), email, password)).StatusCode);

        var on = await SucceedAsync<UserDto>(Command(admin, $"/api/users/{user.Id}/activate", Version(off)));

        Assert.Equal("Active", on.Status);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), email, password)).StatusCode);
    }

    [IntegrationFact]
    public async Task A_locked_out_user_shows_as_Locked_and_unlock_lets_the_correct_password_in_again()
    {
        var role = await CreateRoleAsync("Staff");
        var (user, password, email) = await CreateUserAsync("mia", role);
        var attacker = NewClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(attacker, email, "wrong-password-" + attempt)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), email, password)).StatusCode);
        var locked = await SucceedAsync<UserDto>(admin.GetAsync($"/api/users/{user.Id}"));
        Assert.Equal("Locked", locked.Status);
        var asList = await ReadAsync(await admin.GetAsync($"/api/users/query?search={Uri.EscapeDataString(email)}"));
        Assert.Equal("Locked", asList.GetProperty("items")[0].GetProperty("status").GetString());

        var unlocked = await SucceedAsync<UserDto>(Command(admin, $"/api/users/{user.Id}/unlock", Version(locked)));

        Assert.Equal("Active", unlocked.Status);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), email, password)).StatusCode);
    }

    [IntegrationFact]
    public async Task Changing_the_role_of_a_user_updates_the_account_and_a_stale_version_is_a_conflict()
    {
        var first = await CreateRoleAsync("Staff");
        var second = await CreateRoleAsync("Lead");
        var (user, _, _) = await CreateUserAsync("mia", first);

        var updated = await SucceedAsync<UserDto>(admin.PutAsJsonAsync($"/api/users/{user.Id}", new { roleId = second.Id, rowVersion = Version(user) }));
        var stale = await admin.PutAsJsonAsync($"/api/users/{user.Id}", new { roleId = first.Id, rowVersion = Version(user) });

        Assert.Equal(second.Id, updated.RoleId);
        Assert.NotEqual(user.RowVersion, updated.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("Conflict", (await ReadAsync(stale)).GetProperty("code").GetString());
        Assert.Equal(second.Id, await Sql.ScalarAsync<int>("SELECT RoleId FROM sec.AppUser WHERE Id = @id", ("@id", user.Id)));
    }

    [IntegrationFact]
    public async Task An_administrator_cannot_demote_deactivate_or_delete_their_own_account()
    {
        var staff = await CreateRoleAsync("Staff");
        var self = await SucceedAsync<UserDto>(admin.GetAsync($"/api/users/{TestAdministrator.AccountId}"));

        var demote = await admin.PutAsJsonAsync($"/api/users/{self.Id}", new { roleId = staff.Id, rowVersion = Version(self) });
        var deactivate = await Command(admin, $"/api/users/{self.Id}/deactivate", Version(self));
        var delete = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{self.Id}") { Content = JsonContent.Create(new { rowVersion = Version(self) }) });

        foreach (var response in new[] { demote, deactivate, delete })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        Assert.Equal("You cannot change your own role or deactivate your own account.", (await ReadAsync(demote)).GetProperty("detail").GetString());
        Assert.Equal("You cannot delete your own account.", (await ReadAsync(delete)).GetProperty("detail").GetString());
        Assert.Equal("Active", await Sql.ScalarAsync<string>("SELECT Status FROM sec.AppUser WHERE Id = @id", ("@id", self.Id)));
    }

    [IntegrationFact]
    public async Task Deleting_a_user_removes_the_account_and_the_login()
    {
        var role = await CreateRoleAsync("Staff");
        var (user, password, email) = await CreateUserAsync("mia", role);

        var delete = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/users/{user.Id}") { Content = JsonContent.Create(new { rowVersion = Version(user) }) });

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE Id = @id", ("@id", user.Id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM auth.[User] WHERE NormalizedEmail = UPPER(@email)", ("@email", email)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), email, password)).StatusCode);
    }

    [IntegrationFact]
    public async Task An_administrator_can_send_the_password_reset_mail_and_the_user_can_choose_a_new_password_with_it()
    {
        var role = await CreateRoleAsync("Staff");
        var (user, password, email) = await CreateUserAsync("mia", role);

        var reset = await admin.PostAsync($"/api/users/{user.Id}/password-reset", content: null);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var file = Assert.Single(Directory.GetFiles(factory.MailFolder, "*.eml"));
        var mail = await File.ReadAllTextAsync(file);
        Assert.Contains($"To: {email.ToLowerInvariant()}", mail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, mail, StringComparison.Ordinal);
        var token = Uri.UnescapeDataString(TokenInMail().Match(mail).Groups[1].Value);
        var newPassword = TestAccounts.NewPassword();
        var choose = await NewClient().PostAsJsonAsync("/api/auth/reset-password", new { email, token, newPassword });
        Assert.Equal(HttpStatusCode.NoContent, choose.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), email, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), email, password)).StatusCode);
    }

    // ---- Roles ----

    [IntegrationFact]
    public async Task Built_in_roles_cannot_be_renamed_deactivated_or_deleted()
    {
        var all = await SucceedAsync<List<RoleDto>>(admin.GetAsync("/api/roles"));
        var builtIn = all.Single(role => role.Name == "HR Administrator");
        Assert.True(builtIn.IsSystem);

        var rename = await admin.PutAsJsonAsync($"/api/roles/{builtIn.Id}", new { name = $"{Tag}-Renamed", description = builtIn.Description, rowVersion = Version(builtIn) });
        var deactivate = await Command(admin, $"/api/roles/{builtIn.Id}/deactivate", Version(builtIn));
        var delete = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/roles/{builtIn.Id}") { Content = JsonContent.Create(new { rowVersion = Version(builtIn) }) });

        Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);
        Assert.Equal("A built-in role cannot be renamed.", (await ReadAsync(rename)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("HR Administrator", await Sql.ScalarAsync<string>("SELECT Name FROM sec.Role WHERE Id = @id", ("@id", builtIn.Id)));
    }

    [IntegrationFact]
    public async Task A_role_that_users_hold_cannot_be_deactivated_or_deleted_but_an_unused_one_can_be_deleted()
    {
        var used = await CreateRoleAsync("Used");
        var unused = await CreateRoleAsync("Unused");
        await CreateUserAsync("mia", used);

        var deactivate = await Command(admin, $"/api/roles/{used.Id}/deactivate", Version(used));
        var deleteUsed = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/roles/{used.Id}") { Content = JsonContent.Create(new { rowVersion = Version(used) }) });
        var deleteUnused = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/roles/{unused.Id}") { Content = JsonContent.Create(new { rowVersion = Version(unused) }) });

        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal("1 active user(s) still have this role. Move them to another role first.", (await ReadAsync(deactivate)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, deleteUsed.StatusCode);
        Assert.Equal($"\"{used.Name}\" cannot be deleted because it is used by 1 user(s). Deactivate it instead.", (await ReadAsync(deleteUsed)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.NoContent, deleteUnused.StatusCode);
    }

    [IntegrationFact]
    public async Task Role_names_are_unique()
    {
        var role = await CreateRoleAsync("Staff");

        var duplicate = await admin.PostAsJsonAsync("/api/roles", new { name = role.Name.ToUpperInvariant(), status = "Active" });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.NotNull((await ReadAsync(duplicate)).GetProperty("errors").GetProperty("name")[0].GetString());
    }

    // ---- Permission matrix ----

    [IntegrationFact]
    public async Task The_permission_matrix_is_saved_per_role_with_the_view_rule_and_the_row_version()
    {
        var role = await CreateRoleAsync("Staff");
        var empty = await SucceedAsync<RolePermissionsDto>(admin.GetAsync($"/api/role-permissions/{role.Id}"));
        Assert.Empty(empty.Grants);

        var saved = await SucceedAsync<RolePermissionsDto>(admin.PutAsJsonAsync(
            $"/api/role-permissions/{role.Id}",
            new { grants = new Dictionary<string, string[]> { ["leave"] = ["Edit", "View", "Approve"], ["employees"] = ["View"], ["settings"] = [] }, rowVersion = Convert.ToBase64String(empty.RowVersion) }));

        Assert.Equal(["View", "Edit", "Approve"], saved.Grants["leave"]);
        Assert.Equal(["View"], saved.Grants["employees"]);
        Assert.DoesNotContain("settings", saved.Grants.Keys);
        Assert.NotEqual(empty.RowVersion, saved.RowVersion);
        Assert.Equal(4, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.RolePermission WHERE RoleId = @id", ("@id", role.Id)));
        Assert.Equal(4, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.RolePermission WHERE RoleId = @id AND CreatedByUserId = @admin", ("@id", role.Id), ("@admin", TestAdministrator.AccountId)));

        var matrix = await ReadAsync(await admin.GetAsync("/api/role-permissions"));
        Assert.Contains(matrix.GetProperty("modules").EnumerateArray(), module => module.GetProperty("key").GetString() == "leave");
        var mine = matrix.GetProperty("roles").EnumerateArray().Single(entry => entry.GetProperty("roleId").GetInt32() == role.Id);
        Assert.Equal(2, mine.GetProperty("grants").EnumerateObject().Count());

        // The first version is stale now; a grant without View is refused; a smaller set replaces the old one.
        var stale = await admin.PutAsJsonAsync($"/api/role-permissions/{role.Id}", new { grants = new Dictionary<string, string[]>(), rowVersion = Convert.ToBase64String(empty.RowVersion) });
        var noView = await admin.PutAsJsonAsync($"/api/role-permissions/{role.Id}", new { grants = new Dictionary<string, string[]> { ["leave"] = ["Create"] }, rowVersion = Convert.ToBase64String(saved.RowVersion) });
        var smaller = await SucceedAsync<RolePermissionsDto>(admin.PutAsJsonAsync($"/api/role-permissions/{role.Id}", new { grants = new Dictionary<string, string[]> { ["leave"] = ["View"] }, rowVersion = Convert.ToBase64String(saved.RowVersion) }));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noView.StatusCode);
        Assert.Equal("View is required.", (await ReadAsync(noView)).GetProperty("errors").GetProperty("leave")[0].GetString());
        Assert.Equal(["View"], smaller.Grants["leave"]);
        Assert.Single(smaller.Grants);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.RolePermission WHERE RoleId = @id", ("@id", role.Id)));
    }

    [IntegrationFact]
    public async Task The_HR_Administrator_cannot_lose_a_grant()
    {
        var all = await SucceedAsync<List<RoleDto>>(admin.GetAsync("/api/roles"));
        var builtIn = all.Single(role => role.Name == "HR Administrator");
        var current = await SucceedAsync<RolePermissionsDto>(admin.GetAsync($"/api/role-permissions/{builtIn.Id}"));
        var before = await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.RolePermission WHERE RoleId = @id", ("@id", builtIn.Id));

        var fewer = current.Grants.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        fewer["settings"] = ["View"];
        var response = await admin.PutAsJsonAsync($"/api/role-permissions/{builtIn.Id}", new { grants = fewer, rowVersion = Convert.ToBase64String(current.RowVersion) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(before, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.RolePermission WHERE RoleId = @id", ("@id", builtIn.Id)));
    }

    // ---- General settings ----

    [IntegrationFact]
    public async Task General_settings_are_read_validated_and_saved_with_a_row_version()
    {
        var original = await SucceedAsync<GeneralSettingsDto>(admin.GetAsync("/api/general-settings"));
        var bad = await admin.PutAsJsonAsync("/api/general-settings", new { organizationName = original.OrganizationName, contactEmail = "nope", timezone = "Mars/Base", dateFormat = original.DateFormat, currencyCode = "mmk", fiscalYearStartMonth = 13, emailNotifications = true, weeklyDigest = false, rowVersion = Convert.ToBase64String(original.RowVersion) });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await ReadAsync(bad)).GetProperty("errors");
        foreach (var field in new[] { "contactEmail", "timezone", "currencyCode", "fiscalYearStartMonth" })
        {
            Assert.True(errors.TryGetProperty(field, out _), field);
        }

        GeneralSettingsDto? changed = null;
        try
        {
            changed = await SucceedAsync<GeneralSettingsDto>(admin.PutAsJsonAsync("/api/general-settings", ToBody(original, month: original.FiscalYearStartMonth == 4 ? 5 : 4)));
            Assert.NotEqual(original.RowVersion, changed.RowVersion);
            Assert.NotEqual(original.FiscalYearStartMonth, changed.FiscalYearStartMonth);

            var stale = await admin.PutAsJsonAsync("/api/general-settings", ToBody(original, month: 6));
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        finally
        {
            // The settings are one shared row: put the original values back.
            var latest = changed ?? original;
            await admin.PutAsJsonAsync("/api/general-settings", ToBody(original, month: original.FiscalYearStartMonth, rowVersion: latest.RowVersion));
        }

        var restored = await SucceedAsync<GeneralSettingsDto>(admin.GetAsync("/api/general-settings"));
        Assert.Equal(original.FiscalYearStartMonth, restored.FiscalYearStartMonth);
    }

    private static object ToBody(GeneralSettingsDto settings, int month, byte[]? rowVersion = null) => new
    {
        settings.OrganizationName,
        settings.ContactEmail,
        settings.Timezone,
        settings.DateFormat,
        settings.CurrencyCode,
        fiscalYearStartMonth = month,
        settings.EmailNotifications,
        settings.WeeklyDigest,
        rowVersion = Convert.ToBase64String(rowVersion ?? settings.RowVersion),
    };
}
