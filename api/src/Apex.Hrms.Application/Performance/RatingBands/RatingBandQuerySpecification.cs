using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.RatingBands;

internal sealed class RatingBandQuerySpecification : IQuerySpecification<RatingBand, RatingBandFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<RatingBand, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<RatingBand, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = b => b.Id,
            ["label"] = b => b.Label,
            ["minScore"] = b => b.MinScore,
            ["status"] = b => b.Status,
        };

    public Expression<Func<RatingBand, bool>> Search(string term) =>
        Predicates.ContainsAny<RatingBand>(term, b => b.Label);

    public Expression<Func<RatingBand, bool>> Filter(RatingBandFilter filter)
    {
        if (!Organization.FilterPredicates.IsSet(filter.Status))
        {
            return Predicates.Always<RatingBand>();
        }

        return RecordStatusText.TryParse(filter.Status, out var status)
            ? b => b.Status == status
            : Predicates.Never<RatingBand>();
    }
}
