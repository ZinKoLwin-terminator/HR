using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Branches;

internal sealed class BranchRules(IRepository<Branch> branches, IRepository<Company> companies)
    : ISaveRules<Branch, BranchRequest>
{
    public Task<Result> CheckCreateAsync(BranchRequest request, CancellationToken cancellationToken) =>
        CheckAsync(request, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(Branch existing, BranchRequest request, CancellationToken cancellationToken) =>
        CheckAsync(request, existing, cancellationToken);

    private async Task<Result> CheckAsync(BranchRequest request, Branch? existing, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            branches, b => b.Code == code && b.Id != excludedId, "Branch code", "code", code, cancellationToken);
        if (unique.IsFailure || existing?.CompanyId == request.CompanyId)
        {
            return unique;
        }

        var company = await HierarchyChecks.FindParentAsync(
            companies, request.CompanyId, "Company", "companyId", cancellationToken);
        return company.IsFailure
            ? company
            : HierarchyChecks.EnsureActive(company.Value, "Company", company.Value.Name, "branches", "companyId");
    }
}
