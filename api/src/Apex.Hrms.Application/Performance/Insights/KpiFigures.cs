using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Insights;

/// <summary>
/// The arithmetic of the KPI dashboards and reports, kept apart from the queries so each rule has one definition. The database
/// returns sums and counts (exact decimals); averages and rates are divided and rounded here (2 decimals, away from zero, as
/// <see cref="KpiCalculator"/> rounds).
/// </summary>
public static class KpiFigures
{
    /// <summary>The attention threshold when no active rating band starts at or above <see cref="MeetsExpectationsFloor"/>.</summary>
    public const decimal DefaultAttentionThreshold = 60m;

    /// <summary>The lowest MinScore a band may have to count as the "Meets Expectations" band for the attention list.</summary>
    public const decimal MeetsExpectationsFloor = 60m;

    /// <summary>At most this many scorecards in the HR dashboard's top performers.</summary>
    public const int TopPerformerCount = 5;

    /// <summary>At most this many rows in the HR dashboard's attention list (the count covers all).</summary>
    public const int AttentionListLimit = 50;

    /// <summary>At most this many earlier scorecards in the employee dashboard's history.</summary>
    public const int HistoryLimit = 12;

    /// <summary>The statuses, in workflow order, for the per-status counts.</summary>
    public static IReadOnlyList<ScorecardStatus> StatusOrder { get; } =
    [
        ScorecardStatus.Planning,
        ScorecardStatus.Draft,
        ScorecardStatus.Submitted,
        ScorecardStatus.UnderReview,
        ScorecardStatus.Returned,
        ScorecardStatus.Resubmitted,
        ScorecardStatus.Approved,
        ScorecardStatus.Rejected,
        ScorecardStatus.Finalized,
    ];

    /// <summary>The mean of <paramref name="count"/> values that add up to <paramref name="sum"/>, 2 decimals; null when there are none.</summary>
    public static decimal? Average(decimal? sum, int count) =>
        count <= 0 ? null : Math.Round((sum ?? 0m) / count, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Finalized ÷ evaluated × 100, 2 decimals, where evaluated is every scorecard past Planning. 0 when nothing has been evaluated
    /// (never a division by zero).
    /// </summary>
    public static decimal CompletionRate(int finalized, int evaluated) =>
        evaluated <= 0 ? 0m : Math.Round(finalized * 100m / evaluated, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The score below which a Finalized scorecard needs attention: the MinScore of the lowest active rating band whose MinScore is
    /// at least <see cref="MeetsExpectationsFloor"/> (taken as the "Meets Expectations" band), or <see cref="DefaultAttentionThreshold"/>
    /// when there is no such band.
    /// </summary>
    public static decimal AttentionThreshold(IEnumerable<decimal> activeBandMinScores)
    {
        ArgumentNullException.ThrowIfNull(activeBandMinScores);
        var candidates = activeBandMinScores.Where(score => score >= MeetsExpectationsFloor).ToList();
        return candidates.Count == 0 ? DefaultAttentionThreshold : candidates.Min();
    }

    /// <summary>
    /// Standard competition ranks (1, 2, 2, 4) of scores already sorted from highest to lowest. <paramref name="firstRank"/> is the rank
    /// of the first score and <paramref name="firstPosition"/> its 1-based position in the whole ranking (they differ when a page starts
    /// in the middle of a tie).
    /// </summary>
    public static IReadOnlyList<int> CompetitionRanks(IReadOnlyList<decimal> scoresDescending, int firstRank = 1, int firstPosition = 1)
    {
        ArgumentNullException.ThrowIfNull(scoresDescending);
        var ranks = new int[scoresDescending.Count];
        for (var i = 0; i < ranks.Length; i++)
        {
            ranks[i] = i == 0
                ? firstRank
                : scoresDescending[i] == scoresDescending[i - 1] ? ranks[i - 1] : firstPosition + i;
        }

        return ranks;
    }

    /// <summary>Every status with its count (0 when absent), in workflow order.</summary>
    public static IReadOnlyList<KpiStatusCountDto> ByStatus(IReadOnlyDictionary<ScorecardStatus, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return [.. StatusOrder.Select(status => new KpiStatusCountDto(ScorecardStatusText.ToText(status), counts.GetValueOrDefault(status)))];
    }
}
