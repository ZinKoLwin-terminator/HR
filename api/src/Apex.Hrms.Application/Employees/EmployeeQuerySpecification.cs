using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Employees;

/// <summary>
/// What the employee list can search, filter and sort by. Only fields of the list DTO appear here: searching or
/// sorting by a personal field would reveal it through the order or the hits.
/// </summary>
internal sealed class EmployeeQuerySpecification : IQuerySpecification<Employee, EmployeeFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Employee, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Employee, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = e => e.Id,
            ["employeeCode"] = e => e.EmployeeCode,
            ["firstName"] = e => e.FirstName,
            ["lastName"] = e => e.LastName,
            ["fullName"] = e => e.FirstName + " " + e.LastName,
            ["workEmail"] = e => e.WorkEmail,
            ["branchId"] = e => e.BranchId,
            ["departmentId"] = e => e.DepartmentId,
            ["positionId"] = e => e.PositionId,
            ["employmentType"] = e => e.EmploymentType,
            ["employmentStatus"] = e => e.Status,
            ["joiningDate"] = e => e.JoiningDate,
        };

    public Expression<Func<Employee, bool>> Search(string term) =>
        Predicates.ContainsAny<Employee>(term, e => e.EmployeeCode, e => e.FirstName + " " + e.LastName, e => e.WorkEmail);

    public Expression<Func<Employee, bool>> Filter(EmployeeFilter filter)
    {
        var predicate = Predicates.Always<Employee>();
        if (filter.DepartmentId is { } departmentId)
        {
            predicate = predicate.And(e => e.DepartmentId == departmentId);
        }

        if (filter.BranchId is { } branchId)
        {
            predicate = predicate.And(e => e.BranchId == branchId);
        }

        if (filter.ShiftId is { } shiftId)
        {
            predicate = predicate.And(e => e.ShiftId == shiftId);
        }

        if (FilterPredicates.IsSet(filter.EmploymentStatus))
        {
            predicate = EmploymentStatusText.TryParse(filter.EmploymentStatus, out var status)
                ? predicate.And(e => e.Status == status)
                : Predicates.Never<Employee>();
        }

        if (FilterPredicates.IsSet(filter.EmploymentType))
        {
            predicate = EmploymentTypeText.TryParse(filter.EmploymentType, out var type)
                ? predicate.And(e => e.EmploymentType == type)
                : Predicates.Never<Employee>();
        }

        if (filter.JoiningFrom is { } from)
        {
            predicate = predicate.And(e => e.JoiningDate >= from);
        }

        if (filter.JoiningTo is { } to)
        {
            predicate = predicate.And(e => e.JoiningDate <= to);
        }

        return predicate;
    }
}
