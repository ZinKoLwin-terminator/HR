using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// The accounts that could pass the permission check, by the rule of <c>AccountSnapshot.CanSignIn</c>: the account and its role are
/// Active and the linked employee (if any) has not left. Shared by the notification recipients and the permission holders.
/// </summary>
internal static class ReachableAccounts
{
    public static IQueryable<AppAccount> Of(HrmsDbContext context) =>
        from account in context.Set<AppAccount>().AsNoTracking()
        join role in context.Set<AppRole>().AsNoTracking() on account.RoleId equals role.Id
        where account.Status == AccountStatus.Active
            && role.Status == RecordStatus.Active
            && (account.EmployeeId == null
                || !context.Set<Employee>().Any(employee => employee.Id == account.EmployeeId && employee.ExitDate != null))
        select account;
}
