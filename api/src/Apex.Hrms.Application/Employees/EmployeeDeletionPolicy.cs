using Apex.Hrms.Application.References;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Employees;

/// <summary>
/// What employees block. Organization records cannot be deleted, or moved under another parent, while employees
/// sit in them; the count comes from the placement view (no personal data). An employee cannot be deleted while
/// someone reports to them or while they head a branch, department or section: there is no soft delete, so the
/// caller records an exit instead.
/// </summary>
internal static class EmployeeDeletionPolicy
{
    public static IServiceCollection AddEmployeeReferences(this IServiceCollection services) => services
        .AddReference<Company, EmployeeOrgLink>("employee(s)", ReferenceUse.Delete, ownerId => employee => employee.CompanyId == ownerId)
        .AddReference<Branch, EmployeeOrgLink>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.BranchId == ownerId)
        .AddReference<Department, EmployeeOrgLink>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.DepartmentId == ownerId)
        .AddReference<Section, EmployeeOrgLink>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.SectionId == ownerId)
        .AddReference<JobGrade, EmployeeOrgLink>("employee(s)", ReferenceUse.Delete, ownerId => employee => employee.JobGradeId == ownerId)
        .AddReference<Position, EmployeeOrgLink>("employee(s)", ReferenceUse.Both, ownerId => employee => employee.PositionId == ownerId)

        .AddReference<Employee, Employee>("direct report(s)", ReferenceUse.Delete, ownerId => employee => employee.ManagerEmployeeId == ownerId)
        .AddReference<Employee, Branch>("branch(es) they manage", ReferenceUse.Delete, ownerId => branch => branch.ManagerEmployeeId == ownerId)
        .AddReference<Employee, Department>("department(s) they head", ReferenceUse.Delete, ownerId => department => department.HeadEmployeeId == ownerId)
        .AddReference<Employee, Section>("section(s) they supervise", ReferenceUse.Delete, ownerId => section => section.SupervisorEmployeeId == ownerId);
}
