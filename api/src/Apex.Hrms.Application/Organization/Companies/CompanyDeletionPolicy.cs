using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Companies;

internal sealed class CompanyDeletionPolicy : IDeletionPolicy<Company>
{
    public IReadOnlyList<ReferenceCheck> References { get; } =
    [
        new("branch(es)", ReferenceQuery.BranchesOfCompany),
        new("department(s)", ReferenceQuery.DepartmentsOfCompany),
        new("employee(s)", ReferenceQuery.EmployeesOfCompany),
    ];
}
