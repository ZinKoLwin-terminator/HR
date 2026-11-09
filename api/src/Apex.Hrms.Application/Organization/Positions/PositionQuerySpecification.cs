using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Positions;

internal sealed class PositionQuerySpecification : IQuerySpecification<Position, PositionFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Position, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Position, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = p => p.Id,
            ["code"] = p => p.Code,
            ["title"] = p => p.Title,
            ["departmentId"] = p => p.DepartmentId,
            ["jobGradeId"] = p => p.JobGradeId,
            ["headcount"] = p => p.Headcount,
            ["status"] = p => p.Status,
        };

    public Expression<Func<Position, bool>> Search(string term) =>
        Predicates.ContainsAny<Position>(term, p => p.Code, p => p.Title);

    public Expression<Func<Position, bool>> Filter(PositionFilter filter)
    {
        var predicate = FilterPredicates.Status<Position>(filter.Status);
        if (filter.DepartmentId is { } departmentId)
        {
            predicate = predicate.And(p => p.DepartmentId == departmentId);
        }

        if (filter.JobGradeId is { } jobGradeId)
        {
            predicate = predicate.And(p => p.JobGradeId == jobGradeId);
        }

        return predicate;
    }
}
