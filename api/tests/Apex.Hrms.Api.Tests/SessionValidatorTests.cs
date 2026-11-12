using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The per-request session checks that end a session: past the absolute maximum, no longer Active, or malformed. The
/// security stamp check needs the identity store and is covered by the integration tests (password reset ends sessions).
/// </summary>
public sealed class SessionValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeAccounts(AccountSnapshot? account) : IAccountDirectory
    {
        public int Lookups { get; private set; }

        public Task<AccountSnapshot?> FindByExternalSubjectAsync(string externalSubject, bool includePermissions, CancellationToken cancellationToken) =>
            Task.FromResult(account);

        public Task<AccountSnapshot?> FindByIdAsync(int accountId, bool includePermissions, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(account);
        }

        public Task RecordSignInAsync(int accountId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingAuthentication : IAuthenticationService
    {
        public int SignOuts { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOuts++;
            return Task.CompletedTask;
        }
    }

    private static AccountSnapshot Snapshot(AccountStatus status = AccountStatus.Active, bool roleActive = true, bool employeeLeft = false) =>
        new(7, null, "TST- Test Person", "tst@example.test", "subject-1", status, 1, "HR Administrator", roleActive, employeeLeft, []);

    private static (CookieValidatePrincipalContext Context, FakeAccounts Accounts, RecordingAuthentication Authentication) Arrange(
        DateTimeOffset now,
        AccountSnapshot? account,
        DateTimeOffset? absoluteExpiry,
        bool withClaims = true)
    {
        var accounts = new FakeAccounts(account);
        var authentication = new RecordingAuthentication();
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTime(now))
            .AddSingleton<IAccountDirectory>(accounts)
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };

        var claims = withClaims
            ? new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "subject-1"),
                new Claim(HrmsClaims.AccountId, "7"),
                new Claim(HrmsClaims.SecurityStamp, "stamp-1"),
            }
            : [new Claim(ClaimTypes.NameIdentifier, "subject-1")];
        var properties = new AuthenticationProperties();
        if (absoluteExpiry is { } limit)
        {
            properties.Items[HrmsClaims.AbsoluteExpiryProperty] = limit.ToString("O", CultureInfo.InvariantCulture);
        }

        var scheme = new AuthenticationScheme("test", null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), properties, "test");
        return (new CookieValidatePrincipalContext(http, scheme, new CookieAuthenticationOptions(), ticket), accounts, authentication);
    }

    [Fact]
    public async Task A_session_past_its_absolute_maximum_is_rejected_and_signed_out_without_a_lookup()
    {
        var (context, accounts, authentication) = Arrange(Now, Snapshot(), absoluteExpiry: Now.AddSeconds(-1));

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authentication.SignOuts);
        Assert.Equal(0, accounts.Lookups);
    }

    [Theory]
    [InlineData(AccountStatus.Inactive, true)]
    [InlineData(AccountStatus.Locked, true)]
    [InlineData(AccountStatus.Active, false)]
    public async Task A_session_of_an_account_that_can_no_longer_sign_in_is_rejected(AccountStatus status, bool roleActive)
    {
        var (context, _, authentication) = Arrange(Now, Snapshot(status, roleActive), absoluteExpiry: Now.AddHours(1));

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authentication.SignOuts);
    }

    [Fact]
    public async Task A_session_of_an_account_whose_employee_has_left_is_rejected()
    {
        var (context, _, authentication) = Arrange(Now, Snapshot(employeeLeft: true), absoluteExpiry: Now.AddHours(1));

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authentication.SignOuts);
    }

    [Fact]
    public async Task A_session_whose_account_no_longer_exists_is_rejected()
    {
        var (context, _, authentication) = Arrange(Now, account: null, absoluteExpiry: null);

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authentication.SignOuts);
    }

    [Fact]
    public async Task A_cookie_without_the_hrms_claims_is_rejected()
    {
        var (context, _, authentication) = Arrange(Now, Snapshot(), absoluteExpiry: null, withClaims: false);

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
        Assert.Equal(1, authentication.SignOuts);
    }

    [Fact]
    public async Task A_garbled_absolute_limit_is_rejected_rather_than_trusted()
    {
        var (context, _, _) = Arrange(Now, Snapshot(), absoluteExpiry: null);
        context.Properties.Items[HrmsClaims.AbsoluteExpiryProperty] = "not a date";

        await SessionValidator.ValidateAsync(context);

        Assert.Null(context.Principal);
    }
}
