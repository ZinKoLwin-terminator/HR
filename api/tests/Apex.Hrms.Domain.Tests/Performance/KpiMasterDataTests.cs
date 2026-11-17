using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>Categories, rating bands and templates: the rules that need no other record.</summary>
public sealed class KpiMasterDataTests
{
    private static KpiTemplateDetails Template(
        string code = "SALES-01",
        KpiMeasurementType measurement = KpiMeasurementType.Number,
        KpiDirection direction = KpiDirection.HigherIsBetter,
        KpiScoringMethod scoring = KpiScoringMethod.DirectPercentage,
        decimal target = 100m,
        decimal? rangeMin = null,
        decimal? rangeMax = null,
        decimal? maxRating = null,
        decimal weight = 20m,
        decimal? cap = null) => new(
        code,
        "Monthly sales",
        Description: null,
        CategoryId: 1,
        DepartmentId: null,
        measurement,
        Unit: null,
        direction,
        scoring,
        target,
        rangeMin,
        rangeMax,
        maxRating,
        weight,
        cap,
        KpiFrequency.Monthly);

    private static void AssertFailsOn(Result result, string field)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.FieldErrors!.Keys);
    }

    // ---- Categories ----

    [Fact]
    public void A_category_is_created_with_trimmed_text_and_blank_description_becomes_null()
    {
        var created = KpiCategory.Create(new KpiCategoryDetails(" PROD ", "  Productivity ", "   "), RecordStatus.Active);

        Assert.True(created.IsSuccess);
        Assert.Equal("PROD", created.Value.Code);
        Assert.Equal("Productivity", created.Value.Name);
        Assert.Null(created.Value.Description);
        Assert.True(created.Value.IsActive);
    }

    [Theory]
    [InlineData("prod")]
    [InlineData("PR OD")]
    [InlineData("-PROD")]
    [InlineData("")]
    public void A_category_code_follows_the_record_code_rules(string code) =>
        AssertFailsOn(KpiCategory.Validate(new KpiCategoryDetails(code, "Productivity", null)), "code");

    [Fact]
    public void A_category_needs_a_name_and_a_failed_update_changes_nothing()
    {
        var category = KpiCategory.Create(new KpiCategoryDetails("PROD", "Productivity", null), RecordStatus.Active).Value;

        var result = category.Update(new KpiCategoryDetails("QUAL", " ", null));

        AssertFailsOn(result, "name");
        Assert.Equal("PROD", category.Code);
        Assert.Equal("Productivity", category.Name);
    }

    [Fact]
    public void A_category_is_deactivated_and_activated_again()
    {
        var category = KpiCategory.Create(new KpiCategoryDetails("PROD", "Productivity", null), RecordStatus.Active).Value;

        category.Deactivate();
        Assert.False(category.IsActive);
        category.Activate();
        Assert.True(category.IsActive);
    }

    // ---- Rating bands ----

    [Theory]
    [InlineData(0)]
    [InlineData(60.5)]
    [InlineData(500)]
    public void A_rating_band_accepts_a_minimum_score_from_0_to_500(decimal minScore) =>
        Assert.True(RatingBand.Create(new RatingBandDetails("Meets", minScore, null), RecordStatus.Active).IsSuccess);

    [Theory]
    [InlineData(-0.01)]
    [InlineData(500.01)]
    public void A_rating_band_refuses_a_minimum_score_outside_0_to_500(decimal minScore) =>
        AssertFailsOn(RatingBand.Validate(new RatingBandDetails("Meets", minScore, null)), "minScore");

    [Fact]
    public void A_rating_band_needs_a_label_and_changes_status_by_command_only()
    {
        AssertFailsOn(RatingBand.Validate(new RatingBandDetails("  ", 10m, null)), "label");

        var band = RatingBand.Create(new RatingBandDetails(" Meets ", 70m, " ok "), RecordStatus.Inactive).Value;
        Assert.Equal("Meets", band.Label);
        Assert.Equal("ok", band.Description);
        Assert.False(band.IsActive);

        band.Update(new RatingBandDetails("Meets", 75m, null));
        Assert.False(band.IsActive);
        band.Activate();
        Assert.True(band.IsActive);
        band.Deactivate();
        Assert.False(band.IsActive);
    }

    // ---- Templates ----

    [Fact]
    public void A_template_is_created_with_its_rule_and_normalized_text()
    {
        var details = Template() with { Name = "  Monthly sales ", Unit = " USD ", Description = " " };

        var created = KpiTemplate.Create(details, RecordStatus.Active);

        Assert.True(created.IsSuccess);
        Assert.Equal("Monthly sales", created.Value.Name);
        Assert.Equal("USD", created.Value.Unit);
        Assert.Null(created.Value.Description);
        Assert.Equal(KpiScoringMethod.DirectPercentage, created.Value.Rule.ScoringMethod);
        Assert.Equal(100m, created.Value.Rule.Target);
    }

    [Fact]
    public void A_lower_is_better_template_without_a_cap_is_refused_the_way_the_database_does()
    {
        var details = Template(direction: KpiDirection.LowerIsBetter, scoring: KpiScoringMethod.CappedPercentage, target: 5m, cap: null);

        AssertFailsOn(KpiTemplate.Validate(details), "maxAchievementPct");
    }

    [Fact]
    public void A_lower_is_better_template_with_a_cap_is_valid_and_may_have_a_zero_target()
    {
        var details = Template(direction: KpiDirection.LowerIsBetter, scoring: KpiScoringMethod.CappedPercentage, target: 0m, cap: 120m);

        Assert.True(KpiTemplate.Validate(details).IsSuccess);
    }

    [Fact]
    public void The_target_error_is_reported_on_the_request_field_default_target()
    {
        var result = KpiTemplate.Validate(Template(target: 0m));

        AssertFailsOn(result, "defaultTarget");
        Assert.DoesNotContain("target", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void A_rating_template_needs_a_scale_and_the_rating_measurement()
    {
        var valid = Template(measurement: KpiMeasurementType.Rating, scoring: KpiScoringMethod.RatingBased, target: 5m, maxRating: 5m);
        var noScale = valid with { MaxRating = null };
        var wrongMeasurement = valid with { MeasurementType = KpiMeasurementType.Number };

        Assert.True(KpiTemplate.Validate(valid).IsSuccess);
        AssertFailsOn(KpiTemplate.Validate(noScale), "maxRating");
        AssertFailsOn(KpiTemplate.Validate(wrongMeasurement), "scoringMethod");
    }

    [Fact]
    public void A_range_template_needs_a_range_and_the_range_direction()
    {
        var valid = Template(direction: KpiDirection.RangeBased, scoring: KpiScoringMethod.RangeBased, rangeMin: 90m, rangeMax: 110m);

        Assert.True(KpiTemplate.Validate(valid).IsSuccess);
        AssertFailsOn(KpiTemplate.Validate(valid with { RangeMax = 90m }), "rangeMax");
        AssertFailsOn(KpiTemplate.Validate(valid with { RangeMin = null }), "rangeMin");
        AssertFailsOn(KpiTemplate.Validate(valid with { Direction = KpiDirection.HigherIsBetter }), "scoringMethod");
    }

    [Fact]
    public void A_capped_percentage_template_needs_a_cap_and_a_direct_one_must_not_have_one()
    {
        AssertFailsOn(KpiTemplate.Validate(Template(scoring: KpiScoringMethod.CappedPercentage, cap: null)), "maxAchievementPct");
        AssertFailsOn(KpiTemplate.Validate(Template(scoring: KpiScoringMethod.DirectPercentage, cap: 120m)), "maxAchievementPct");
        Assert.True(KpiTemplate.Validate(Template(scoring: KpiScoringMethod.CappedPercentage, cap: 120m)).IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.01)]
    public void The_default_weight_is_above_0_and_at_most_100(decimal weight) =>
        AssertFailsOn(KpiTemplate.Validate(Template(weight: weight)), "defaultWeight");

    [Fact]
    public void A_template_reports_every_problem_at_once_and_a_failed_update_changes_nothing()
    {
        var template = KpiTemplate.Create(Template(), RecordStatus.Active).Value;

        var result = template.Update(Template(code: "bad code", weight: 0m, target: 0m));

        Assert.True(result.IsFailure);
        Assert.Equal(["code", "defaultTarget", "defaultWeight"], result.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("SALES-01", template.Code);
        Assert.Equal(100m, template.DefaultTarget);
        Assert.Equal(20m, template.DefaultWeight);
    }

    [Fact]
    public void A_template_update_changes_every_field_but_not_the_status()
    {
        var template = KpiTemplate.Create(Template(), RecordStatus.Inactive).Value;
        var changed = Template(
            "QUAL-01",
            measurement: KpiMeasurementType.Rating,
            scoring: KpiScoringMethod.RatingBased,
            target: 4m,
            maxRating: 5m,
            weight: 35m) with
        {
            CategoryId = 2,
            DepartmentId = 7,
            Frequency = KpiFrequency.Quarterly,
        };

        var result = template.Update(changed);

        Assert.True(result.IsSuccess);
        Assert.Equal("QUAL-01", template.Code);
        Assert.Equal(2, template.CategoryId);
        Assert.Equal(7, template.DepartmentId);
        Assert.Equal(KpiFrequency.Quarterly, template.Frequency);
        Assert.Equal(5m, template.MaxRating);
        Assert.False(template.IsActive);
    }
}
