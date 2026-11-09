using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.IntegrationTests.Support;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Sign-in, sessions, lockout, antiforgery, audit attribution and the password reset flow through HTTP, against the
/// real host and the test database. Every account is created with plain SQL under the tag of the test (TST- email prefix).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed partial class AuthHttpTests : DatabaseTest, IDisposable
{
    private const string SessionCookie = "hrms-dev";

    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];

    public AuthHttpTests() => factory.EnsurePointsAtTestDatabase();

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

    [GeneratedRegex(@"token=([^\s]+)")]
    private static partial Regex TokenInMail();

    [GeneratedRegex(@"expires=([^;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpiresAttribute();

    private HttpClient NewClient(bool sendXsrfHeader = true)
    {
        var client = factory.CreateBrowserClient(out _, sendXsrfHeader);
        clients.Add(client);
        return client;
    }

    private Task<TestAccount> AccountAsync(string name, string status = "Active", string role = TestAccounts.AdministratorRole, bool linkAccount = true) =>
        TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, name), status: status, roleName: role, linkAccount: linkAccount);

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, bool rememberMe = false) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password, rememberMe });

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static string SetCookieOf(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal));

    private string[] MailFiles() =>
        Directory.Exists(factory.MailFolder) ? Directory.GetFiles(factory.MailFolder, "*.eml") : [];

    private async Task<string> ResetTokenFromMailAsync(string email)
    {
        var file = Assert.Single(MailFiles());
        var content = await File.ReadAllTextAsync(file);
        Assert.Contains($"To: {email}", content, StringComparison.Ordinal);
        File.Delete(file);
        return Uri.UnescapeDataString(TokenInMail().Match(content).Groups[1].Value);
    }

    // ---- Login ----

    [IntegrationFact]
    public async Task Login_returns_the_account_with_its_permissions_and_sets_a_http_only_same_site_session_cookie()
    {
        var account = await AccountAsync("login");
        var client = NewClient();

        var response = await LoginAsync(client, account.Email, account.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await ReadAsync(response);
        Assert.Equal(account.AccountId, me.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("employeeId").ValueKind);
        Assert.Equal("TST- Test Person", me.GetProperty("displayName").GetString());
        Assert.Equal(account.Email, me.GetProperty("email").GetString());
        Assert.Equal(TestAccounts.AdministratorRole, me.GetProperty("roleName").GetString());
        var permissions = me.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToList();
        Assert.Contains("organization:View", permissions);
        Assert.Contains("employees:ViewBank", permissions);
        Assert.Contains("settings:Delete", permissions);

        var cookie = SetCookieOf(response, SessionCookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        Assert.DoesNotContain("password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [IntegrationFact]
    public async Task Me_returns_the_signed_in_account_and_records_the_sign_in_time_without_touching_the_audit_columns()
    {
        var account = await AccountAsync("me");
        var client = NewClient();
        await LoginAsync(client, account.Email, account.Password);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(account.AccountId, (await ReadAsync(response)).GetProperty("id").GetInt32());
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE Id = @id AND LastLoginAtUtc IS NOT NULL", ("@id", account.AccountId)));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM sec.AppUser WHERE Id = @id AND ModifiedByUserId IS NULL", ("@id", account.AccountId)));
    }

    [IntegrationTheory]
    [InlineData("unknown")]
    [InlineData("wrong-password")]
    [InlineData("inactive")]
    [InlineData("locked")]
    [InlineData("inactive-role")]
    [InlineData("not-linked")]
    public async Task Every_failed_sign_in_is_the_same_generic_401(string reason)
    {
        var email = TestAccounts.EmailFor(Tag, reason);
        var password = TestAccounts.NewPassword();
        switch (reason)
        {
            case "wrong-password":
                await TestAccounts.CreateAsync(email);
                password = "this is not the password";
                break;
            case "inactive":
                await TestAccounts.CreateAsync(email, password, status: "Inactive");
                break;
            case "locked":
                await TestAccounts.CreateAsync(email, password, status: "Locked");
                break;
            case "inactive-role":
                await TestAccounts.CreateInactiveRoleAsync($"{Tag}-role");
                await TestAccounts.CreateAsync(email, password, roleName: $"{Tag}-role");
                break;
            case "not-linked":
                await TestAccounts.CreateAsync(email, password, linkAccount: false);
                break;
        }

        var response = await LoginAsync(NewClient(), email, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Unauthorized", body.GetProperty("code").GetString());
        Assert.Equal(AuthFlows.GenericSignInFailure, body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("errors", out _));
        Assert.DoesNotContain(response.Headers, header => header.Key == "Set-Cookie" && header.Value.Any(value => value.StartsWith(SessionCookie + "=", StringComparison.Ordinal)));
    }

    [IntegrationFact]
    public async Task A_correct_password_for_an_inactive_account_is_refused_with_the_same_answer_as_a_wrong_one()
    {
        var inactive = await AccountAsync("inactive-same", status: "Inactive");
        var active = await AccountAsync("active-same");

        var refused = await LoginAsync(NewClient(), inactive.Email, inactive.Password);
        var wrong = await LoginAsync(NewClient(), active.Email, "wrong wrong wrong wrong");

        Assert.Equal(refused.StatusCode, wrong.StatusCode);
        Assert.Equal(
            (await ReadAsync(refused)).GetProperty("detail").GetString(),
            (await ReadAsync(wrong)).GetProperty("detail").GetString());
    }

    [IntegrationFact]
    public async Task Login_without_an_email_or_password_is_a_400_with_field_errors()
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/login", new { email = "", password = "", rememberMe = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    // ---- Lockout ----

    [IntegrationFact]
    public async Task Five_failed_sign_ins_lock_the_account_for_fifteen_minutes()
    {
        var account = await AccountAsync("lockout");
        var client = NewClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var failed = await LoginAsync(client, account.Email, "wrong wrong wrong wrong");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        // Locked now: even the right password is refused, with the same generic answer.
        var locked = await LoginAsync(client, account.Email, account.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(AuthFlows.GenericSignInFailure, (await ReadAsync(locked)).GetProperty("detail").GetString());

        var minutesLeft = await Sql.ScalarAsync<int>(
            "SELECT DATEDIFF(MINUTE, SYSDATETIMEOFFSET(), LockoutEnd) FROM auth.[User] WHERE Id = @id", ("@id", account.AuthUserId));
        Assert.InRange(minutesLeft, 14, 15);

        // The lock ends by itself: move its end into the past instead of waiting.
        await Sql.ExecuteAsync("UPDATE auth.[User] SET LockoutEnd = DATEADD(MINUTE, -1, SYSDATETIMEOFFSET()) WHERE Id = @id", ("@id", account.AuthUserId));
        var unlocked = await LoginAsync(client, account.Email, account.Password);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
    }

    [IntegrationFact]
    public async Task A_successful_sign_in_resets_the_failure_count()
    {
        var account = await AccountAsync("reset-count");
        var client = NewClient();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await LoginAsync(client, account.Email, "wrong wrong wrong wrong");
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, account.Email, account.Password)).StatusCode);

        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT AccessFailedCount FROM auth.[User] WHERE Id = @id", ("@id", account.AuthUserId)));
    }

    [IntegrationFact]
    public async Task The_sign_in_endpoints_are_rate_limited_per_ip()
    {
        using var limited = new ApiFactory(loginPermitsPerMinute: 3);
        using var client = limited.CreateBrowserClient(out _);
        var email = TestAccounts.EmailFor(Tag, "ratelimit");

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            statuses.Add((await LoginAsync(client, email, "wrong wrong wrong wrong")).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    // ---- Sessions ----

    [IntegrationFact]
    public async Task Logout_is_204_and_ends_the_session()
    {
        var account = await AccountAsync("logout");
        var client = NewClient();
        await LoginAsync(client, account.Email, account.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [IntegrationFact]
    public async Task Without_remember_me_the_cookie_is_a_session_cookie_and_with_it_a_persistent_one_for_seven_days()
    {
        var account = await AccountAsync("remember");

        var session = await LoginAsync(NewClient(), account.Email, account.Password, rememberMe: false);
        var persistent = await LoginAsync(NewClient(), account.Email, account.Password, rememberMe: true);

        var sessionCookie = SetCookieOf(session, SessionCookie);
        Assert.DoesNotContain("expires=", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age=", sessionCookie, StringComparison.OrdinalIgnoreCase);

        var persistentCookie = SetCookieOf(persistent, SessionCookie);
        var expires = DateTimeOffset.Parse(ExpiresAttribute().Match(persistentCookie).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expires, DateTimeOffset.UtcNow.AddDays(7).AddHours(-1), DateTimeOffset.UtcNow.AddDays(7).AddHours(1));
    }

    [IntegrationFact]
    public async Task Deactivating_the_account_ends_an_open_session()
    {
        var account = await AccountAsync("deactivate");
        var client = NewClient();
        await LoginAsync(client, account.Email, account.Password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);

        await Sql.ExecuteAsync("UPDATE sec.AppUser SET Status = 'Inactive' WHERE Id = @id", ("@id", account.AccountId));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    // ---- Antiforgery and anonymous writes ----

    [IntegrationFact]
    public async Task A_write_without_the_antiforgery_header_is_a_400_and_writes_nothing()
    {
        var account = await AccountAsync("noxsrf");
        var client = NewClient(sendXsrfHeader: false);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email = account.Email, password = account.Password, rememberMe = false }) };
        login.Headers.Add("X-XSRF-TOKEN", await TokenOf(client));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(login)).StatusCode);

        var response = await client.PostAsJsonAsync("/api/companies", Requests.Company());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Code = @code", ("@code", Code("A"))));
    }

    [IntegrationFact]
    public async Task An_anonymous_write_is_a_401_and_writes_nothing()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/companies", Requests.Company());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Code = @code", ("@code", Code("A"))));
    }

    [IntegrationFact]
    public async Task An_anonymous_read_is_a_401()
    {
        var response = await NewClient().GetAsync("/api/companies/query?pageIndex=0&pageSize=5");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string> TokenOf(HttpClient client)
    {
        // Read the token the way the frontend does: from the readable cookie, through the browser handler's jar.
        var response = await client.GetAsync("/api/auth/antiforgery");
        var cookie = SetCookieOf(response, "XSRF-TOKEN");
        return Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';', StringComparison.Ordinal)]);
    }

    // ---- Audit attribution ----

    [IntegrationFact]
    public async Task Audit_columns_hold_the_id_of_the_signed_in_account()
    {
        var author = await AccountAsync("author");
        var editor = await AccountAsync("editor");
        var authorClient = NewClient();
        var editorClient = NewClient();
        await authorClient.SignInAsync(author);
        await editorClient.SignInAsync(editor);

        var created = await authorClient.PostAsJsonAsync("/api/companies", Requests.Company());
        created.EnsureSuccessStatusCode();
        var company = (await created.Content.ReadFromJsonAsync<CompanyDto>())!;
        (await editorClient.PutAsJsonAsync($"/api/companies/{company.Id}", Requests.CompanyUpdate(company, city: "Mandalay"))).EnsureSuccessStatusCode();

        Assert.Equal(author.AccountId, await Sql.ScalarAsync<int>("SELECT CreatedByUserId FROM org.Company WHERE Id = @id", ("@id", company.Id)));
        Assert.Equal(editor.AccountId, await Sql.ScalarAsync<int>("SELECT ModifiedByUserId FROM org.Company WHERE Id = @id", ("@id", company.Id)));
    }

    // ---- Password reset ----

    [IntegrationFact]
    public async Task Forgot_password_answers_the_same_for_known_unknown_and_inactive_addresses_and_mails_only_the_active_account()
    {
        var known = await AccountAsync("forgot-known");
        var inactive = await AccountAsync("forgot-inactive", status: "Inactive");
        var client = NewClient();

        var knownResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = known.Email });
        Assert.Single(MailFiles());
        await ResetTokenFromMailAsync(known.Email);
        var unknownResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = TestAccounts.EmailFor(Tag, "nobody") });
        var inactiveResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = inactive.Email });

        Assert.Equal(HttpStatusCode.OK, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
        var expected = await knownResponse.Content.ReadAsStringAsync();
        Assert.Equal(expected, await unknownResponse.Content.ReadAsStringAsync());
        Assert.Equal(expected, await inactiveResponse.Content.ReadAsStringAsync());
        Assert.Contains(AuthFlows.ForgotPasswordAnswer, expected, StringComparison.Ordinal);
        Assert.Empty(MailFiles());
    }

    [IntegrationFact]
    public async Task Reset_password_with_a_valid_token_changes_the_password_once_and_ends_open_sessions()
    {
        var account = await AccountAsync("reset");
        var openSession = NewClient();
        await openSession.SignInAsync(account);
        var client = NewClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = account.Email });
        var token = await ResetTokenFromMailAsync(account.Email);
        var newPassword = TestAccounts.NewPassword();

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = account.Email, token, newPassword });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await openSession.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), account.Email, account.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), account.Email, newPassword)).StatusCode);

        // The token works once.
        var again = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = account.Email, token, newPassword = TestAccounts.NewPassword() });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [IntegrationFact]
    public async Task Reset_password_fails_the_same_way_for_a_bad_token_and_an_unknown_address()
    {
        var account = await AccountAsync("reset-bad");
        var client = NewClient();

        var badToken = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = account.Email, token = "not-a-valid-token", newPassword = TestAccounts.NewPassword() });
        var unknown = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = TestAccounts.EmailFor(Tag, "nobody"), token = "not-a-valid-token", newPassword = TestAccounts.NewPassword() });

        Assert.Equal(HttpStatusCode.BadRequest, badToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        var badBody = await ReadAsync(badToken);
        Assert.Equal(AuthFlows.InvalidResetMessage, badBody.GetProperty("detail").GetString());
        Assert.Equal(AuthFlows.InvalidResetMessage, (await ReadAsync(unknown)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), account.Email, account.Password)).StatusCode);
    }

    [IntegrationFact]
    public async Task Reset_password_rejects_a_short_password_before_looking_at_the_account()
    {
        var account = await AccountAsync("reset-short");
        var client = NewClient();

        var known = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = account.Email, token = "x", newPassword = "short" });
        var unknown = await client.PostAsJsonAsync("/api/auth/reset-password", new { email = TestAccounts.EmailFor(Tag, "nobody"), token = "x", newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, known.StatusCode);
        Assert.True((await ReadAsync(known)).GetProperty("errors").TryGetProperty("newPassword", out _));
        Assert.Equal(Strip(await known.Content.ReadAsStringAsync()), Strip(await unknown.Content.ReadAsStringAsync()));
    }

    // The trace id differs per response; everything else must be identical.
    private static string Strip(string json) => TraceId().Replace(json, string.Empty);

    [GeneratedRegex("\"traceId\":\"[^\"]*\"")]
    private static partial Regex TraceId();
}
