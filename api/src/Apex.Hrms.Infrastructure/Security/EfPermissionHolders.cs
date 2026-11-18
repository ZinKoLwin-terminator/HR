using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Infrastructure.Persistence;

namespace Apex.Hrms.Infrastructure.Security;

/// <summary>
/// The employees whose account can sign in (the rule of <see cref="ReachableAccounts"/>) and whose role holds the grant. The built-in
/// administrator has no employee and is never listed.
/// </summary>
internal sealed class EfPermissionHolders(HrmsDbContext context) : IPermissionHolders
{
    public IQueryable<int> EmployeesHolding(string moduleKey, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var grants = context.Set<RoleGrant>().Where(grant => grant.ModuleKey == moduleKey && grant.Action == action);
        return ReachableAccounts.Of(context)
            .Where(account => account.EmployeeId != null && grants.Any(grant => grant.RoleId == account.RoleId))
            .Select(account => account.EmployeeId!.Value);
    }
}
