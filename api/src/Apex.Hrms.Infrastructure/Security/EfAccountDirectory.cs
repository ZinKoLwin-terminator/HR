using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Security;

internal sealed class EfAccountDirectory(HrmsDbContext context, TimeProvider timeProvider) : IAccountDirectory
{
    public Task<AccountSnapshot?> FindByExternalSubjectAsync(string externalSubject, bool includePermissions, CancellationToken cancellationToken) =>
        FindAsync(account => account.ExternalSubject == externalSubject, includePermissions, cancellationToken);

    public Task<AccountSnapshot?> FindByIdAsync(int accountId, bool includePermissions, CancellationToken cancellationToken) =>
        FindAsync(account => account.Id == accountId, includePermissions, cancellationToken);

    public async Task RecordSignInAsync(int accountId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // A bulk update on purpose: signing in is not a business change, so it must not run the audit stamping
        // (nobody is signed in yet) and must not touch ModifiedAtUtc/ModifiedByUserId or the row version.
        await context.Set<AppAccount>()
            .Where(account => account.Id == accountId)
            .ExecuteUpdateAsync(set => set.SetProperty(account => account.LastLoginAtUtc, now), cancellationToken);
    }

    private async Task<AccountSnapshot?> FindAsync(
        System.Linq.Expressions.Expression<Func<AppAccount, bool>> filter,
        bool includePermissions,
        CancellationToken cancellationToken)
    {
        var row = await (
            from account in context.Set<AppAccount>().AsNoTracking().Where(filter)
            join role in context.Set<AppRole>().AsNoTracking() on account.RoleId equals role.Id
            select new
            {
                account.Id,
                account.EmployeeId,
                account.DisplayName,
                account.Email,
                account.ExternalSubject,
                account.Status,
                RoleId = role.Id,
                RoleName = role.Name,
                EmployeeLeft = account.EmployeeId != null
                    && context.Set<Employee>().Any(employee => employee.Id == account.EmployeeId && employee.ExitDate != null),
                RoleStatus = role.Status,
                RoleRowVersion = role.RowVersion,
            }).SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        IReadOnlyList<string> permissions = [];
        if (includePermissions)
        {
            var grants = await context.Set<RoleGrant>().AsNoTracking()
                .Where(grant => grant.RoleId == row.RoleId)
                .Select(grant => new { grant.ModuleKey, grant.Action })
                .ToListAsync(cancellationToken);
            permissions = [.. grants.Select(grant => $"{grant.ModuleKey}:{grant.Action}").Order(StringComparer.Ordinal)];
        }

        return new AccountSnapshot(
            row.Id,
            row.EmployeeId,
            row.DisplayName,
            row.Email,
            row.ExternalSubject,
            row.Status,
            row.RoleId,
            row.RoleName,
            row.RoleStatus == RecordStatus.Active,
            row.EmployeeLeft,
            permissions,
            row.RoleRowVersion);
    }
}
