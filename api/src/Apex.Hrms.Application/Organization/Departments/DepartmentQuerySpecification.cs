using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Departments;

internal sealed class DepartmentQuerySpecification : IQuerySpecification<Department, DepartmentFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Department, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Department, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = d => d.Id,
            ["code"] = d => d.Code,
            ["name"] = d => d.Name,
            ["companyId"] = d => d.CompanyId,
            ["branchId"] = d => d.BranchId,
            ["costCenter"] = d => d.CostCenter!,
            ["status"] = d => d.Status,
        };

    public Expression<Func<Department, bool>> Search(string term) =>
        Predicates.ContainsAny<Department>(term, d => d.Code, d => d.Name, d => d.CostCenter);

    public Expression<Func<Department, bool>> Filter(DepartmentFilter filter)
    {
        var predicate = FilterPredicates.Status<Department>(filter.Status);
        if (filter.CompanyId is { } companyId)
        {
            predicate = predicate.And(d => d.CompanyId == companyId);
        }

        if (filter.BranchId is { } branchId)
        {
            predicate = predicate.And(d => d.BranchId == branchId);
        }

        return predicate;
    }
}
