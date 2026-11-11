using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Application.Security;
using Microsoft.AspNetCore.Authorization;

namespace Apex.Hrms.Api.Authorization;

/// <summary>
/// The caller must be an HR Administrator. This is a real check, not a placeholder: it reads the account and its role from
/// the database on every request (never from a cookie claim), so a demoted, deactivated or locked administrator loses
/// access at once. The built-in administrator (no employee) holds the same role and passes the same check.
/// </summary>
internal sealed class AdministratorRequirement : IAuthorizationRequirement
{
}

internal sealed class AdministratorHandler(IAccountDirectory accounts) : AuthorizationHandler<AdministratorRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement)
    {
        var idText = context.User.FindFirstValue(HrmsClaims.AccountId);
        if (!int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId))
        {
            return;
        }

        var account = await accounts.FindByIdAsync(accountId, includePermissions: false, CancellationToken.None);
        if (account is { CanSignIn: true } && account.RoleName == SecurityPolicy.AdministratorRoleName)
        {
            context.Succeed(requirement);
        }
    }
}
