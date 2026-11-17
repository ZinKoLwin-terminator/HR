using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Domain.Tests.Performance;

/// <summary>The life of a scorecard after the plan: results, submit, review, approve, return, reject and the HR finalize, each with its valid and invalid statuses.</summary>
public sealed class ScorecardWorkflowTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly DateOnly Later = new(2026, 11, 20);
    private static readonly DateTime Now = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
    private static readonly ReviewActor Hr = new(5, null, "HR Test");
    private static readonly ReviewActor Employee = new(10, 100, "Mia Hartley");
    private static readonly ReviewActor Manager = new(11, 101, "Anna Lead");
    private static readonly ScorecardScore Score = new([new KpiItemScore(90m, 90m)], 90m);
    private static readonly ScorecardScore OtherScore = new([new KpiItemScore(95m, 95m)], 95m);

    private static readonly ScorecardStatus[] AllStatuses = Enum.GetValues<ScorecardStatus>();

    public static TheoryData<ScorecardStatus> Statuses => [.. AllStatuses];

    private static ScorecardItem NewItem(
        KpiMeasurementType measurement = KpiMeasurementType.Number,
        KpiDirection direction = KpiDirection.HigherIsBetter,
        KpiScoringMethod scoring = KpiScoringMethod.DirectPercentage,
        decimal target = 100m,
        decimal weight = 100m,
        decimal? cap = null,
        decimal? maxRating = null) => ScorecardItem.Create(new ScorecardItemDetails(
        1, 2, 3, "SALES-01", "Monthly sales", measurement, null, direction, scoring, target, null, null, maxRating, weight, cap, Today, Today.AddDays(30), 1, 9, Today)).Value;

    /// <summary>A scorecard in the given status, reached through the real transitions.</summary>
    private static Scorecard In(ScorecardStatus status)
    {
        var scorecard = Scorecard.Create(10, 20, 1, 2, 3, 4, reviewerEmployeeId: null).Value;
        if (status == ScorecardStatus.Planning)
        {
            return scorecard;
        }

        Assert.True(scorecard.FinalizePlan(Today, 11, [100m], Hr, Now).IsSuccess);
        switch (status)
        {
            case ScorecardStatus.Submitted:
                Submit(scorecard);
                break;
            case ScorecardStatus.UnderReview:
                Submit(scorecard);
                Assert.True(scorecard.StartReview(Manager, Now).IsSuccess);
                break;
            case ScorecardStatus.Returned:
                Submit(scorecard);
                Assert.True(scorecard.Return(Manager, "Please check sales", [], Now).IsSuccess);
                break;
            case ScorecardStatus.Resubmitted:
                Submit(scorecard);
                Assert.True(scorecard.Return(Manager, "Please check sales", [], Now).IsSuccess);
                Submit(scorecard);
                break;
            case ScorecardStatus.Approved:
                Submit(scorecard);
                Assert.True(scorecard.Approve(true, Today, Score, "Exceeds", null, Manager, Now).IsSuccess);
                break;
            case ScorecardStatus.Rejected:
                Submit(scorecard);
                Assert.True(scorecard.Reject(Manager, "Not credible", Now).IsSuccess);
                break;
            case ScorecardStatus.Finalized:
                Submit(scorecard);
                Assert.True(scorecard.Approve(false, Today, Score, "Exceeds", null, Manager, Now).IsSuccess);
                break;
            default:
                break;
        }

        Assert.Equal(status, scorecard.Status);
        return scorecard;
    }

    private static void Submit(Scorecard scorecard) =>
        Assert.True(scorecard.Submit(Today, Score, "Exceeds", Employee, Now).IsSuccess);

    private static AppError Refused<T>(Result<T> result)
    {
        Assert.True(result.IsFailure);
        return result.Error;
    }

    private static void AssertConflictNaming(AppError error, ScorecardStatus status)
    {
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Contains(ScorecardStatusText.ToText(status), error.Message, StringComparison.Ordinal);
    }

    // ---- Results ----

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Results_can_only_be_saved_while_the_scorecard_is_draft_or_returned(ScorecardStatus status)
    {
        var scorecard = In(status);

        var result = scorecard.SaveResults("Good quarter");

        if (status is ScorecardStatus.Draft or ScorecardStatus.Returned)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal("Good quarter", scorecard.EmployeeComment);
        }
        else
        {
            AssertConflictNaming(result.Error, status);
            Assert.Null(scorecard.EmployeeComment);
        }
    }

    [Fact]
    public void Saving_results_does_not_score_the_scorecard_and_a_blank_comment_clears_it()
    {
        var scorecard = In(ScorecardStatus.Draft);
        Assert.True(scorecard.SaveResults("  First thoughts ").IsSuccess);

        Assert.Equal("First thoughts", scorecard.EmployeeComment);
        Assert.Null(scorecard.OverallScore);
        Assert.Null(scorecard.CalculationVersion);
        Assert.True(scorecard.SaveResults("   ").IsSuccess);
        Assert.Null(scorecard.EmployeeComment);
    }

    [Fact]
    public void A_comment_longer_than_1000_characters_is_refused_on_the_comment_field()
    {
        var scorecard = In(ScorecardStatus.Draft);

        var result = scorecard.SaveResults(new string('x', 1001));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("employeeComment", result.Error.FieldErrors!.Keys);
        Assert.True(scorecard.SaveResults(new string('x', 1000)).IsSuccess);
    }

    // ---- Submit ----

    [Fact]
    public void Submitting_a_draft_stores_the_score_the_label_the_version_and_the_date_and_writes_an_employee_step()
    {
        var scorecard = In(ScorecardStatus.Draft);

        var entry = scorecard.Submit(Later, Score, "Exceeds", Employee, Now);

        Assert.True(entry.IsSuccess);
        Assert.Equal(ScorecardStatus.Submitted, scorecard.Status);
        Assert.Equal((90m, "Exceeds", KpiCalculator.CalculationVersion, Later), (scorecard.OverallScore, scorecard.RatingLabel, scorecard.CalculationVersion, scorecard.SubmittedOn));
        Assert.Equal(
            (ReviewStep.Employee, ReviewActionType.Submit, ScorecardStatus.Draft, ScorecardStatus.Submitted, 10, 100, "Mia Hartley", 90m),
            (entry.Value.Step, entry.Value.Action, entry.Value.FromStatus, entry.Value.ToStatus, entry.Value.ActorUserId, entry.Value.ActorEmployeeId, entry.Value.ActorName, entry.Value.OverallScore));
    }

    [Fact]
    public void Submitting_a_returned_scorecard_is_a_resubmission_with_the_new_score()
    {
        var scorecard = In(ScorecardStatus.Returned);
        Assert.Null(scorecard.OverallScore);

        var entry = scorecard.Submit(Later, OtherScore, null, Employee, Now);

        Assert.Equal(ScorecardStatus.Resubmitted, scorecard.Status);
        Assert.Equal((ReviewActionType.Resubmit, ScorecardStatus.Returned, ScorecardStatus.Resubmitted), (entry.Value.Action, entry.Value.FromStatus, entry.Value.ToStatus));
        Assert.Equal((95m, null, Later), (scorecard.OverallScore, scorecard.RatingLabel, scorecard.SubmittedOn));
    }

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Only_a_draft_or_returned_scorecard_can_be_submitted(ScorecardStatus status)
    {
        var scorecard = In(status);
        var allowed = status is ScorecardStatus.Draft or ScorecardStatus.Returned;

        var precheck = scorecard.EnsureCanSubmit();

        var result = scorecard.Submit(Today, OtherScore, "Outstanding", Employee, Now);

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed, precheck.IsSuccess);
        if (!allowed)
        {
            AssertConflictNaming(result.Error, status);
            Assert.Equal(status, scorecard.Status);
            Assert.NotEqual(95m, scorecard.OverallScore);
        }
    }

    // ---- Start review ----

    [Theory]
    [MemberData(nameof(Statuses))]
    public void A_review_starts_only_from_submitted_or_resubmitted(ScorecardStatus status)
    {
        var scorecard = In(status);
        var allowed = status is ScorecardStatus.Submitted or ScorecardStatus.Resubmitted;

        var result = scorecard.StartReview(Manager, Now);

        Assert.Equal(allowed, result.IsSuccess);
        if (allowed)
        {
            Assert.Equal(ScorecardStatus.UnderReview, scorecard.Status);
            Assert.Equal((ReviewStep.Manager, ReviewActionType.StartReview, status, ScorecardStatus.UnderReview), (result.Value.Step, result.Value.Action, result.Value.FromStatus, result.Value.ToStatus));
        }
        else
        {
            AssertConflictNaming(result.Error, status);
            Assert.Equal(status, scorecard.Status);
        }
    }

    // ---- Review draft ----

    [Theory]
    [MemberData(nameof(Statuses))]
    public void A_review_draft_is_saved_only_in_the_manager_stage_and_changes_no_status(ScorecardStatus status)
    {
        var scorecard = In(status);
        var allowed = status is ScorecardStatus.Submitted or ScorecardStatus.Resubmitted or ScorecardStatus.UnderReview;

        var result = scorecard.SaveReview(4.25m, " Solid ");

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(status, scorecard.Status);
        Assert.Equal(allowed ? (4.25m, "Solid") : (null, null), (scorecard.ManagerRating, scorecard.ManagerComment));
        if (!allowed)
        {
            AssertConflictNaming(result.Error, status);
        }
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(5.01)]
    [InlineData(3.333)]
    public void A_manager_rating_outside_1_to_5_or_with_more_than_two_decimals_is_refused(double rating)
    {
        var scorecard = In(ScorecardStatus.Submitted);

        var result = scorecard.SaveReview((decimal)rating, null);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("managerRating", result.Error.FieldErrors!.Keys);
        Assert.Null(scorecard.ManagerRating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(2.5)]
    public void A_manager_rating_from_1_to_5_is_stored_and_null_clears_it(double rating)
    {
        var scorecard = In(ScorecardStatus.UnderReview);

        Assert.True(scorecard.SaveReview((decimal)rating, null).IsSuccess);
        Assert.Equal((decimal)rating, scorecard.ManagerRating);
        Assert.True(scorecard.SaveReview(null, null).IsSuccess);
        Assert.Null(scorecard.ManagerRating);
    }

    // ---- Approve ----

    [Theory]
    [InlineData(ScorecardStatus.Submitted)]
    [InlineData(ScorecardStatus.Resubmitted)]
    [InlineData(ScorecardStatus.UnderReview)]
    public void Approving_in_a_period_with_an_hr_review_waits_for_hr_with_the_recalculated_score(ScorecardStatus from)
    {
        var scorecard = In(from);

        var entry = scorecard.Approve(requiresHrReview: true, Later, OtherScore, "Outstanding", "Well done", Manager, Now);

        Assert.True(entry.IsSuccess);
        Assert.Equal(ScorecardStatus.Approved, scorecard.Status);
        Assert.Equal((95m, "Outstanding", Later), (scorecard.OverallScore, scorecard.RatingLabel, scorecard.ReviewedOn));
        Assert.Null(scorecard.FinalizedOn);
        Assert.Null(scorecard.FinalizedByUserId);
        Assert.Equal(
            (ReviewStep.Manager, ReviewActionType.Approve, from, ScorecardStatus.Approved, "Well done", 95m),
            (entry.Value.Step, entry.Value.Action, entry.Value.FromStatus, entry.Value.ToStatus, entry.Value.Comment, entry.Value.OverallScore));
    }

    [Fact]
    public void Approving_in_a_period_without_an_hr_review_finalizes_the_scorecard_at_once()
    {
        var scorecard = In(ScorecardStatus.UnderReview);

        var entry = scorecard.Approve(requiresHrReview: false, Later, OtherScore, "Outstanding", null, Manager, Now);

        Assert.Equal(ScorecardStatus.Finalized, scorecard.Status);
        Assert.Equal((Later, 11, Later, 95m), (scorecard.FinalizedOn, scorecard.FinalizedByUserId, scorecard.ReviewedOn, scorecard.OverallScore));
        Assert.Equal((ReviewActionType.Approve, ScorecardStatus.Finalized), (entry.Value.Action, entry.Value.ToStatus));
    }

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Only_the_manager_stage_can_be_approved(ScorecardStatus status)
    {
        var scorecard = In(status);
        var allowed = status is ScorecardStatus.Submitted or ScorecardStatus.Resubmitted or ScorecardStatus.UnderReview;

        var precheck = scorecard.EnsureCanApprove();

        var result = scorecard.Approve(true, Today, OtherScore, null, null, Manager, Now);

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed, precheck.IsSuccess);
        if (!allowed)
        {
            AssertConflictNaming(result.Error, status);
            Assert.Equal(status, scorecard.Status);
        }
    }

    [Fact]
    public void An_approval_comment_longer_than_1000_characters_changes_nothing()
    {
        var scorecard = In(ScorecardStatus.Submitted);

        var result = scorecard.Approve(false, Today, OtherScore, null, new string('c', 1001), Manager, Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(ScorecardStatus.Submitted, scorecard.Status);
        Assert.Equal(90m, scorecard.OverallScore);
    }

    // ---- Return and reject ----

    [Theory]
    [InlineData(ScorecardStatus.Submitted, ReviewStep.Manager)]
    [InlineData(ScorecardStatus.Resubmitted, ReviewStep.Manager)]
    [InlineData(ScorecardStatus.UnderReview, ReviewStep.Manager)]
    [InlineData(ScorecardStatus.Approved, ReviewStep.HR)]
    public void Returning_goes_back_to_the_employee_with_the_reason_on_the_step_of_the_stage_and_clears_the_score(ScorecardStatus from, ReviewStep step)
    {
        var scorecard = In(from);

        var entry = scorecard.Return(Manager, "  Evidence missing ", [], Now);

        Assert.True(entry.IsSuccess);
        Assert.Equal(ScorecardStatus.Returned, scorecard.Status);
        Assert.Equal((step, ReviewActionType.Return, from, ScorecardStatus.Returned, "Evidence missing", 90m), (entry.Value.Step, entry.Value.Action, entry.Value.FromStatus, entry.Value.ToStatus, entry.Value.Comment, entry.Value.OverallScore));
        Assert.Null(scorecard.OverallScore);
        Assert.Null(scorecard.RatingLabel);
        Assert.Null(scorecard.CalculationVersion);
        Assert.Null(scorecard.ReviewedOn);
    }

    /// <summary>An item of the scorecard built by <see cref="In"/> (its id is 0 until it is saved).</summary>
    private static ScorecardItem ItemOf(Scorecard scorecard, string code) => ScorecardItem.Create(new ScorecardItemDetails(
        scorecard.Id, 2, 3, code, "Monthly sales", KpiMeasurementType.Number, null, KpiDirection.HigherIsBetter, KpiScoringMethod.DirectPercentage,
        100m, null, null, null, 50m, null, Today, Today.AddDays(30), 1, 9, Today)).Value;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Returning_clears_the_review_draft_of_the_scorecard_and_of_every_item_so_it_is_never_kept(bool fromApproved)
    {
        var scorecard = In(ScorecardStatus.UnderReview);
        ScorecardItem[] items = [ItemOf(scorecard, "A-1"), ItemOf(scorecard, "A-2")];
        Assert.True(scorecard.SaveReview(4m, "Draft opinion").IsSuccess);
        Assert.True(items[0].RecordReview(5m, "Strong").IsSuccess);
        Assert.True(items[1].RecordReview(2m, "Weak").IsSuccess);
        if (fromApproved)
        {
            Assert.True(scorecard.Approve(true, Today, Score, "Exceeds", "Looks fine", Manager, Now).IsSuccess);
        }

        var entry = scorecard.Return(fromApproved ? Hr : Manager, "Recheck A-2", items, Now);

        Assert.True(entry.IsSuccess);
        Assert.Equal("Recheck A-2", entry.Value.Comment);
        Assert.Equal((null, null, null), (scorecard.ManagerRating, scorecard.ManagerComment, scorecard.HrComment));
        Assert.All(items, item => Assert.Equal((null, null), (item.ManagerRating, item.ManagerComment)));
    }

    [Fact]
    public void A_refused_return_keeps_the_review_draft_and_an_item_of_another_scorecard_is_a_programming_error()
    {
        var scorecard = In(ScorecardStatus.UnderReview);
        var item = ItemOf(scorecard, "A-1");
        Assert.True(scorecard.SaveReview(4m, "Draft opinion").IsSuccess);
        Assert.True(item.RecordReview(5m, "Strong").IsSuccess);

        Refused(scorecard.Return(Manager, "  ", [item], Now));

        Assert.Equal((4m, "Draft opinion"), (scorecard.ManagerRating, scorecard.ManagerComment));
        Assert.Equal((5m, "Strong"), (item.ManagerRating, item.ManagerComment));
        Assert.Throws<ArgumentException>(() => scorecard.Return(Manager, "reason", [NewItem()], Now));
        Assert.Equal(ScorecardStatus.UnderReview, scorecard.Status);
    }

    [Theory]
    [InlineData(ScorecardStatus.Submitted, ReviewStep.Manager)]
    [InlineData(ScorecardStatus.Resubmitted, ReviewStep.Manager)]
    [InlineData(ScorecardStatus.UnderReview, ReviewStep.Manager)]
    [InlineData(ScorecardStatus.Approved, ReviewStep.HR)]
    public void Rejecting_is_final_with_the_reason_on_the_step_of_the_stage_and_keeps_the_score(ScorecardStatus from, ReviewStep step)
    {
        var scorecard = In(from);

        var entry = scorecard.Reject(Manager, "Not credible", Now);

        Assert.True(entry.IsSuccess);
        Assert.Equal(ScorecardStatus.Rejected, scorecard.Status);
        Assert.Equal((step, ReviewActionType.Reject, from, ScorecardStatus.Rejected, "Not credible", 90m), (entry.Value.Step, entry.Value.Action, entry.Value.FromStatus, entry.Value.ToStatus, entry.Value.Comment, entry.Value.OverallScore));
        Assert.Equal(90m, scorecard.OverallScore);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returning_or_rejecting_without_a_reason_is_a_validation_error_on_comment_and_changes_nothing(string? reason)
    {
        var returned = In(ScorecardStatus.UnderReview);
        var rejected = In(ScorecardStatus.UnderReview);

        var returnResult = returned.Return(Manager, reason, [], Now);
        var rejectResult = rejected.Reject(Manager, reason, Now);

        foreach (var error in new[] { Refused(returnResult), Refused(rejectResult) })
        {
            Assert.Equal(ErrorType.Validation, error.Type);
            Assert.Equal("A reason is required.", error.FieldErrors!["comment"][0]);
        }

        Assert.Equal(ScorecardStatus.UnderReview, returned.Status);
        Assert.Equal(ScorecardStatus.UnderReview, rejected.Status);
        Assert.Equal(90m, returned.OverallScore);
    }

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Return_and_reject_work_only_from_the_manager_stage_and_approved(ScorecardStatus status)
    {
        var allowed = status is ScorecardStatus.Submitted or ScorecardStatus.Resubmitted or ScorecardStatus.UnderReview or ScorecardStatus.Approved;
        var toReturn = In(status);
        var toReject = In(status);

        var returned = toReturn.Return(Manager, "reason", [], Now);
        var rejected = toReject.Reject(Manager, "reason", Now);

        Assert.Equal(allowed, returned.IsSuccess);
        Assert.Equal(allowed, rejected.IsSuccess);
        if (!allowed)
        {
            AssertConflictNaming(returned.Error, status);
            AssertConflictNaming(rejected.Error, status);
            Assert.Equal(status, toReturn.Status);
            Assert.Equal(status, toReject.Status);
        }
    }

    [Fact]
    public void A_rejected_scorecard_accepts_no_further_action()
    {
        var scorecard = In(ScorecardStatus.Rejected);

        AssertConflictNaming(scorecard.SaveResults("x").Error, ScorecardStatus.Rejected);
        AssertConflictNaming(Refused(scorecard.Submit(Today, Score, null, Employee, Now)), ScorecardStatus.Rejected);
        AssertConflictNaming(Refused(scorecard.StartReview(Manager, Now)), ScorecardStatus.Rejected);
        AssertConflictNaming(Refused(scorecard.Approve(true, Today, Score, null, null, Manager, Now)), ScorecardStatus.Rejected);
        AssertConflictNaming(Refused(scorecard.Return(Manager, "r", [], Now)), ScorecardStatus.Rejected);
        AssertConflictNaming(Refused(scorecard.Reject(Manager, "r", Now)), ScorecardStatus.Rejected);
        AssertConflictNaming(Refused(scorecard.FinalizeByHr(Today, null, Hr, Now)), ScorecardStatus.Rejected);
        Assert.Equal(ScorecardStatus.Rejected, scorecard.Status);
    }

    // ---- HR finalize ----

    [Fact]
    public void Hr_finalizes_an_approved_scorecard_and_the_comment_is_kept_as_the_hr_comment()
    {
        var scorecard = In(ScorecardStatus.Approved);

        var entry = scorecard.FinalizeByHr(Later, " Calibrated ", Hr, Now);

        Assert.Equal(ScorecardStatus.Finalized, scorecard.Status);
        Assert.Equal((Later, 5, "Calibrated", 90m), (scorecard.FinalizedOn, scorecard.FinalizedByUserId, scorecard.HrComment, scorecard.OverallScore));
        Assert.Equal(
            (ReviewStep.HR, ReviewActionType.Finalize, ScorecardStatus.Approved, ScorecardStatus.Finalized, "Calibrated"),
            (entry.Value.Step, entry.Value.Action, entry.Value.FromStatus, entry.Value.ToStatus, entry.Value.Comment));
    }

    [Theory]
    [MemberData(nameof(Statuses))]
    public void Only_an_approved_scorecard_can_be_finalized_by_hr(ScorecardStatus status)
    {
        var scorecard = In(status);
        var allowed = status == ScorecardStatus.Approved;

        var result = scorecard.FinalizeByHr(Today, null, Hr, Now);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            AssertConflictNaming(result.Error, status);
            Assert.Equal(status, scorecard.Status);
        }
    }

    [Fact]
    public void A_finalized_scorecard_accepts_no_further_action()
    {
        var scorecard = In(ScorecardStatus.Finalized);

        AssertConflictNaming(scorecard.SaveResults("x").Error, ScorecardStatus.Finalized);
        AssertConflictNaming(Refused(scorecard.Submit(Today, Score, null, Employee, Now)), ScorecardStatus.Finalized);
        AssertConflictNaming(Refused(scorecard.Approve(true, Today, Score, null, null, Manager, Now)), ScorecardStatus.Finalized);
        AssertConflictNaming(Refused(scorecard.Return(Manager, "r", [], Now)), ScorecardStatus.Finalized);
        AssertConflictNaming(Refused(scorecard.FinalizeByHr(Today, null, Hr, Now)), ScorecardStatus.Finalized);
    }

    // ---- The history entry ----

    [Fact]
    public void A_history_comment_longer_than_1000_characters_is_refused()
    {
        var entry = ReviewAction.Create(1, ReviewStep.Manager, ReviewActionType.Approve, ScorecardStatus.Submitted, ScorecardStatus.Finalized, 1, null, "Admin", new string('c', 1001), null, Now);

        Assert.Equal(ErrorType.Validation, entry.Error.Type);
        Assert.Contains("comment", entry.Error.FieldErrors!.Keys);
    }

    // ---- Item results ----

    [Fact]
    public void An_actual_result_stores_the_achievement_and_the_weighted_score_and_null_clears_both()
    {
        var item = NewItem(target: 200m, weight: 40m);

        Assert.True(item.RecordResult(150m, " On track ").IsSuccess);
        Assert.Equal((150m, "On track", 75m, 30m), (item.ActualValue, item.EmployeeComment, item.AchievementPct, item.WeightedScore));

        Assert.True(item.RecordResult(null, null).IsSuccess);
        Assert.Equal((null, null, null, null), (item.ActualValue, item.EmployeeComment, item.AchievementPct, item.WeightedScore));
    }

    [Fact]
    public void A_boolean_result_of_one_half_is_refused_on_actual_value_and_changes_nothing()
    {
        var item = NewItem(KpiMeasurementType.Boolean, KpiDirection.HigherIsBetter, KpiScoringMethod.Boolean, target: 1m);
        Assert.True(item.RecordResult(1m, null).IsSuccess);

        var result = item.RecordResult(0.5m, "changed");

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("actualValue", result.Error.FieldErrors!.Keys);
        Assert.Equal((1m, null, 100m), (item.ActualValue, item.EmployeeComment, item.AchievementPct));
    }

    [Fact]
    public void A_rating_above_the_scale_and_a_negative_lower_is_better_result_are_refused()
    {
        var rating = NewItem(KpiMeasurementType.Rating, KpiDirection.HigherIsBetter, KpiScoringMethod.RatingBased, target: 5m, maxRating: 5m);
        var lower = NewItem(KpiMeasurementType.Number, KpiDirection.LowerIsBetter, KpiScoringMethod.CappedPercentage, target: 10m, cap: 120m);

        Assert.Contains("actualValue", rating.CheckResult(5.5m, null).Error.FieldErrors!.Keys);
        Assert.Contains("actualValue", lower.CheckResult(-1m, null).Error.FieldErrors!.Keys);
        Assert.True(rating.CheckResult(5m, null).IsSuccess);
        Assert.True(lower.CheckResult(0m, null).IsSuccess);
    }

    [Fact]
    public void An_item_comment_longer_than_1000_characters_is_refused_on_employee_comment()
    {
        var item = NewItem();

        var result = item.RecordResult(10m, new string('c', 1001));

        Assert.Contains("employeeComment", result.Error.FieldErrors!.Keys);
        Assert.Null(item.ActualValue);
    }

    [Fact]
    public void An_item_score_from_the_calculator_replaces_the_stored_figures()
    {
        var item = NewItem();
        item.RecordResult(50m, null);

        item.ApplyScore(new KpiItemScore(80m, 80m));

        Assert.Equal((80m, 80m), (item.AchievementPct, item.WeightedScore));
    }

    [Fact]
    public void An_item_review_stores_a_rating_from_1_to_5_and_a_comment()
    {
        var item = NewItem();

        Assert.True(item.RecordReview(4m, " Good ").IsSuccess);
        Assert.Equal((4m, "Good"), (item.ManagerRating, item.ManagerComment));
        Assert.Contains("managerRating", item.RecordReview(6m, null).Error.FieldErrors!.Keys);
        Assert.Contains("managerRating", item.RecordReview(0m, null).Error.FieldErrors!.Keys);
        Assert.Contains("managerComment", item.RecordReview(3m, new string('c', 1001)).Error.FieldErrors!.Keys);
        Assert.Equal(4m, item.ManagerRating);
        Assert.True(item.RecordReview(null, null).IsSuccess);
        Assert.Null(item.ManagerRating);
    }

    // ---- The period ----

    [Theory]
    [InlineData(EvaluationPeriodStatus.Draft, false, false)]
    [InlineData(EvaluationPeriodStatus.Active, true, true)]
    [InlineData(EvaluationPeriodStatus.Completed, false, true)]
    [InlineData(EvaluationPeriodStatus.Closed, false, false)]
    public void Results_need_an_active_period_and_reviews_an_active_or_completed_one(EvaluationPeriodStatus status, bool results, bool review)
    {
        var period = EvaluationPeriod.Create(new EvaluationPeriodDetails(1, "2026 Q4", KpiFrequency.Quarterly, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), null, false)).Value;
        if (status != EvaluationPeriodStatus.Draft)
        {
            Assert.True(period.Activate().IsSuccess);
        }

        if (status is EvaluationPeriodStatus.Completed or EvaluationPeriodStatus.Closed)
        {
            Assert.True(period.Complete().IsSuccess);
        }

        if (status == EvaluationPeriodStatus.Closed)
        {
            Assert.True(period.Close().IsSuccess);
        }

        Assert.Equal(results, period.EnsureAcceptsResults().IsSuccess);
        Assert.Equal(review, period.EnsureAllowsReview().IsSuccess);
        if (!results)
        {
            Assert.Equal(ErrorType.Conflict, period.EnsureAcceptsResults().Error.Type);
            Assert.Contains(EvaluationPeriodStatusText.ToText(status), period.EnsureAcceptsResults().Error.Message, StringComparison.Ordinal);
        }
    }
}
