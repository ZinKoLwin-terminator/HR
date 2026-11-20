using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// <c>GET /api/organization/lookup</c> through the real pipeline without a database: anonymous callers get 401, any signed-in
/// caller gets the names (no module grant needed) in the contract's camelCase shape, and a real session cookie is checked by the
/// session validator, which reads the account live on every request: an account that can no longer sign in is refused at the
/// account check (the identity user store is never reached), an active one gets the names. The handler, the account directory
/// and the identity user store are fakes with fictional rows, so nothing here reads a database.
/// </summary>
public sealed class OrganizationLookupHttpTests(WebApplicationFactory<Program> fixture) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/api/organization/lookup";

    private const string Subject = "tst-subject";

    private const string Stamp = "tst-stamp";

    private static WebApplicationFactory<Program> WithFakeLookup(WebApplicationFactory<Program> host) =>
        host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IOrganizationLookupHandler, FakeLookup>()));

    private static string[] PropertiesOf(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];

    [Fact]
    public async Task An_anonymous_caller_gets_a_401_problem()
    {
        var host = SignedInHost.Cached(fixture, "development", factory => factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_signed_in_caller_whose_role_holds_no_grant_gets_the_names_in_the_contract_shape()
    {
        var host = WithFakeLookup(SignedInHost.Customize(fixture, AccessFakes.Account("Employee"), grants: []));
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["companies", "branches", "departments", "sections", "positions", "jobGrades"], PropertiesOf(body));
        Assert.Equal(["id", "code", "name", "status"], PropertiesOf(body.GetProperty("companies")[0]));
        Assert.Equal(["id", "companyId", "code", "name", "status"], PropertiesOf(body.GetProperty("branches")[0]));
        Assert.Equal(["id", "companyId", "branchId", "code", "name", "status"], PropertiesOf(body.GetProperty("departments")[0]));
        Assert.Equal(["id", "departmentId", "code", "name", "status"], PropertiesOf(body.GetProperty("sections")[0]));
        Assert.Equal(["id", "departmentId", "sectionId", "jobGradeId", "code", "title", "status"], PropertiesOf(body.GetProperty("positions")[0]));
        Assert.Equal(["id", "code", "name", "level", "status"], PropertiesOf(body.GetProperty("jobGrades")[0]));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("positions")[0].GetProperty("sectionId").ValueKind);
        Assert.Equal("Inactive", body.GetProperty("departments")[0].GetProperty("status").GetString());
        Assert.Equal(3, body.GetProperty("jobGrades")[0].GetProperty("level").GetInt32());
    }

    [Theory]
    [InlineData(AccountStatus.Inactive, true, false)]
    [InlineData(AccountStatus.Locked, true, false)]
    [InlineData(AccountStatus.Active, false, false)]
    [InlineData(AccountStatus.Active, true, true)]
    public async Task A_session_cookie_of_an_account_that_can_no_longer_sign_in_is_refused_by_the_account_check(AccountStatus status, bool roleActive, bool employeeLeft)
    {
        var (response, accounts, identities) = await SendWithSessionCookieAsync(status, roleActive, employeeLeft);

        Assert.Equal(HttpStatusCode.Unauthorized, response);

        // The cookie was read and the validator looked the account up live (so the 401 is not an unreadable cookie) ...
        Assert.Equal(1, accounts.Lookups);

        // ... and refused there: it never went on to the identity user and its security stamp.
        Assert.Equal(0, identities.Finds);
    }

    [Fact]
    public async Task A_session_cookie_of_an_active_account_gets_the_names()
    {
        var (response, accounts, identities) = await SendWithSessionCookieAsync(AccountStatus.Active, roleActive: true, employeeLeft: false);

        Assert.Equal(HttpStatusCode.OK, response);
        Assert.Equal(1, accounts.Lookups);
        Assert.Equal(1, identities.Finds);
    }

    /// <summary>
    /// Sends the lookup with a real session cookie through the real cookie scheme and session validator. The account directory and
    /// the identity user store are fakes (the store holds the cookie's user with the cookie's security stamp), so nothing reads a database.
    /// </summary>
    private static async Task<(HttpStatusCode Status, FakeAccountDirectory Accounts, CountingUserStore Identities)> SendWithSessionCookieAsync(
        AccountStatus status,
        bool roleActive,
        bool employeeLeft)
    {
        var keys = Path.Combine(Path.GetTempPath(), "apex-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            var accounts = new FakeAccountDirectory(
                new AccountSnapshot(AccessFakes.AccountId, null, "TST- Test User", "tst@example.test", Subject, status, AccessFakes.RoleId, "Employee", roleActive, employeeLeft, []));
            var identities = new CountingUserStore();
            using var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("DataProtection:KeysPath", keys);
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IAccountDirectory>(accounts);
                    services.AddSingleton<IUserStore<IdentityUser>>(identities);
                    services.AddScoped<IOrganizationLookupHandler, FakeLookup>();
                });
            });
            using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

            using var request = new HttpRequestMessage(HttpMethod.Get, Route);
            request.Headers.Add("Cookie", SessionCookie(host));
            using var response = await client.SendAsync(request);
            return (response.StatusCode, accounts, identities);
        }
        finally
        {
            if (Directory.Exists(keys))
            {
                Directory.Delete(keys, recursive: true);
            }
        }
    }

    /// <summary>A session cookie as sign-in issues it (the claims the session validator reads), protected by the host's own cookie options.</summary>
    private static string SessionCookie(WebApplicationFactory<Program> host)
    {
        var options = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Subject),
                new Claim(ClaimTypes.Name, "TST- Test User"),
                new Claim(HrmsClaims.AccountId, AccessFakes.AccountId.ToString(CultureInfo.InvariantCulture)),
                new Claim(HrmsClaims.SecurityStamp, Stamp),
            ],
            HrmsClaims.AuthenticationType);
        var now = DateTimeOffset.UtcNow;
        var properties = new AuthenticationProperties { IssuedUtc = now, ExpiresUtc = now.AddMinutes(30) };
        properties.Items[HrmsClaims.AbsoluteExpiryProperty] = now.AddHours(8).ToString("O", CultureInfo.InvariantCulture);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, IdentityConstants.ApplicationScheme);
        return $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}";
    }

    /// <summary>
    /// The identity users as the session validator reads them (<c>UserManager.FindByIdAsync</c>, then the security stamp): one user,
    /// the cookie's subject with the cookie's stamp. Counts the lookups; anything that would write is refused.
    /// </summary>
    private sealed class CountingUserStore : IUserSecurityStampStore<IdentityUser>
    {
        private readonly IdentityUser user = new() { Id = Subject, UserName = "tst-user", SecurityStamp = Stamp };

        public int Finds { get; private set; }

        public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            Finds++;
            return Task.FromResult(userId == Subject ? user : null);
        }

        public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<IdentityUser?>(null);

        public Task<string?> GetSecurityStampAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);

        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);

        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);

        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);

        public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }

    /// <summary>One fictional row per list: an inactive department and a position without a section.</summary>
    private sealed class FakeLookup : IOrganizationLookupHandler
    {
        public Task<Result<OrganizationLookupDto>> HandleAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(new OrganizationLookupDto(
                [new CompanyLookupDto(1, "TST", "TST- Test Company", "Active")],
                [new BranchLookupDto(1, 1, "TBR", "TST- Test Branch", "Active")],
                [new DepartmentLookupDto(2, 1, 1, "TDP", "TST- Test Department", "Inactive")],
                [new SectionLookupDto(1, 2, "TSC", "TST- Test Section", "Active")],
                [new PositionLookupDto(1, 2, null, 3, "TPS", "TST- Test Position", "Active")],
                [new JobGradeLookupDto(3, "TG3", "TST- Test Grade", 3, "Active")])));
    }
}
