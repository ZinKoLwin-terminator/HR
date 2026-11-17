using System.Linq.Expressions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Periods;

internal sealed class EvaluationPeriodQuerySpecification : IQuerySpecification<EvaluationPeriod, EvaluationPeriodFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<EvaluationPeriod, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<EvaluationPeriod, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = p => p.Id,
            ["name"] = p => p.Name,
            ["companyId"] = p => p.CompanyId,
            ["periodType"] = p => p.PeriodType,
            ["startDate"] = p => p.StartDate,
            ["endDate"] = p => p.EndDate,
            ["status"] = p => p.Status,
        };

    public Expression<Func<EvaluationPeriod, bool>> Search(string term) =>
        Predicates.ContainsAny<EvaluationPeriod>(term, p => p.Name);

    public Expression<Func<EvaluationPeriod, bool>> Filter(EvaluationPeriodFilter filter) => Predicates.Always<EvaluationPeriod>()
        .And(filter.CompanyId is { } companyId ? p => p.CompanyId == companyId : Predicates.Always<EvaluationPeriod>())
        .And(filter.Year is { } year ? p => p.StartDate.Year == year : Predicates.Always<EvaluationPeriod>())
        .And(PeriodTypeFilter(filter.PeriodType))
        .And(StatusFilter(filter.Status));

    private static Expression<Func<EvaluationPeriod, bool>> PeriodTypeFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<EvaluationPeriod>();
        }

        return KpiFrequencyText.TryParse(text, out var type)
            ? p => p.PeriodType == type
            : Predicates.Never<EvaluationPeriod>();
    }

    private static Expression<Func<EvaluationPeriod, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<EvaluationPeriod>();
        }

        return EvaluationPeriodStatusText.TryParse(text, out var status)
            ? p => p.Status == status
            : Predicates.Never<EvaluationPeriod>();
    }
}
