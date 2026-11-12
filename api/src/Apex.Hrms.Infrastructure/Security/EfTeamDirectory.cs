using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Security;

/// <summary>The direct reports of an employee: the employees whose <c>ManagerEmployeeId</c> is that employee.</summary>
internal sealed class EfTeamDirectory(HrmsDbContext context) : ITeamDirectory
{
    public async Task<IReadOnlyList<int>> DirectReportIdsAsync(int managerEmployeeId, CancellationToken cancellationToken) =>
        await context.Set<Employee>()
            .AsNoTracking()
            .Where(employee => employee.ManagerEmployeeId == managerEmployeeId)
            .Select(employee => employee.Id)
            .ToListAsync(cancellationToken);
}
