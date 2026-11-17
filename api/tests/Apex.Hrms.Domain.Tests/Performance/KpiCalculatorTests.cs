using System.Globalization;
using System.Text.Json;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>
/// Tests of the KPI calculation engine (calculation version 1). The shared golden cases in
/// docs/test-data/kpi-calculation-cases.json are run too; the Angular demo mock runs the same file.
/// </summary>
public sealed class KpiCalculatorTests
{
    private static KpiRule Higher(decimal target = 100m, decimal? cap = null) =>
        new(
            KpiMeasurementType.Number,
            KpiDirection.HigherIsBetter,
            cap is null ? KpiScoringMethod.DirectPercentage : KpiScoringMethod.CappedPercentage,
            target,
            null,
            null,
            null,
            cap);

    /// <summary>Lower is better always has a cap (a zero result scores it); the default is the largest allowed.</summary>
    private static KpiRule Lower(decimal target = 100m, decimal cap = KpiRule.MaxAchievementLimit) =>
        Higher(target, cap) with { Direction = KpiDirection.LowerIsBetter };

    private static KpiRule Exact(decimal target = 100m) =>
        Higher(target) with { Direction = KpiDirection.ExactTarget };

    private static KpiRule RatingRule(decimal max = 5m) =>
        new(KpiMeasurementType.Rating, KpiDirection.HigherIsBetter, KpiScoringMethod.RatingBased, max, null, null, max, null);

    private static KpiRule BooleanRule() =>
        new(KpiMeasurementType.Boolean, KpiDirection.HigherIsBetter, KpiScoringMethod.Boolean, 1m, null, null, null, null);

    private static KpiRule RangeRule(decimal min = 90m, decimal max = 110m) =>
        new(KpiMeasurementType.Number, KpiDirection.RangeBased, KpiScoringMethod.RangeBased, 100m, min, max, null, null);

    private static decimal Achieve(KpiRule rule, decimal actual) => KpiCalculator.Achievement(rule, actual).Value;

