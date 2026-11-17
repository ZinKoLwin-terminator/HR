using Apex.Hrms.Application.Performance.Insights;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>The arithmetic of the KPI dashboards and reports, by hand-computed cases.</summary>
public sealed class KpiFiguresTests
{
    [Theory]
    [InlineData(325, 4, 81.25)]
    [InlineData(260, 3, 86.67)]
    [InlineData(235, 3, 78.33)]
    [InlineData(200.005, 1, 200.01)]
    public void An_average_is_the_sum_over_the_count_rounded_to_two_decimals_away_from_zero(decimal sum, int count, decimal expected) =>
        Assert.Equal(expected, KpiFigures.Average(sum, count));

    [Fact]
    public void An_average_of_nothing_is_null_not_zero()
    {
        Assert.Null(KpiFigures.Average(null, 0));
        Assert.Null(KpiFigures.Average(0m, 0));
    }

    [Theory]
    [InlineData(4, 9, 44.44)]
    [InlineData(2, 3, 66.67)]
    [InlineData(3, 3, 100)]
    [InlineData(0, 5, 0)]
    public void The_completion_rate_is_finalized_over_evaluated_as_a_percentage_with_two_decimals(int finalized, int evaluated, decimal expected) =>
        Assert.Equal(expected, KpiFigures.CompletionRate(finalized, evaluated));

    [Fact]
    public void The_completion_rate_with_nothing_evaluated_is_zero_and_never_divides_by_zero() =>
        Assert.Equal(0m, KpiFigures.CompletionRate(0, 0));

    [Fact]
    public void The_attention_threshold_is_the_lowest_band_minimum_at_or_above_sixty()
    {
        Assert.Equal(70m, KpiFigures.AttentionThreshold([90m, 70m, 50m]));
        Assert.Equal(60m, KpiFigures.AttentionThreshold([60m, 75m]));
        Assert.Equal(65.5m, KpiFigures.AttentionThreshold([0m, 59.99m, 65.5m, 80m]));
    }

    [Fact]
    public void Without_a_band_at_or_above_sixty_the_attention_threshold_is_sixty()
    {
        Assert.Equal(60m, KpiFigures.AttentionThreshold([]));
        Assert.Equal(60m, KpiFigures.AttentionThreshold([0m, 40m, 59.99m]));
    }

    [Fact]
    public void Equal_scores_share_a_rank_and_the_next_rank_skips_standard_competition_ranking()
    {
        Assert.Equal([1, 1, 3, 4, 4, 6, 7], KpiFigures.CompetitionRanks([90m, 90m, 80m, 70m, 70m, 65m, 60m]));
        Assert.Equal([1, 2, 2, 4], KpiFigures.CompetitionRanks([4m, 3m, 3m, 1m]));
        Assert.Empty(KpiFigures.CompetitionRanks([]));
    }

    [Fact]
    public void A_page_that_starts_inside_a_tie_keeps_the_rank_of_the_tie()
    {
        // Whole ranking 90, 90, 80, 65: the second page of size 1 starts with the second 90 (position 2, rank 1).
        Assert.Equal([1, 3], KpiFigures.CompetitionRanks([90m, 80m], firstRank: 1, firstPosition: 2));
        Assert.Equal([3, 4], KpiFigures.CompetitionRanks([80m, 65m], firstRank: 3, firstPosition: 3));
    }

    [Fact]
    public void Every_status_is_listed_in_workflow_order_with_zero_counts()
    {
        var counts = KpiFigures.ByStatus(new Dictionary<ScorecardStatus, int> { [ScorecardStatus.Finalized] = 2, [ScorecardStatus.Draft] = 1 });

        Assert.Equal(
            ["Planning", "Draft", "Submitted", "Under Review", "Returned", "Resubmitted", "Approved", "Rejected", "Finalized"],
            counts.Select(count => count.Status));
        Assert.Equal([0, 1, 0, 0, 0, 0, 0, 0, 2], counts.Select(count => count.Count));
    }
}
