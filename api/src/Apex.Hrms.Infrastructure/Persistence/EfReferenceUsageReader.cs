using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

internal sealed class EfReferenceUsageReader(HrmsDbContext context) : IReferenceUsageReader
{
    public Task<int> CountAsync(ReferenceQuery query, int ownerId, CancellationToken cancellationToken) => query switch
    {
        ReferenceQuery.BranchesOfCompany => Count<Branch>(b => b.CompanyId == ownerId, cancellationToken),
        ReferenceQuery.DepartmentsOfCompany => Count<Department>(d => d.CompanyId == ownerId, cancellationToken),
        ReferenceQuery.EmployeesOfCompany => Count<EmployeeReference>(e => e.CompanyId == ownerId, cancellationToken),
        ReferenceQuery.DepartmentsOfBranch => Count<Department>(d => d.BranchId == ownerId, cancellationToken),
        ReferenceQuery.EmployeesOfBranch => Count<EmployeeReference>(e => e.BranchId == ownerId, cancellationToken),
        ReferenceQuery.SectionsOfDepartment => Count<Section>(s => s.DepartmentId == ownerId, cancellationToken),
        ReferenceQuery.PositionsOfDepartment => Count<Position>(p => p.DepartmentId == ownerId, cancellationToken),
        ReferenceQuery.EmployeesOfDepartment => Count<EmployeeReference>(e => e.DepartmentId == ownerId, cancellationToken),
        ReferenceQuery.PositionsOfSection => Count<Position>(p => p.SectionId == ownerId, cancellationToken),
        ReferenceQuery.EmployeesOfSection => Count<EmployeeReference>(e => e.SectionId == ownerId, cancellationToken),
        ReferenceQuery.PositionsOfJobGrade => Count<Position>(p => p.JobGradeId == ownerId, cancellationToken),
        ReferenceQuery.EmployeesOfJobGrade => Count<EmployeeReference>(e => e.JobGradeId == ownerId, cancellationToken),
        ReferenceQuery.EmployeesOfPosition => Count<EmployeeReference>(e => e.PositionId == ownerId, cancellationToken),
        ReferenceQuery.PositionsReportingToPosition => Count<Position>(p => p.ReportsToPositionId == ownerId, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown reference query."),
    };

    private Task<int> Count<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken)
        where TEntity : class =>
        context.Set<TEntity>().CountAsync(predicate, cancellationToken);
}
