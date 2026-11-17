using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>The persisted scorecard entities are constructible and keep the rules the database CHECK constraints state.</summary>
public sealed class ScorecardEntitiesTests
{
    private static readonly DateOnly Day = new(2026, 11, 9);

    private static ScorecardItemDetails Item(
        KpiMeasurementType measurement = KpiMeasurementType.Number,
        KpiDirection direction = KpiDirection.HigherIsBetter,
        KpiScoringMethod scoring = KpiScoringMethod.DirectPercentage,
        decimal target = 100m,
        decimal weight = 50m,
        decimal? cap = null) => new(
        ScorecardId: 1,
        TemplateId: 2,
        CategoryId: 3,
        KpiCode: " SALES-01 ",
        KpiName: " Monthly sales ",
        measurement,
        Unit: " ",
        direction,
        scoring,
        target,
        RangeMin: null,
        RangeMax: null,
        MaxRating: null,
        weight,
        cap,
        Day,
        Day.AddDays(30),
        SortOrder: 1,
        AssignedByUserId: 9,
        AssignedOn: Day);

    [Fact]
    public void A_scorecard_starts_in_planning_with_the_copied_placement()
    {
        var created = Scorecard.Create(10, 20, 1, 2, 3, 4, reviewerEmployeeId: 11);

        Assert.True(created.IsSuccess);
        var scorecard = created.Value;
        Assert.Equal(ScorecardStatus.Planning, scorecard.Status);
        Assert.Equal((10, 20, 1, 2, 3, 4), (scorecard.EmployeeId, scorecard.PeriodId, scorecard.CompanyId, scorecard.BranchId, scorecard.DepartmentId, scorecard.PositionId));
        Assert.Equal(11, scorecard.ReviewerEmployeeId);
        Assert.Null(scorecard.OverallScore);
        Assert.Null(scorecard.PlanFinalizedOn);
    }

    [Fact]
    public void An_employee_cannot_be_the_reviewer_of_their_own_scorecard()
    {
        var result = Scorecard.Create(10, 20, 1, 2, 3, 4, reviewerEmployeeId: 10);

        Assert.True(result.IsFailure);
        Assert.Contains("reviewerEmployeeId", result.Error.FieldErrors!.Keys);
        Assert.True(Scorecard.Create(10, 20, 1, 2, 3, 4, reviewerEmployeeId: null).IsSuccess);
    }

    [Fact]
    public void An_item_copies_the_template_values_trimmed_and_has_no_result_yet()
    {
        var created = ScorecardItem.Create(Item());

        Assert.True(created.IsSuccess);
        var item = created.Value;
        Assert.Equal("SALES-01", item.KpiCode);
        Assert.Equal("Monthly sales", item.KpiName);
        Assert.Null(item.Unit);
        Assert.Equal(50m, item.Weight);
        Assert.Null(item.ActualValue);
        Assert.Null(item.AchievementPct);
        Assert.Null(item.WeightedScore);
        Assert.Equal(KpiScoringMethod.DirectPercentage, item.Rule.ScoringMethod);
    }

    [Fact]
    public void An_item_refuses_an_inconsistent_rule_and_a_weight_outside_the_range()
    {
        var lowerWithoutCap = ScorecardItem.Create(Item(direction: KpiDirection.LowerIsBetter, scoring: KpiScoringMethod.CappedPercentage, cap: null));
        var noWeight = ScorecardItem.Create(Item(weight: 0m));
        var tooHeavy = ScorecardItem.Create(Item(weight: 100.5m));

        Assert.Contains("maxAchievementPct", lowerWithoutCap.Error.FieldErrors!.Keys);
        Assert.Contains("weight", noWeight.Error.FieldErrors!.Keys);
        Assert.Contains("weight", tooHeavy.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void An_item_cannot_end_before_it_starts()
    {
        var result = ScorecardItem.Create(Item() with { EndDate = Day.AddDays(-1) });

        Assert.Contains("endDate", result.Error.FieldErrors!.Keys);
    }

    [Theory]
    [InlineData(ReviewActionType.Reject)]
    [InlineData(ReviewActionType.Return)]
    public void Rejecting_or_returning_needs_a_reason(ReviewActionType action)
    {
        var withoutReason = ReviewAction.Create(1, ReviewStep.Manager, action, ScorecardStatus.UnderReview, ScorecardStatus.Rejected, 5, 6, "Aye Test", "  ", null, DateTime.UtcNow);
        var withReason = ReviewAction.Create(1, ReviewStep.Manager, action, ScorecardStatus.UnderReview, ScorecardStatus.Rejected, 5, 6, "Aye Test", " Needs evidence ", null, DateTime.UtcNow);

        Assert.True(withoutReason.IsFailure);
        Assert.Contains("comment", withoutReason.Error.FieldErrors!.Keys);
        Assert.Equal("Needs evidence", withReason.Value.Comment);
    }

    [Fact]
    public void An_approval_needs_no_comment_and_keeps_the_administrator_without_an_employee()
    {
        var created = ReviewAction.Create(1, ReviewStep.HR, ReviewActionType.Approve, ScorecardStatus.Approved, ScorecardStatus.Finalized, 5, null, "Aye Test", null, 87.5m, new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc));

        Assert.True(created.IsSuccess);
        Assert.Null(created.Value.ActorEmployeeId);
        Assert.Null(created.Value.Comment);
        Assert.Equal(87.5m, created.Value.OverallScore);
        Assert.Equal(ReviewStep.HR, created.Value.Step);
    }

    [Fact]
    public void A_review_action_needs_an_actor_name()
    {
        var result = ReviewAction.Create(1, ReviewStep.Employee, ReviewActionType.Submit, ScorecardStatus.Draft, ScorecardStatus.Submitted, 5, 6, " ", null, null, DateTime.UtcNow);

        Assert.Contains("actorName", result.Error.FieldErrors!.Keys);
    }

    private const string Key = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void An_attachment_is_metadata_with_a_server_made_storage_key()
    {
        var created = KpiAttachment.Create(1, " evidence.pdf ", "application/pdf", 2048, Key, Day);

        Assert.True(created.IsSuccess);
        Assert.Equal("evidence.pdf", created.Value.FileName);
        Assert.Equal(Key, created.Value.StorageKey);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(10485761L)]
    public void An_attachment_is_larger_than_0_and_at_most_10_megabytes(long size)
    {
        var result = KpiAttachment.Create(1, "evidence.pdf", "application/pdf", size, Key, Day);

        Assert.Contains("sizeBytes", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void An_attachment_of_exactly_10_megabytes_is_accepted() =>
        Assert.True(KpiAttachment.Create(1, "evidence.pdf", "application/pdf", 10485760L, Key, Day).IsSuccess);

    [Theory]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef")]
    [InlineData("../../etc/passwd/0123456789abcdef0")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    [InlineData("")]
    public void A_storage_key_is_32_lower_case_hexadecimal_characters_and_never_a_path(string key)
    {
        Assert.False(KpiAttachment.IsValidStorageKey(key));
        Assert.Contains("storageKey", KpiAttachment.Create(1, "evidence.pdf", "application/pdf", 10, key, Day).Error.FieldErrors!.Keys);
    }
}
