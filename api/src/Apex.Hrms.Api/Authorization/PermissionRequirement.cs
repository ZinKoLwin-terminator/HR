using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Microsoft.AspNetCore.Authorization;

namespace Apex.Hrms.Api.Authorization;

/// <summary>
/// The caller must hold one permission, for example <c>employees:ViewBank</c>. One requirement is registered per module and
/// action of <see cref="PermissionModule.All"/>, under the policy name <c>module:Action</c>.
/// </summary>
internal class PermissionRequirement(string module, string action) : IAuthorizationRequirement
{
    public string Module { get; } = module;

    public string Action { get; } = action;

    public string Key => $"{Module}:{Action}";

    /// <summary>Other keys (<c>module:Action</c>) that also satisfy the requirement; none for an ordinary permission.</summary>
    public virtual IReadOnlyList<string> Alternatives => [];
}

/// <summary>
/// Checks a permission against the database on every request. The account (status, role, role status, employee exit) is read
/// live, never from the cookie, so a deactivated user, an inactive role or a leaver loses access at once. The grants are the
/// rows of sec.RolePermission for the role of that account; they come through <see cref="RoleGrantCache"/>, which holds a
/// role's grants while the role row version is unchanged. The held grants are also handed to the request's <see cref="CallerPermissions"/>.
/// HR Administrator has no special path: it is allowed because its role holds every grant in the seed. Anything unclear
/// (no account id claim, no account, an inactive account, an action that is not part of the permission model) is refused.
/// </summary>
internal sealed class PermissionHandler(
    IAccountDirectory accounts,
    IPermissionStore grants,
    RoleGrantCache cache,
    CallerPermissions caller,
    ITeamDirectory teams,
    CallerTeam team,
    IRecruitmentDirectory recruitment,
    CallerRecruitment callerRecruitment)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!IsKnown(requirement))
        {
            context.Fail();
            return;
        }

        var idText = context.User.FindFirstValue(HrmsClaims.AccountId);
        if (!int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId))
        {
            return;
        }

        var account = await accounts.FindByIdAsync(accountId, includePermissions: false, CancellationToken.None);
        if (account is not { CanSignIn: true })
        {
            return;
        }

        var held = await cache.GetAsync(account.RoleId, account.RoleRowVersion ?? [], LoadAsync(account.RoleId), CancellationToken.None);

        // The access scopes of the request (own rows or everything) read the same grants.
        caller.Set(held);

        // ... and the direct reports of the caller (read once per request), which the manager tier of those scopes needs.
        if (account.EmployeeId is { } employeeId && !team.IsLoaded)
        {
            team.Set(await teams.DirectReportIdsAsync(employeeId, CancellationToken.None));
        }

        // Recruitment: a caller who may view but not edit (a hiring manager) sees only the openings they manage, which the
        // recruitment scopes need (read once per request, and only on recruitment routes).
        if (requirement.Module == "recruitment"
            && account.EmployeeId is { } hiringManagerId
            && !callerRecruitment.IsLoaded
            && !held.Contains("recruitment:Edit"))
        {
            callerRecruitment.Set(await recruitment.ManagedByAsync(hiringManagerId, CancellationToken.None));
        }

        if (held.Contains(requirement.Key) || requirement.Alternatives.Any(held.Contains))
        {
            context.Succeed(requirement);
        }
    }

    private static bool IsKnown(PermissionRequirement requirement) =>
        PermissionModule.All.Any(module =>
            module.Key == requirement.Module
            && module.Permissions.Any(permission => permission.ToString() == requirement.Action));

    private Func<CancellationToken, Task<IReadOnlySet<string>>> LoadAsync(int roleId) => async cancellationToken =>
    {
        var rows = await grants.GrantsAsync(roleId, cancellationToken);
        return rows.Select(grant => $"{grant.ModuleKey}:{grant.Action}").ToHashSet(StringComparer.Ordinal);
    };
}

/// <summary>
/// A requirement met by any one of several permissions, for a route used by two kinds of caller (the employee picker of the
/// job opening form: <c>recruitment:Edit</c> or <c>employees:View</c>). The same handler checks it, live, like every permission.
/// </summary>
internal sealed class AnyPermissionRequirement(string module, string action, params string[] alternatives) : PermissionRequirement(module, action)
{
    public override IReadOnlyList<string> Alternatives { get; } = alternatives;
}
