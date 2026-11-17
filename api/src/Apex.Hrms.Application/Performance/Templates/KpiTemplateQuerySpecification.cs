using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Templates;

internal sealed class KpiTemplateQuerySpecification : IQuerySpecification<KpiTemplate, KpiTemplateFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<KpiTemplate, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<KpiTemplate, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = t => t.Id,
            ["code"] = t => t.Code,
            ["name"] = t => t.Name,
            ["categoryId"] = t => t.CategoryId,
            ["defaultWeight"] = t => t.DefaultWeight,
            ["scoringMethod"] = t => t.ScoringMethod,
            ["frequency"] = t => t.Frequency,
            ["status"] = t => t.Status,
        };

    public Expression<Func<KpiTemplate, bool>> Search(string term) =>
        Predicates.ContainsAny<KpiTemplate>(term, t => t.Code, t => t.Name);

    public Expression<Func<KpiTemplate, bool>> Filter(KpiTemplateFilter filter) => FilterPredicates.Status<KpiTemplate>(filter.Status)
        .And(filter.CategoryId is { } categoryId ? t => t.CategoryId == categoryId : Predicates.Always<KpiTemplate>())
        .And(filter.DepartmentId is { } departmentId ? t => t.DepartmentId == departmentId : Predicates.Always<KpiTemplate>())
        .And(ScoringMethodFilter(filter.ScoringMethod));

    private static Expression<Func<KpiTemplate, bool>> ScoringMethodFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<KpiTemplate>();
        }

        return KpiScoringMethodText.TryParse(text, out var method)
            ? t => t.ScoringMethod == method
            : Predicates.Never<KpiTemplate>();
    }
}
