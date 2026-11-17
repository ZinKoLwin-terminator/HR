using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.RatingBands;

/// <summary>A rating band has a unique label and a unique minimum score (the database has unique constraints for both).</summary>
internal sealed class RatingBandRules(IRepository<RatingBand> bands) : ISaveRules<RatingBand, RatingBandDetails>
{
    public Task<Result> CheckCreateAsync(RatingBandDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(RatingBand existing, RatingBandDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing.Id, cancellationToken);

    private async Task<Result> CheckAsync(RatingBandDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var label = details.Label;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            bands, b => b.Label == label && b.Id != excludedId, "Rating label", "label", label, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var minScore = details.MinScore;
        return await HierarchyChecks.EnsureUniqueAsync(
            bands,
            b => b.MinScore == minScore && b.Id != excludedId,
            "Minimum score",
            "minScore",
            minScore.ToString("0.##", CultureInfo.InvariantCulture),
            cancellationToken);
    }
}
