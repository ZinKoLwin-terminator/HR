using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

public sealed class KpiRuleTests
{
    private static KpiRule Direct(decimal target = 100m, KpiDirection direction = KpiDirection.HigherIsBetter) =>
        new(KpiMeasurementType.Number, direction, KpiScoringMethod.DirectPercentage, target, null, null, null, null);

    private static KpiRule Rating(decimal? maxRating = 5m) =>
        new(KpiMeasurementType.Rating, KpiDirection.HigherIsBetter, KpiScoringMethod.RatingBased, 5m, null, null, maxRating, null);

    private static KpiRule Range(decimal? min = 90m, decimal? max = 110m) =>
        new(KpiMeasurementType.Number, KpiDirection.RangeBased, KpiScoringMethod.RangeBased, 100m, min, max, null, null);

    private static void AssertInvalid(KpiRule rule, string field)
    {
        var result = rule.Validate();
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void A_plain_direct_percentage_rule_is_valid() => Assert.True(Direct().Validate().IsSuccess);

    [Fact]
    public void A_rating_rule_with_a_scale_is_valid() => Assert.True(Rating().Validate().IsSuccess);

    [Fact]
    public void A_range_rule_is_valid() => Assert.True(Range().Validate().IsSuccess);

    [Fact]
    public void A_boolean_rule_is_valid() =>
        Assert.True(new KpiRule(KpiMeasurementType.Boolean, KpiDirection.HigherIsBetter, KpiScoringMethod.Boolean, 1m, null, null, null, null).Validate().IsSuccess);

    [Fact]
    public void A_capped_rule_with_a_cap_is_valid() =>
        Assert.True(new KpiRule(KpiMeasurementType.Number, KpiDirection.LowerIsBetter, KpiScoringMethod.CappedPercentage, 10m, null, null, null, 150m).Validate().IsSuccess);

    [Fact]
    public void Rating_based_scoring_needs_the_rating_measurement_type() =>
        AssertInvalid(Rating() with { MeasurementType = KpiMeasurementType.Number }, "scoringMethod");

    [Fact]
    public void The_rating_measurement_type_needs_rating_based_scoring() =>
        AssertInvalid(Direct() with { MeasurementType = KpiMeasurementType.Rating }, "scoringMethod");

    [Fact]
    public void Boolean_scoring_needs_the_boolean_measurement_type() =>
        AssertInvalid(Direct() with { ScoringMethod = KpiScoringMethod.Boolean }, "scoringMethod");

    [Fact]
    public void The_boolean_measurement_type_needs_boolean_scoring() =>
        AssertInvalid(Direct() with { MeasurementType = KpiMeasurementType.Boolean }, "scoringMethod");

    [Fact]
    public void Range_based_scoring_needs_the_range_direction() =>
        AssertInvalid(Range() with { Direction = KpiDirection.HigherIsBetter }, "scoringMethod");

    [Fact]
    public void The_range_direction_needs_range_based_scoring() =>
        AssertInvalid(Direct(direction: KpiDirection.RangeBased), "scoringMethod");

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_rating_scale_must_be_above_zero(int? scale) =>
        AssertInvalid(Rating(scale), "maxRating");

    [Fact]
    public void A_rating_scale_is_only_for_rating_scoring() =>
        AssertInvalid(Direct() with { MaxRating = 5m }, "maxRating");

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_range_minimum_must_be_above_zero(int? min) =>
        AssertInvalid(Range(min), "rangeMin");

    [Theory]
    [InlineData(null)]
    [InlineData(90)]
    [InlineData(80)]
    public void A_range_maximum_must_be_above_the_minimum(int? max) =>
        AssertInvalid(Range(max: max), "rangeMax");

    [Fact]
    public void A_range_minimum_is_only_for_range_scoring() =>
        AssertInvalid(Direct() with { RangeMin = 1m }, "rangeMin");

    [Fact]
    public void A_range_maximum_is_only_for_range_scoring() =>
        AssertInvalid(Direct() with { RangeMax = 1m }, "rangeMax");

    [Fact]
    public void Capped_percentage_needs_a_maximum_achievement() =>
        AssertInvalid(Direct() with { ScoringMethod = KpiScoringMethod.CappedPercentage }, "maxAchievementPct");

    [Fact]
    public void Direct_percentage_has_no_maximum_achievement() =>
        AssertInvalid(Direct() with { MaxAchievementPct = 120m }, "maxAchievementPct");

    [Theory]
    [InlineData(0.99)]
    [InlineData(0)]
    [InlineData(500.01)]
    public void A_maximum_achievement_must_be_between_1_and_500(double cap) =>
        AssertInvalid(Direct() with { ScoringMethod = KpiScoringMethod.CappedPercentage, MaxAchievementPct = (decimal)cap }, "maxAchievementPct");

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    public void The_limits_of_the_maximum_achievement_are_valid(int cap) =>
        Assert.True((Direct() with { ScoringMethod = KpiScoringMethod.CappedPercentage, MaxAchievementPct = cap }).Validate().IsSuccess);

    [Fact]
    public void A_target_cannot_be_negative() => AssertInvalid(Direct(-1m), "target");

    [Theory]
    [InlineData(KpiDirection.HigherIsBetter)]
    [InlineData(KpiDirection.ExactTarget)]
    public void These_directions_need_a_target_above_zero(KpiDirection direction) =>
        AssertInvalid(Direct(0m, direction), "target");

    [Fact]
    public void Lower_is_better_accepts_a_zero_target() =>
        Assert.True((Direct(0m, KpiDirection.LowerIsBetter) with { ScoringMethod = KpiScoringMethod.CappedPercentage, MaxAchievementPct = 120m })
            .Validate().IsSuccess);

    [Fact]
    public void Lower_is_better_needs_a_cap() =>
        AssertInvalid(Direct(10m, KpiDirection.LowerIsBetter), "maxAchievementPct");

    [Fact]
    public void Several_problems_are_reported_together()
    {
        var rule = Direct(-1m) with { MaxRating = 5m, RangeMin = 1m };

        var errors = rule.Validate().Error.FieldErrors!;

        Assert.Contains("target", errors.Keys);
        Assert.Contains("maxRating", errors.Keys);
        Assert.Contains("rangeMin", errors.Keys);
    }
}
