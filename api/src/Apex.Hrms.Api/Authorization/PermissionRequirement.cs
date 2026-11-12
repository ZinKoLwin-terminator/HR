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
internal sealed class PermissionRequirement(string module, string action) : IAuthorizationRequirement
{
    public string Module { get; } = module;

    public string Action { get; } = action;

    public string Key => $"{Module}:{Action}";
}

/// <summary>
/// Checks a permission against the database on every request. The account (status, role, role status, employee exit) is read
/// live, never from the cookie, so a deactivated user, an inactive role or a leaver loses access at once. The grants are the
/// rows of sec.RolePermission for the role of that account; they come through <see cref="RoleGrantCache"/>, which holds a
/// role's grants for at most <see cref="RoleGrantCache.Lifetime"/> and is emptied when the Settings handlers change them.
/// HR Administrator has no special path: it is allowed because its role holds every grant in the seed. Anything unclear
/// (no account id claim, no account, an inactive account, an action that is not part of the permission model) is refused.
/// </summary>
internal sealed class PermissionHandler(IAccountDirectory accounts, IPermissionStore grants, RoleGrantCache cache)
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

        var held = await cache.GetAsync(account.RoleId, LoadAsync(account.RoleId), CancellationToken.None);
        if (held.Contains(requirement.Key))
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