    private static void AssertActualError(KpiRule rule, decimal actual)
    {
        var result = KpiCalculator.Achievement(rule, actual);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("actualValue", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void The_calculation_version_is_1_and_the_ceiling_is_1000()
    {
        Assert.Equal(1, KpiCalculator.CalculationVersion);
        Assert.Equal(1000m, KpiCalculator.SafetyCeilingPct);
    }

    [Fact]
    public void An_invalid_rule_fails_before_any_calculation()
    {
        var result = KpiCalculator.Achievement(Higher(0m), 5m);

        Assert.True(result.IsFailure);
        Assert.Contains("target", result.Error.FieldErrors!.Keys);
    }

    // Higher is better

    [Theory]
    [InlineData(80, 80)]
    [InlineData(100, 100)]
    [InlineData(150, 150)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    public void Higher_is_better_is_actual_over_target(int actual, int expected) =>
        Assert.Equal(expected, Achieve(Higher(), actual));

    [Fact]
    public void Higher_is_better_rounds_to_two_decimals() => Assert.Equal(33.33m, Achieve(Higher(3m), 1m));

    [Fact]
    public void A_midpoint_rounds_away_from_zero() => Assert.Equal(0.13m, Achieve(Higher(800m), 1m));

    [Fact]
    public void A_cap_is_applied_when_exceeded() => Assert.Equal(120m, Achieve(Higher(cap: 120m), 200m));

    [Fact]
    public void A_cap_is_not_applied_when_not_reached() => Assert.Equal(110m, Achieve(Higher(cap: 120m), 110m));

    [Fact]
    public void An_uncapped_direct_percentage_is_limited_by_the_safety_ceiling() =>
        Assert.Equal(1000m, Achieve(Higher(1m), 99999999999999.9999m));

    [Fact]
    public void The_cap_wins_when_it_is_below_the_safety_ceiling() =>
        Assert.Equal(500m, Achieve(Higher(1m, 500m), 99999m));

    // Lower is better

    [Theory]
    [InlineData(50, 200)]
    [InlineData(100, 100)]
    [InlineData(200, 50)]
    [InlineData(400, 25)]
    public void Lower_is_better_is_target_over_actual(int actual, int expected) =>
        Assert.Equal(expected, Achieve(Lower(), actual));

    [Fact]
    public void Lower_is_better_rounds_to_two_decimals() => Assert.Equal(33.33m, Achieve(Lower(), 300m));

    [Fact]
    public void Lower_is_better_with_a_tiny_actual_scores_the_cap() => Assert.Equal(500m, Achieve(Lower(), 3m));

    [Fact]
    public void Lower_is_better_never_scores_less_for_a_better_result()
    {
        // The flaw this rule closes: without a cap, 0 scored 100 while 0.001 scored the 1000 ceiling.
        var zero = Achieve(Lower(), 0m);
        var tiny = Achieve(Lower(), 0.001m);
        var onTarget = Achieve(Lower(), 100m);

        Assert.True(zero >= tiny && tiny >= onTarget);
    }

    [Fact]
    public void Lower_is_better_without_a_cap_is_refused()
    {
        var uncapped = Higher() with { Direction = KpiDirection.LowerIsBetter };

        var result = KpiCalculator.Achievement(uncapped, 0m);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.True(result.Error.FieldErrors!.ContainsKey("maxAchievementPct"));
    }

    [Fact]
    public void Lower_is_better_with_zero_actual_scores_the_cap_when_there_is_one() =>
        Assert.Equal(150m, Achieve(Lower(cap: 150m), 0m));

    [Fact]
    public void Lower_is_better_below_target_is_capped() => Assert.Equal(150m, Achieve(Lower(cap: 150m), 10m));

    [Fact]
    public void Lower_is_better_with_a_negative_actual_is_an_error() => AssertActualError(Lower(), -1m);

    [Fact]
    public void Lower_is_better_with_zero_target_and_zero_actual_scores_100() =>
        Assert.Equal(100m, Achieve(Lower(0m), 0m));

    [Fact]
    public void Lower_is_better_with_zero_target_and_positive_actual_scores_0() =>
        Assert.Equal(0m, Achieve(Lower(0m), 0.01m));

    // Exact target

    [Theory]
    [InlineData(100, 100)]
    [InlineData(90, 90)]
    [InlineData(110, 90)]
    [InlineData(200, 0)]
    [InlineData(250, 0)]
    [InlineData(0, 0)]
    [InlineData(-50, 0)]
    public void Exact_target_loses_the_relative_distance(int actual, int expected) =>
        Assert.Equal(expected, Achieve(Exact(), actual));

    // Boolean

    [Theory]
    [InlineData(1, 100)]
    [InlineData(0, 0)]
    public void Boolean_is_all_or_nothing(int actual, int expected) =>
        Assert.Equal(expected, Achieve(BooleanRule(), actual));

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    [InlineData(-1)]
    public void Boolean_rejects_anything_but_0_and_1(double actual) =>
        AssertActualError(BooleanRule(), (decimal)actual);

    // Rating

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 80)]
    [InlineData(5, 100)]
    public void Rating_is_the_share_of_the_scale(int actual, int expected) =>
        Assert.Equal(expected, Achieve(RatingRule(), actual));

    [Fact]
    public void Rating_rounds_to_two_decimals() => Assert.Equal(33.33m, Achieve(RatingRule(3m), 1m));

    [Theory]
    [InlineData(5.01)]
    [InlineData(-0.01)]
    public void Rating_outside_the_scale_is_an_error(double actual) =>
        AssertActualError(RatingRule(), (decimal)actual);

    [Fact]
    public void A_cap_applies_to_rating_scoring() =>
        Assert.Equal(40m, Achieve(RatingRule(7m) with { MaxAchievementPct = 40m }, 3.5m));

    // Range

    [Theory]
    [InlineData(90, 100)]
    [InlineData(100, 100)]
    [InlineData(110, 100)]
    [InlineData(45, 50)]
    [InlineData(89, 98.89)]
    [InlineData(0, 0)]
    [InlineData(-10, 0)]
    [InlineData(220, 50)]
    public void Range_is_full_inside_and_proportional_outside(double actual, double expected) =>
        Assert.Equal((decimal)expected, Achieve(RangeRule(), (decimal)actual));

    [Fact]
    public void Range_just_above_the_maximum_is_just_below_100() =>
        Assert.Equal(99.99m, Achieve(RangeRule(), 110.01m));

    [Fact]
    public void A_cap_applies_to_range_scoring() =>
        Assert.Equal(40m, Achieve(RangeRule() with { MaxAchievementPct = 40m }, 100m));

    // No input throws

    [Fact]
    public void Extreme_values_never_throw()
    {
        decimal[] actuals =
        [
            -99999999999999.9999m, -1m, -0.0001m, 0m, 0.0001m, 1m, 99999999999999.9999m,
        ];
        decimal[] targets = [0m, 0.0000000000000000000000000001m, 0.0001m, 1m, 99999999999999.9999m, 70000000000000000000000000000m];
        decimal[] ranges = [0.0000000000000000000000000001m, 1m, 99999999999999.9999m];

        foreach (var actual in actuals)
        {
            foreach (var target in targets)
            {
                foreach (var rule in new[] { Higher(target), Higher(target, 500m), Lower(target), Lower(target, 1m), Exact(target) })
                {
                    AssertInRange(rule, actual);
                }
            }

            foreach (var min in ranges)
            {
                AssertInRange(RangeRule(min, min + 1m), actual);
            }

            AssertInRange(RatingRule(0.0000000000000000000000000001m), actual);
            AssertInRange(RatingRule(99999999999999.9999m), actual);
            AssertInRange(BooleanRule(), actual);
        }
    }

    private static void AssertInRange(KpiRule rule, decimal actual)
    {
        var result = KpiCalculator.Achievement(rule, actual);
        if (result.IsSuccess)
        {
            Assert.InRange(result.Value, 0m, KpiCalculator.SafetyCeilingPct);
        }
    }

    // Weighted score

    [Fact]
    public void A_weighted_score_is_achievement_times_weight() =>
        Assert.Equal(40m, KpiCalculator.WeightedScore(80m, 50m));

    [Fact]
    public void A_weighted_score_midpoint_rounds_away_from_zero() =>
        Assert.Equal(0.51m, KpiCalculator.WeightedScore(50.5m, 1m));

    [Fact]
    public void A_weighted_score_uses_the_rounded_achievement() =>
        Assert.Equal(0.17m, KpiCalculator.WeightedScore(33.33m, 0.5m));

    // Weights

    [Theory]
    [InlineData("100")]
    [InlineData("50", "50")]
    [InlineData("33.33", "33.33", "33.34")]
    public void Weights_that_add_up_to_100_are_valid(params string[] weights) =>
        Assert.True(KpiCalculator.ValidateWeights(Parse(weights)).IsSuccess);

    [Fact]
    public void Weights_adding_up_to_99_99_are_rejected()
    {
        var result = KpiCalculator.ValidateWeights(Parse("50", "49.99"));

        Assert.True(result.IsFailure);
        Assert.Equal("The KPI weights must add up to 100%. They add up to 99.99%.", result.Error.Message);
        Assert.Contains("weights", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Weights_adding_up_to_100_01_are_rejected() =>
        Assert.Equal(
            "The KPI weights must add up to 100%. They add up to 100.01%.",
            KpiCalculator.ValidateWeights(Parse("50", "50.01")).Error.Message);

    [Fact]
    public void Weights_report_their_total_with_two_decimals() =>
        Assert.Contains("95.50%", KpiCalculator.ValidateWeights(Parse("60", "35.5")).Error.Message, StringComparison.Ordinal);

    [Fact]
    public void An_empty_list_of_weights_is_rejected()
    {
        var result = KpiCalculator.ValidateWeights([]);

        Assert.True(result.IsFailure);
        Assert.Contains("weights", result.Error.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData("0", "100")]
    [InlineData("-5", "105")]
    [InlineData("100.01")]
    [InlineData("100.001")]
    [InlineData("99.999", "0.001")]
    public void Weights_must_be_above_0_up_to_100_with_two_decimals(params string[] weights)
    {
        var result = KpiCalculator.ValidateWeights(Parse(weights));

        Assert.True(result.IsFailure);
        Assert.Contains("weights", result.Error.FieldErrors!.Keys);
    }

    private static decimal[] Parse(params string[] weights) =>
        [.. weights.Select(weight => decimal.Parse(weight, CultureInfo.InvariantCulture))];

    // Scorecard

    private static List<KpiScoreInput> Card(decimal? thirdActual = 200m) =>
    [
        new(Higher(), 50m, 80m),
        new(BooleanRule(), 30m, 1m),
        new(Lower(), 20m, thirdActual),
    ];

    [Fact]
    public void A_scorecard_is_the_sum_of_its_weighted_scores()
    {
        var result = KpiCalculator.Score(Card());

        Assert.True(result.IsSuccess);
        Assert.Equal([80m, 100m, 50m], result.Value.Items.Select(item => item.AchievementPct));
        Assert.Equal([40m, 30m, 10m], result.Value.Items.Select(item => item.WeightedScore));
        Assert.Equal(80m, result.Value.OverallScore);
    }

    [Fact]
    public void The_overall_score_adds_the_rounded_weighted_scores()
    {
        var items = new List<KpiScoreInput>
        {
            new(Higher(3m), 33.33m, 1m),
            new(Higher(3m), 33.33m, 1m),
            new(Higher(3m), 33.34m, 1m),
        };

        var result = KpiCalculator.Score(items).Value;

        Assert.Equal([11.11m, 11.11m, 11.11m], result.Items.Select(item => item.WeightedScore));
        Assert.Equal(33.33m, result.OverallScore);
    }

    [Fact]
    public void A_missing_actual_names_the_item()
    {
        var result = KpiCalculator.Score(Card(null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(["items[2].actualValue"], result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void An_invalid_actual_names_the_item()
    {
        var items = Card();
        items[1] = items[1] with { Actual = 0.5m };

        var result = KpiCalculator.Score(items);

        Assert.True(result.IsFailure);
        Assert.Contains("items[1].actualValue", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void An_invalid_rule_names_the_item()
    {
        var items = Card();
        items[0] = items[0] with { Rule = Higher(0m) };

        Assert.Contains("items[0].target", KpiCalculator.Score(items).Error.FieldErrors!.Keys);
    }

    [Fact]
    public void A_scorecard_with_wrong_weights_fails_on_weights()
    {
        var items = Card();
        items[0] = items[0] with { Weight = 49m };

        Assert.Contains("weights", KpiCalculator.Score(items).Error.FieldErrors!.Keys);
    }

    [Fact]
    public void An_empty_scorecard_fails() => Assert.True(KpiCalculator.Score([]).IsFailure);

    // Rating label

    private static readonly (decimal MinScore, string Label)[] Bands =
    [
        (90m, "Outstanding"),
        (0m, "Poor"),
        (70m, "Good"),
        (50m, "Fair"),
    ];

    [Theory]
    [InlineData(95, "Outstanding")]
    [InlineData(90, "Outstanding")]
    [InlineData(89.99, "Good")]
    [InlineData(70, "Good")]
    [InlineData(50, "Fair")]
    [InlineData(0, "Poor")]
    public void The_label_is_the_highest_band_reached(double score, string expected) =>
        Assert.Equal(expected, KpiCalculator.RatingLabel((decimal)score, Bands));

    [Fact]
    public void A_score_below_the_lowest_band_has_no_label() =>
        Assert.Null(KpiCalculator.RatingLabel(10m, [(50m, "Fair"), (70m, "Good")]));

    [Fact]
    public void Without_bands_there_is_no_label() => Assert.Null(KpiCalculator.RatingLabel(80m, []));

    // Golden cases

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static List<GoldenCase> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "kpi-calculation-cases.json");
        return JsonSerializer.Deserialize<List<GoldenCase>>(File.ReadAllText(path), JsonOptions)!;
    }

    public static TheoryData<string> GoldenCaseNames()
    {
        var names = new TheoryData<string>();
        foreach (var golden in LoadCases())
        {
            names.Add(golden.Name);
        }

        return names;
    }

    [Fact]
    public void The_golden_file_has_at_least_30_cases_with_unique_names()
    {
        var cases = LoadCases();

        Assert.True(cases.Count >= 30);
        Assert.Equal(cases.Count, cases.Select(golden => golden.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(GoldenCaseNames))]
    public void The_golden_case_gives_the_expected_result(string name)
    {
        var golden = LoadCases().Single(candidate => candidate.Name == name);
        var rule = new KpiRule(
            KpiMeasurementTypeText.Parse(golden.Rule.MeasurementType),
            KpiDirectionText.Parse(golden.Rule.Direction),
            KpiScoringMethodText.Parse(golden.Rule.ScoringMethod),
            golden.Rule.Target,
            golden.Rule.RangeMin,
            golden.Rule.RangeMax,
            golden.Rule.MaxRating,
            golden.Rule.MaxAchievementPct);

        var result = KpiCalculator.Achievement(rule, golden.Actual);

        if (golden.ExpectedError is { } field)
        {
            Assert.True(result.IsFailure, name);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Contains(field, result.Error.FieldErrors!.Keys);
        }
        else
        {
            Assert.True(result.IsSuccess, name);
            Assert.Equal(golden.ExpectedAchievement, result.Value);
        }
    }

    public sealed record GoldenRule(
        string MeasurementType,
        string Direction,
        string ScoringMethod,
        decimal Target,
        decimal? RangeMin,
        decimal? RangeMax,
        decimal? MaxRating,
        decimal? MaxAchievementPct);

    public sealed record GoldenCase(
        string Name,
        GoldenRule Rule,
        decimal Actual,
        decimal? ExpectedAchievement,
        string? ExpectedError);
}
