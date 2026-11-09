using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Companies;

internal sealed class CompanyQuerySpecification : IQuerySpecification<Company, CompanyFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<Company, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<Company, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = c => c.Id,
            ["code"] = c => c.Code,
            ["name"] = c => c.Name,
            ["legalName"] = c => c.LegalName,
            ["industry"] = c => c.Industry,
            ["city"] = c => c.City,
            ["country"] = c => c.Country,
            ["foundedDate"] = c => c.FoundedDate,
            ["status"] = c => c.Status,
        };

    public Expression<Func<Company, bool>> Search(string term) =>
        Predicates.ContainsAny<Company>(term, c => c.Code, c => c.Name, c => c.LegalName, c => c.City);

    public Expression<Func<Company, bool>> Filter(CompanyFilter filter) =>
        FilterPredicates.Status<Company>(filter.Status);
}
