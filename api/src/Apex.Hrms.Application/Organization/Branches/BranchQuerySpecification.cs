using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Branches;

internal sealed class BranchQuerySpecification : IQuerySpecification<Branch, BranchFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Branch, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Branch, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = b => b.Id,
            ["code"] = b => b.Code,
            ["name"] = b => b.Name,
            ["companyId"] = b => b.CompanyId,
            ["type"] = b => b.Type,
            ["city"] = b => b.City,
            ["country"] = b => b.Country,
            ["openedDate"] = b => b.OpenedDate,
            ["status"] = b => b.Status,
        };

    public Expression<Func<Branch, bool>> Search(string term) =>
        Predicates.ContainsAny<Branch>(term, b => b.Code, b => b.Name, b => b.City);

    public Expression<Func<Branch, bool>> Filter(BranchFilter filter)
    {
        var predicate = FilterPredicates.Status<Branch>(filter.Status);
        if (filter.CompanyId is { } companyId)
        {
            predicate = predicate.And(b => b.CompanyId == companyId);
        }

        if (FilterPredicates.IsSet(filter.Type))
        {
            predicate = BranchTypeText.TryParse(filter.Type, out var type)
                ? predicate.And(b => b.Type == type)
                : Predicates.Never<Branch>();
        }

        return predicate;
    }
}
