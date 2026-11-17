using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Categories;

/// <summary>A KPI category has a unique code and a unique name.</summary>
internal sealed class KpiCategoryRules(IRepository<KpiCategory> categories) : ISaveRules<KpiCategory, KpiCategoryDetails>
{
    public Task<Result> CheckCreateAsync(KpiCategoryDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(KpiCategory existing, KpiCategoryDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing.Id, cancellationToken);

    private async Task<Result> CheckAsync(KpiCategoryDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            categories, c => c.Code == code && c.Id != excludedId, "KPI category code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var name = details.Name;
        return await HierarchyChecks.EnsureUniqueAsync(
            categories, c => c.Name == name && c.Id != excludedId, "KPI category name", "name", name, cancellationToken);
    }
}
