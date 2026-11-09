using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Companies;

internal sealed class CompanyRules(IRepository<Company> companies) : ISaveRules<Company, CompanyRequest>
{
    public Task<Result> CheckCreateAsync(CompanyRequest request, CancellationToken cancellationToken) =>
        EnsureUniqueAsync(request, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(Company existing, CompanyRequest request, CancellationToken cancellationToken) =>
        EnsureUniqueAsync(request, existing.Id, cancellationToken);

    private async Task<Result> EnsureUniqueAsync(CompanyRequest request, int excludedId, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();
        var codeCheck = await HierarchyChecks.EnsureUniqueAsync(
            companies, c => c.Code == code && c.Id != excludedId, "Company code", "code", code, cancellationToken);
        if (codeCheck.IsFailure)
        {
            return codeCheck;
        }

        return await HierarchyChecks.EnsureUniqueAsync(
            companies, c => c.Name == name && c.Id != excludedId, "Company name", "name", name, cancellationToken);
    }
}
