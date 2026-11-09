using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Departments;

internal sealed class DepartmentRules(
    IRepository<Department> departments,
    IRepository<Branch> branches,
    IRepository<Company> companies,
    IEnumerable<IReferenceCheck<Department>> references) : ISaveRules<Department, DepartmentDetails>
{
    public Task<Result> CheckCreateAsync(DepartmentDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(
        Department existing,
        DepartmentDetails details,
        CancellationToken cancellationToken) =>
        CheckAsync(details, existing, cancellationToken);

    private async Task<Result> CheckAsync(
        DepartmentDetails details,
        Department? existing,
        CancellationToken cancellationToken)
    {
        var code = details.Code;
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            departments, d => d.Code == code && d.Id != excludedId, "Department code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var branch = await HierarchyChecks.FindParentAsync(
            branches, details.BranchId, "Branch", "branchId", cancellationToken);
        if (branch.IsFailure)
        {
            return branch;
        }

        if (branch.Value.CompanyId != details.CompanyId)
        {
            return Result.Failure(AppError.Validation(
                "The selected branch does not belong to the selected company.",
                "branchId",
                "Choose a branch of the selected company."));
        }

        var branchCheck = existing?.BranchId == details.BranchId
            ? Result.Success()
            : HierarchyChecks.EnsureActive(branch.Value, "Branch", branch.Value.Name, "departments", "branchId");
        if (branchCheck.IsFailure)
        {
            return branchCheck;
        }

        var companyCheck = existing?.CompanyId == details.CompanyId
            ? Result.Success()
            : await EnsureCompanyActiveAsync(details.CompanyId, cancellationToken);
        if (companyCheck.IsFailure || existing is null)
        {
            return companyCheck;
        }

        return existing.BranchId == details.BranchId
            ? Result.Success()
            : await ReferenceUsage.EnsureCanMoveAsync(
                references, existing.Id, "Department", existing.Name, "branch", "branchId", cancellationToken);
    }

    private async Task<Result> EnsureCompanyActiveAsync(int companyId, CancellationToken cancellationToken)
    {
        var company = await HierarchyChecks.FindParentAsync(
            companies, companyId, "Company", "companyId", cancellationToken);
        return company.IsFailure
            ? company
            : HierarchyChecks.EnsureActive(company.Value, "Company", company.Value.Name, "departments", "companyId");
    }
}
