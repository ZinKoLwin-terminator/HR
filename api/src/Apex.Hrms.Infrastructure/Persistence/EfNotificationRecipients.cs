using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Reads who a notification goes to from sec.AppUser, sec.Role and sec.RolePermission. Only accounts that could pass the
/// permission check count, by the same rule as <c>AccountSnapshot.CanSignIn</c>: the account and its role are Active and the
/// linked employee (if any) has not left.
/// </summary>
internal sealed class EfNotificationRecipients(HrmsDbContext context) : INotificationRecipients
{
    public async Task<int?> UserIdOfEmployeeAsync(int employeeId, CancellationToken cancellationToken)
    {
        var ids = await EmployeeAccountQuery(employeeId).Take(1).ToListAsync(cancellationToken);
        return ids.Count == 0 ? null : ids[0];
    }

    public async Task<IReadOnlyList<int>> UserIdsHoldingAsync(
        string moduleKey,
        string action,
        IReadOnlyCollection<int> excludedEmployeeIds,
        CancellationToken cancellationToken) =>
        await HoldersQuery(moduleKey, action, excludedEmployeeIds).ToListAsync(cancellationToken);

    /// <summary>The account ids of the employee that can sign in (at most one: sec.AppUser has one account per employee).</summary>
    internal IQueryable<int> EmployeeAccountQuery(int employeeId) =>
        ReachableAccounts.Of(context)
            .Where(account => account.EmployeeId == employeeId)
            .Select(account => account.Id);

    /// <summary>The account ids that can sign in, whose role holds the grant, without the accounts of the excluded employees.</summary>
    internal IQueryable<int> HoldersQuery(string moduleKey, string action, IReadOnlyCollection<int> excludedEmployeeIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(excludedEmployeeIds);

        var excluded = excludedEmployeeIds.Distinct().ToList();
        var grants = context.Set<RoleGrant>().Where(grant => grant.ModuleKey == moduleKey && grant.Action == action);
        return ReachableAccounts.Of(context)
            .Where(account => grants.Any(grant => grant.RoleId == account.RoleId))
            .Where(account => account.EmployeeId == null || !excluded.Contains(account.EmployeeId.Value))
            .OrderBy(account => account.Id)
            .Select(account => account.Id);
    }
}
