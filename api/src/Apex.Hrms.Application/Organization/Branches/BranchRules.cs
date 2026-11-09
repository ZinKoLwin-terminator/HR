using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Branches;

internal sealed class BranchRules(
    IRepository<Branch> branches,
    IRepository<Company> companies,
    IEnumerable<IReferenceCheck<Branch>> references) : ISaveRules<Branch, BranchDetails>
{
    public Task<Result> CheckCreateAsync(BranchDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(Branch existing, BranchDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing, cancellationToken);

    private async Task<Result> CheckAsync(BranchDetails details, Branch? existing, CancellationToken cancellationToken)
    {
        var code = details.Code;
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            branches, b => b.Code == code && b.Id != excludedId, "Branch code", "code", code, cancellationToken);
        if (unique.IsFailure || existing?.CompanyId == details.CompanyId)
        {
            return unique;
        }

        var company = await HierarchyChecks.FindParentAsync(
            companies, details.CompanyId, "Company", "companyId", cancellationToken);
        if (company.IsFailure)
        {
            return company;
        }

        var active = HierarchyChecks.EnsureActive(company.Value, "Company", company.Value.Name, "branches", "companyId");
        if (active.IsFailure || existing is null)
        {
            return active;
        }

        return await ReferenceUsage.EnsureCanMoveAsync(
            references, existing.Id, "Branch", existing.Name, "company", "companyId", cancellationToken);
    }
}
