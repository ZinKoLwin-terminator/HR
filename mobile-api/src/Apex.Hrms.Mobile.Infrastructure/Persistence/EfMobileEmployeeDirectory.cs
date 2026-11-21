using Apex.Hrms.Mobile.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Mobile.Infrastructure.Persistence;

/// <summary>Reads the employee side of sign-in: the account linked to an employee code, and the work identity for the app header.</summary>
internal sealed class EfMobileEmployeeDirectory(MobileDbContext context) : IMobileEmployeeDirectory
{
    public Task<string?> FindIdentityUserIdByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken) =>
        (from employee in context.Employees.AsNoTracking()
         where employee.EmployeeCode == employeeCode
         join account in context.Accounts.AsNoTracking() on (int?)employee.Id equals account.EmployeeId
         select account.ExternalSubject).FirstOrDefaultAsync(cancellationToken);

    public Task<MobileEmployeeProfile?> FindProfileAsync(int employeeId, CancellationToken cancellationToken) =>
        (from employee in context.Employees.AsNoTracking()
         where employee.Id == employeeId
         select new MobileEmployeeProfile(
             employee.EmployeeCode,
             employee.FirstName + " " + employee.LastName,
             context.Departments.Where(department => department.Id == employee.DepartmentId).Select(department => department.Name).FirstOrDefault(),
             context.Positions.Where(position => position.Id == employee.PositionId).Select(position => position.Title).FirstOrDefault(),
             employee.PhotoUrl)).SingleOrDefaultAsync(cancellationToken);
}
