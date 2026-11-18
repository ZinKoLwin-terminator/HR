using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances.Categories;

internal sealed class GrievanceCategoryQuerySpecification : IQuerySpecification<GrievanceCategory, GrievanceCategoryFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<GrievanceCategory, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<GrievanceCategory, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = c => c.Id,
            ["code"] = c => c.Code,
            ["name"] = c => c.Name,
            ["sortOrder"] = c => c.SortOrder,
            ["defaultTargetDays"] = c => c.DefaultTargetDays,
            ["status"] = c => c.Status,
        };

    public Expression<Func<GrievanceCategory, bool>> Search(string term) =>
        Predicates.ContainsAny<GrievanceCategory>(term, c => c.Code, c => c.Name);

    public Expression<Func<GrievanceCategory, bool>> Filter(GrievanceCategoryFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var predicate = FilterPredicates.Status<GrievanceCategory>(filter.Status);
        if (filter.AllowsAnonymous is { } anonymous)
        {
            predicate = predicate.And(c => c.AllowsAnonymous == anonymous);
        }

        return predicate;
    }
}
