using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Departments;

internal sealed class DepartmentDeletionPolicy : IDeletionPolicy<Department>
{
    public IReadOnlyList<ReferenceCheck> References { get; } =
    [
        new("section(s)", ReferenceQuery.SectionsOfDepartment),
        new("position(s)", ReferenceQuery.PositionsOfDepartment),
        new("employee(s)", ReferenceQuery.EmployeesOfDepartment),
    ];
}
