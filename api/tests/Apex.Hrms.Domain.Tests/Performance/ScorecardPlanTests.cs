using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>The planning rules of a scorecard and its items: Planning-only edits, the weight cap, the snapshot of a template and finalizing the plan.</summary>
public sealed class ScorecardPlanTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly DateTime Now = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
    private static readonly ReviewActor Hr = new(5, null, "HR Test");

    private static Scorecard Planning(int employeeId = 10) => Scorecard.Create(employeeId, 20, 1, 2, 3, 4, reviewerEmployeeId: null).Value;

    private static KpiTemplate Template(
        string code = "SALES-01",
        KpiDirection direction = KpiDirection.HigherIsBetter,
        KpiScoringMethod scoring = KpiScoringMethod.DirectPercentage,
        decimal target = 100m,
        decimal weight = 20m,
        decimal? cap = null) => KpiTemplate.Create(
        new KpiTemplateDetails(
            code,
            "Monthly sales",
            null,
            3,
            null,
            KpiMeasurementType.Number,
            "USD",
            direction,
            scoring,
            target,
            null,
            null,
            null,
            weight,
            cap,
            KpiFrequency.Monthly),
        RecordStatus.Active).Value;

    private static ScorecardItem NewItem(decimal? target = null, decimal? weight = null, KpiTemplate? template = null) =>
        ScorecardItem.Create(ScorecardItemDetails.FromTemplate(template ?? Template(), 1, target, weight, Today, Today.AddDays(30), 1, 9, Today)).Value;

    // ---- Planning-only edits ----

    [Fact]
    public void A_new_scorecard_is_in_planning_and_may_be_changed()
    {
        Assert.True(Planning().EnsurePlanning().IsSuccess);
    }

    [Fact]
    public void A_scorecard_that_is_past_planning_refuses_changes_with_a_conflict_naming_its_status()
    {
        var scorecard = Planning();
        Assert.True(scorecard.FinalizePlan(Today, 11, [100m], Hr, Now).IsSuccess);

        var result = scorecard.EnsurePlanning();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains("Draft", result.Error.Message, StringComparison.Ordinal);
    }

    // ---- The weight total ----

    [Theory]
    [InlineData(0.01)]
    [InlineData(99.99)]
    [InlineData(100)]
    public void A_weight_total_up_to_100_is_allowed_while_planning(double total) =>
        Assert.True(Scorecard.EnsureWeightTotal((decimal)total, "items").IsSuccess);

    [Fact]
    public void A_weight_total_above_100_is_a_validation_error_naming_the_total_on_the_field()
    {
        var result = Scorecard.EnsureWeightTotal(100.01m, "weight");

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("100.01%", result.Error.Message, StringComparison.Ordinal);
        Assert.True(result.Error.FieldErrors!.ContainsKey("weight"));
    }

    // ---- The snapshot of a template ----

    [Fact]
    public void An_item_copies_the_template_and_defaults_the_target_and_weight_to_the_templates()
    {
        var template = Template("LOW-1", KpiDirection.LowerIsBetter, KpiScoringMethod.CappedPercentage, target: 5m, weight: 15m, cap: 120m);

        var details = ScorecardItemDetails.FromTemplate(template, 7, null, null, Today, Today.AddDays(30), 2, 9, Today);

        Assert.Equal(7, details.ScorecardId);
        Assert.Equal(template.CategoryId, details.CategoryId);
        Assert.Equal("LOW-1", details.KpiCode);
        Assert.Equal(KpiDirection.LowerIsBetter, details.Direction);
        Assert.Equal(KpiScoringMethod.CappedPercentage, details.ScoringMethod);
        Assert.Equal(5m, details.Target);
        Assert.Equal(15m, details.Weight);
        Assert.Equal(120m, details.MaxAchievementPct);
        Assert.Equal("USD", details.Unit);
    }

    [Fact]
    public void An_overridden_target_and_weight_win_over_the_templates()
    {
        var details = ScorecardItemDetails.FromTemplate(Template(), 1, 250m, 35m, Today, Today, 0, null, Today);

        Assert.Equal(250m, details.Target);
        Assert.Equal(35m, details.Weight);
    }

    [Fact]
    public void A_snapshot_with_a_zero_target_on_higher_is_better_is_refused_on_the_target()
    {
        var result = ScorecardItem.Create(ScorecardItemDetails.FromTemplate(Template(), 1, 0m, null, Today, Today, 0, null, Today));

        Assert.True(result.IsFailure);
        Assert.True(result.Error.FieldErrors!.ContainsKey("target"));
    }

    [Fact]
    public void A_zero_target_is_fine_for_lower_is_better_with_a_cap()
    {
        var template = Template("LOW-1", KpiDirection.LowerIsBetter, KpiScoringMethod.CappedPercentage, target: 5m, cap: 120m);

        Assert.True(ScorecardItem.Create(ScorecardItemDetails.FromTemplate(template, 1, 0m, null, Today, Today, 0, null, Today)).IsSuccess);
    }

    // ---- Changing an item's plan ----

    [Fact]
    public void Changing_the_plan_of_an_item_changes_the_target_weight_dates_and_order_but_not_the_snapshot()
    {
        var item = NewItem();

        var result = item.UpdatePlan(300m, 40m, Today.AddDays(1), Today.AddDays(20), 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(300m, item.Target);
        Assert.Equal(40m, item.Weight);
        Assert.Equal(Today.AddDays(1), item.StartDate);
        Assert.Equal(Today.AddDays(20), item.EndDate);
        Assert.Equal(5, item.SortOrder);
        Assert.Equal("SALES-01", item.KpiCode);
        Assert.Equal(KpiScoringMethod.DirectPercentage, item.ScoringMethod);
    }

    [Fact]
    public void A_plan_change_that_breaks_the_rule_the_weight_or_the_dates_changes_nothing()
    {
        var item = NewItem();

        var zeroTarget = item.UpdatePlan(0m, 40m, Today, Today, 1);
        var heavy = item.UpdatePlan(100m, 100.5m, Today, Today, 1);
        var inverted = item.UpdatePlan(100m, 40m, Today.AddDays(2), Today, 1);

        Assert.True(zeroTarget.Error.FieldErrors!.ContainsKey("target"));
        Assert.True(heavy.Error.FieldErrors!.ContainsKey("weight"));
        Assert.True(inverted.Error.FieldErrors!.ContainsKey("endDate"));
        Assert.Equal(100m, item.Target);
        Assert.Equal(20m, item.Weight);
        Assert.Equal(Today.AddDays(30), item.EndDate);
    }

    // ---- Finalizing the plan ----

    [Fact]
    public void Finalizing_moves_planning_to_draft_sets_the_reviewer_and_date_and_returns_the_history_entry()
    {
        var scorecard = Planning();

        var result = scorecard.FinalizePlan(Today, 11, [60m, 40m], new ReviewActor(5, 6, "Aye Test"), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ScorecardStatus.Draft, scorecard.Status);
        Assert.Equal(11, scorecard.ReviewerEmployeeId);
        Assert.Equal(Today, scorecard.PlanFinalizedOn);
        var entry = result.Value;
        Assert.Equal(ReviewStep.HR, entry.Step);
        Assert.Equal(ReviewActionType.FinalizePlan, entry.Action);
        Assert.Equal(ScorecardStatus.Planning, entry.FromStatus);
        Assert.Equal(ScorecardStatus.Draft, entry.ToStatus);
        Assert.Equal((5, 6, "Aye Test"), (entry.ActorUserId, entry.ActorEmployeeId, entry.ActorName));
        Assert.Equal(Now, entry.OccurredAtUtc);
        Assert.Null(entry.Comment);
    }

    [Fact]
    public void An_employee_without_a_manager_leaves_the_reviewer_empty()
    {
        var scorecard = Planning();

        Assert.True(scorecard.FinalizePlan(Today, null, [100m], Hr, Now).IsSuccess);

        Assert.Null(scorecard.ReviewerEmployeeId);
        Assert.Equal(ScorecardStatus.Draft, scorecard.Status);
    }

    [Theory]
    [InlineData(99.99, false)]
    [InlineData(100, true)]
    [InlineData(100.01, false)]
    public void Only_weights_adding_up_to_exactly_100_finalize_the_plan(double total, bool finalizes)
    {
        var scorecard = Planning();

        var result = scorecard.FinalizePlan(Today, 11, [(decimal)total - 10m, 10m], Hr, Now);

        Assert.Equal(finalizes, result.IsSuccess);
        Assert.Equal(finalizes ? ScorecardStatus.Draft : ScorecardStatus.Planning, scorecard.Status);
        if (!finalizes)
        {
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Contains("must add up to 100%", result.Error.Message, StringComparison.Ordinal);
            Assert.Null(scorecard.PlanFinalizedOn);
        }
    }

    [Fact]
    public void A_plan_of_75_percent_says_so_and_a_plan_without_kpis_needs_one()
    {
        var scorecard = Planning();

        var short75 = scorecard.FinalizePlan(Today, 11, [50m, 25m], Hr, Now);
        var none = scorecard.FinalizePlan(Today, 11, [], Hr, Now);

        Assert.Equal("The KPI weights must add up to 100%. They add up to 75.00%.", short75.Error.Message);
        Assert.Contains("at least one KPI", none.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scorecard_whose_reviewer_would_be_the_employee_is_refused_with_a_conflict_and_stays_in_planning()
    {
        var scorecard = Planning(employeeId: 10);

        var result = scorecard.FinalizePlan(Today, 10, [100m], Hr, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(ScorecardStatus.Planning, scorecard.Status);
        Assert.Null(scorecard.PlanFinalizedOn);
    }

    [Fact]
    public void A_plan_can_only_be_finalized_once()
    {
        var scorecard = Planning();
        Assert.True(scorecard.FinalizePlan(Today, 11, [100m], Hr, Now).IsSuccess);

        var again = scorecard.FinalizePlan(Today, 12, [100m], Hr, Now);

        Assert.Equal(ErrorType.Conflict, again.Error.Type);
        Assert.Equal(11, scorecard.ReviewerEmployeeId);
    }
}
