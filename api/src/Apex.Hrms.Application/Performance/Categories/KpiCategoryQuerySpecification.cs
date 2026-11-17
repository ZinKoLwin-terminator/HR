using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Categories;

internal sealed class KpiCategoryQuerySpecification : IQuerySpecification<KpiCategory, KpiCategoryFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<KpiCategory, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<KpiCategory, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = c => c.Id,
            ["code"] = c => c.Code,
            ["name"] = c => c.Name,
            ["status"] = c => c.Status,
        };

    public Expression<Func<KpiCategory, bool>> Search(string term) =>
        Predicates.ContainsAny<KpiCategory>(term, c => c.Code, c => c.Name);

    public Expression<Func<KpiCategory, bool>> Filter(KpiCategoryFilter filter) =>
        FilterPredicates.Status<KpiCategory>(filter.Status);
}
