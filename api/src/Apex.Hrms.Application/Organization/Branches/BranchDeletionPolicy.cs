using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Branches;

internal sealed class BranchDeletionPolicy : IDeletionPolicy<Branch>
{
    public IReadOnlyList<ReferenceCheck> References { get; } =
    [
        new("department(s)", ReferenceQuery.DepartmentsOfBranch),
        new("employee(s)", ReferenceQuery.EmployeesOfBranch),
    ];
}
