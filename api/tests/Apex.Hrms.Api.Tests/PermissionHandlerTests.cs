using System.Security.Claims;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The permission check on its own, without a host: grants come from the role of the account (read live), nothing is taken
/// from the cookie, and the short cache is emptied when the Settings handlers say a role changed.
/// </summary>
public sealed class PermissionHandlerTests
{
    private sealed class MovableTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly MovableTime time = new();
    private readonly RoleGrantCache cache;
    private CallerPermissions Caller { get; } = new();
    private CallerTeam Team { get; } = new();
    private readonly FakeTeamDirectory teams = new(new Dictionary<int, int[]> { [7] = [11, 12] });

    private readonly FakeGrantStore store = new(["employees:View", "leave:View", "leave:Approve"]);

    public PermissionHandlerTests() => cache = new RoleGrantCache(time);

    private async Task<AuthorizationHandlerContext> RunAsync(
        PermissionRequirement requirement,
        AccountSnapshot? account,
        bool withAccountClaim = true)
    {
        var handler = new PermissionHandler(new FakeAccountDirectory(account), store, cache, Caller, teams, Team);
        var claims = withAccountClaim ? new[] { new Claim(HrmsClaims.AccountId, AccessFakes.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture)) } : [];
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        await handler.HandleAsync(context);
        return context;
    }

    private static PermissionRequirement Needs(string module, string action) => new(module, action);

    [Fact]
    public async Task A_granted_action_succeeds()
    {
        var context = await RunAsync(Needs("employees", "View"), AccessFakes.Account("HR Officer"));

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task An_action_the_role_does_not_hold_is_refused_even_when_it_holds_another_action_of_the_module()
    {
        var context = await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account("HR Officer"));

        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task An_inactive_role_is_refused_although_it_still_holds_the_grant()
    {
        var context = await RunAsync(Needs("employees", "View"), AccessFakes.Account("HR Officer", roleActive: false));

        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData(AccountStatus.Inactive)]
    [InlineData(AccountStatus.Locked)]
    public async Task An_account_that_is_not_active_is_refused_although_its_role_holds_the_grant(AccountStatus status)
    {
        var context = await RunAsync(Needs("employees", "View"), AccessFakes.Account("HR Officer", status));

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task An_account_whose_employee_has_left_is_refused()
    {
        var context = await RunAsync(Needs("employees", "View"), AccessFakes.Account("HR Officer", employeeLeft: true));

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task A_missing_account_or_a_missing_account_claim_is_refused()
    {
        Assert.False((await RunAsync(Needs("employees", "View"), account: null)).HasSucceeded);
        Assert.False((await RunAsync(Needs("employees", "View"), AccessFakes.Account(), withAccountClaim: false)).HasSucceeded);
    }

    [Theory]
    [InlineData("timesheets", "View")]
    [InlineData("payroll", "Delete")]
    [InlineData("employees", "Approve")]
    [InlineData("employees", "view")]
    [InlineData("", "")]
    public async Task A_policy_that_is_not_part_of_the_permission_model_is_refused_even_if_a_grant_row_names_it(string module, string action)
    {
        store.Grants.Add($"{module}:{action}");

        var context = await RunAsync(Needs(module, action), AccessFakes.Account());

        Assert.False(context.HasSucceeded);
        Assert.True(context.HasFailed);
    }

    [Fact]
    public async Task Grants_are_cached_per_role_while_the_row_version_is_unchanged_and_read_again_after_the_safety_lifetime()
    {
        await RunAsync(Needs("employees", "View"), AccessFakes.Account());
        await RunAsync(Needs("leave", "Approve"), AccessFakes.Account());
        Assert.Equal(1, store.Reads);

        time.Now += RoleGrantCache.Lifetime + TimeSpan.FromSeconds(1);
        await RunAsync(Needs("employees", "View"), AccessFakes.Account());

        Assert.Equal(2, store.Reads);
        Assert.True(RoleGrantCache.Lifetime <= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task A_grant_change_is_seen_at_once_after_the_cache_is_invalidated_and_not_before()
    {
        Assert.False((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account())).HasSucceeded);

        store.Grants.Add("employees:ViewBank");
        Assert.False((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account())).HasSucceeded);

        cache.Invalidate(AccessFakes.RoleId + 1);
        Assert.False((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account())).HasSucceeded);

        cache.Invalidate(AccessFakes.RoleId);
        Assert.True((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account())).HasSucceeded);

        store.Grants.Remove("employees:ViewBank");
        cache.InvalidateAll();
        Assert.False((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account())).HasSucceeded);
    }

    [Fact]
    public async Task A_deactivated_account_loses_access_at_once_because_the_account_is_never_cached()
    {
        Assert.True((await RunAsync(Needs("employees", "View"), AccessFakes.Account())).HasSucceeded);

        Assert.False((await RunAsync(Needs("employees", "View"), AccessFakes.Account(status: AccountStatus.Inactive))).HasSucceeded);
    }

    [Fact]
    public async Task A_new_role_row_version_reads_the_grants_again_without_any_invalidation()
    {
        await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account(roleVersion: [1]));
        Assert.False((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account(roleVersion: [1]))).HasSucceeded);
        Assert.Equal(1, store.Reads);

        // Another instance saved the grants: the role row (and so its row version) moved, this instance never heard of it.
        store.Grants.Add("employees:ViewBank");
        Assert.False((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account(roleVersion: [1]))).HasSucceeded);

        Assert.True((await RunAsync(Needs("employees", "ViewBank"), AccessFakes.Account(roleVersion: [2]))).HasSucceeded);
        Assert.Equal(2, store.Reads);
    }

    [Fact]
    public async Task The_held_grants_are_handed_to_the_request_so_the_access_scopes_can_tell_an_approver()
    {
        await RunAsync(Needs("employees", "View"), AccessFakes.Account());

        Assert.True(Caller.Has("leave", "Approve"));
        Assert.False(Caller.Has("attendance", "Approve"));
    }

    [Fact]
    public async Task The_direct_reports_of_the_callers_employee_are_handed_to_the_request_once_for_several_checks()
    {
        await RunAsync(Needs("employees", "View"), AccessFakes.Account(employeeId: 7));
        await RunAsync(Needs("leave", "Approve"), AccessFakes.Account(employeeId: 7));

        Assert.Equal([11, 12], Team.DirectReportIds);
        Assert.Equal(1, teams.Lookups);
    }

    [Fact]
    public async Task An_account_without_an_employee_has_no_team_and_none_is_looked_up()
    {
        await RunAsync(Needs("employees", "View"), AccessFakes.Account());

        Assert.Empty(Team.DirectReportIds);
        Assert.Equal(0, teams.Lookups);
    }

    [Fact]
    public void Every_permission_of_every_module_has_a_registered_requirement_policy()
    {
        var registered = new ServiceCollection().AddHrmsAuthorization().BuildServiceProvider()
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>().Value;

        foreach (var module in PermissionModule.All)
        {
            foreach (var permission in module.Permissions)
            {
                var policy = registered.GetPolicy(module.PolicyName(permission));
                Assert.NotNull(policy);
                var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
                Assert.Equal(module.PolicyName(permission), requirement.Key);
            }
        }

    }
}
