using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Companies;

internal sealed class CompanyRules(IRepository<Company> companies) : ISaveRules<Company, CompanyDetails>
{
    public Task<Result> CheckCreateAsync(CompanyDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(Company existing, CompanyDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueAsync(details, existing.Id, cancellationToken);

    private async Task<Result> EnsureUniqueAsync(CompanyDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        var name = details.Name;
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
