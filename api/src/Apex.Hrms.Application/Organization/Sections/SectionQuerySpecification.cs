using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Sections;

internal sealed class SectionQuerySpecification : IQuerySpecification<Section, SectionFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Section, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Section, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = s => s.Id,
            ["code"] = s => s.Code,
            ["name"] = s => s.Name,
            ["departmentId"] = s => s.DepartmentId,
            ["status"] = s => s.Status,
        };

    public Expression<Func<Section, bool>> Search(string term) =>
        Predicates.ContainsAny<Section>(term, s => s.Code, s => s.Name);

    public Expression<Func<Section, bool>> Filter(SectionFilter filter)
    {
        var predicate = FilterPredicates.Status<Section>(filter.Status);
        return filter.DepartmentId is { } departmentId
            ? predicate.And(s => s.DepartmentId == departmentId)
            : predicate;
    }
}
