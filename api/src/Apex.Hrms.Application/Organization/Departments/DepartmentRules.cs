using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Departments;

internal sealed class DepartmentRules(
    IRepository<Department> departments,
    IRepository<Branch> branches,
    IRepository<Company> companies) : ISaveRules<Department, DepartmentRequest>
{
    public Task<Result> CheckCreateAsync(DepartmentRequest request, CancellationToken cancellationToken) =>
        CheckAsync(request, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(
        Department existing,
        DepartmentRequest request,
        CancellationToken cancellationToken) =>
        CheckAsync(request, existing, cancellationToken);

    private async Task<Result> CheckAsync(
        DepartmentRequest request,
        Department? existing,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            departments, d => d.Code == code && d.Id != excludedId, "Department code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var branch = await HierarchyChecks.FindParentAsync(
            branches, request.BranchId, "Branch", "branchId", cancellationToken);
        if (branch.IsFailure)
        {
            return branch;
        }

        if (branch.Value.CompanyId != request.CompanyId)
        {
            return Result.Failure(AppError.Validation(
                "The selected branch does not belong to the selected company.",
                "branchId",
                "Choose a branch of the selected company."));
        }

        var branchCheck = existing?.BranchId == request.BranchId
            ? Result.Success()
            : HierarchyChecks.EnsureActive(branch.Value, "Branch", branch.Value.Name, "departments", "branchId");
        return branchCheck.IsFailure || existing?.CompanyId == request.CompanyId
            ? branchCheck
            : await EnsureCompanyActiveAsync(request.CompanyId, cancellationToken);
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
