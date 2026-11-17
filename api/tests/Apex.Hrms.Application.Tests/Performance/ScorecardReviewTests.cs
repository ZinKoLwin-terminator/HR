using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using static Apex.Hrms.Application.Tests.Fakes.EmployeeFixtures;
using static Apex.Hrms.Application.Tests.Fakes.ReviewFixtures;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// The reviewer's part: start review, review draft, approve, return, reject and the HR final review: who may act (reviewer, HR tier, never
/// the employee, separation of duties), the status and period rules, the recalculated score and the history.
/// </summary>
public sealed class ScorecardReviewTests : ScorecardWorkflowTestBase
{
    private TestHost host => Host;

    private const string NotReviewer = "Only the employee's reviewer or HR can review this scorecard.";

    // ---- Start review ----

    [Fact]
    public async Task The_reviewer_starts_the_review_and_a_manager_step_is_written()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);

        var started = ResultAssert.Succeeded(await host.StartReview(submitted));

        Assert.Equal("Under Review", started.Status);
        var entry = Assert.Single(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.StartReview);
        Assert.Equal((ReviewStep.Manager, ScorecardStatus.Submitted, ScorecardStatus.UnderReview, 21, world.Anna.Id, "Anna Lead"), (entry.Step, entry.FromStatus, entry.ToStatus, entry.ActorUserId, entry.ActorEmployeeId, entry.ActorName));
        Assert.Equal("Review of the scorecard of EMP-0002 for \"2026 Q4\" started", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task A_resubmitted_scorecard_can_be_put_under_review()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var returned = ResultAssert.Succeeded(await host.Return(submitted, "Look again"));
        AsEmployee(world.Ben, 20);
        var again = ResultAssert.Succeeded(await host.Submit(returned));
        AsManager(world.Anna, 21, world.Ben.Id);

        Assert.Equal("Under Review", ResultAssert.Succeeded(await host.StartReview(again)).Status);
    }

    [Fact]
    public async Task A_review_cannot_start_from_draft_or_twice()
    {
        var world = await SeedAsync();
        AsManager(world.Anna, 21, world.Ben.Id);

        var draft = ResultAssert.Failed(await host.StartReview(world.Card), ErrorType.Conflict);
        Assert.Contains("Draft", draft.Message, StringComparison.Ordinal);

        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var started = ResultAssert.Succeeded(await host.StartReview(submitted));
        var twice = ResultAssert.Failed(await host.StartReview(started), ErrorType.Conflict);
        Assert.Contains("Under Review", twice.Message, StringComparison.Ordinal);
    }

    // ---- Who may review ----

    [Fact]
    public async Task The_employee_never_reviews_their_own_scorecard_even_with_every_grant_and_in_every_step()
    {
        var world = await SeedAsync();
        var hrCard = await SubmittedCardAsync(world, world.Hr, 22, "H-1");
        AsHr(world.Hr, 22);

        foreach (var attempt in new Func<Task<Result<ScorecardDetailDto>>>[]
        {
            () => host.StartReview(hrCard),
            () => host.Approve(hrCard),
            () => host.Return(hrCard, "no"),
            () => host.Reject(hrCard, "no"),
            () => host.FinalizeByHr(hrCard),
            () => host.SaveReview(hrCard.Id, ReviewOf(hrCard, 3m, null)),
        })
        {
            var error = ResultAssert.Failed(await attempt(), ErrorType.Conflict);
            Assert.Equal("You cannot review your own scorecard.", error.Message);
        }

        Assert.Equal(ScorecardStatus.Submitted, Stored(hrCard.Id).Status);
    }

    [Fact]
    public async Task A_manager_cannot_review_their_own_scorecard_either()
    {
        var world = await SeedAsync();
        var annaCard = await SubmittedCardAsync(world, world.Anna, 21, "N-1");
        AsManager(world.Anna, 21, world.Ben.Id);

        var error = ResultAssert.Failed(await host.Approve(annaCard), ErrorType.Conflict);

        Assert.Equal("You cannot review your own scorecard.", error.Message);
    }

    [Fact]
    public async Task The_reviewer_the_hr_tier_and_the_built_in_administrator_may_review_but_another_manager_may_not()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);

        AsManager(world.Dan, 24, world.Ben.Id);
        Assert.Equal(NotReviewer, ResultAssert.Failed(await host.StartReview(submitted), ErrorType.Forbidden).Message);
        Assert.Equal(NotReviewer, ResultAssert.Failed(await host.Approve(submitted), ErrorType.Forbidden).Message);
        Assert.Equal(NotReviewer, ResultAssert.Failed(await host.Return(submitted, "x"), ErrorType.Forbidden).Message);
        Assert.Equal(NotReviewer, ResultAssert.Failed(await host.Reject(submitted, "x"), ErrorType.Forbidden).Message);
        Assert.Equal(NotReviewer, ResultAssert.Failed(await host.SaveReview(submitted.Id, ReviewOf(submitted, 3m, null)), ErrorType.Forbidden).Message);
        Assert.Equal(ScorecardStatus.Submitted, Stored(submitted.Id).Status);

        AsHr(null, 1);
        var started = ResultAssert.Succeeded(await host.StartReview(submitted));
        AsHr(world.Hr, 22);
        Assert.Equal("Finalized", ResultAssert.Succeeded(await host.Approve(started)).Status);
    }

    [Fact]
    public async Task A_manager_who_cannot_see_the_scorecard_gets_a_404()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Dan, 24);

        ResultAssert.Failed(await host.StartReview(submitted), ErrorType.NotFound);
        ResultAssert.Failed(await host.Approve(submitted), ErrorType.NotFound);
        ResultAssert.Failed(await host.FinalizeByHr(submitted), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_scorecard_without_a_reviewer_can_only_be_reviewed_by_the_hr_tier()
    {
        var world = await SeedAsync();
        var danCard = await SubmittedCardAsync(world, world.Dan, 24, "D-1");
        Assert.Null(danCard.ReviewerEmployeeId);

        AsManager(world.Anna, 21, world.Dan.Id);
        Assert.Equal(NotReviewer, ResultAssert.Failed(await host.StartReview(danCard), ErrorType.Forbidden).Message);

        AsHr(world.Hr, 22);
        Assert.Equal("Under Review", ResultAssert.Succeeded(await host.StartReview(danCard)).Status);
    }

    [Fact]
    public async Task The_reviewer_is_fixed_when_the_plan_is_finalized_and_a_later_manager_change_does_not_move_it()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        Assert.Equal(world.Anna.Id, submitted.ReviewerEmployeeId);
        AsHr(null, 1);
        var ben = ResultAssert.Succeeded(await host.GetEmployee(world.Ben.Id));
        ResultAssert.Succeeded(await host.UpdateEmployee(world.Ben.Id, UpdateOf(ben) with { ManagerId = world.Dan.Id }));

        AsManager(world.Dan, 24, world.Ben.Id);
        ResultAssert.Failed(await host.StartReview(submitted), ErrorType.Forbidden);
        AsManager(world.Anna, 21);
        var started = ResultAssert.Succeeded(await host.StartReview(submitted));
        Assert.Equal("Under Review", started.Status);
    }

    [Fact]
    public async Task The_team_tier_sees_the_scorecards_it_reviews_after_the_reporting_line_changed_and_not_a_strangers()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsHr(null, 1);
        var ben = ResultAssert.Succeeded(await host.GetEmployee(world.Ben.Id));
        ResultAssert.Succeeded(await host.UpdateEmployee(world.Ben.Id, UpdateOf(ben) with { ManagerId = world.Dan.Id }));

        AsManager(world.Anna, 21);
        Assert.True((await host.GetScorecard(submitted.Id)).IsSuccess);
        Assert.Equal([submitted.Id], ResultAssert.Succeeded(await host.QueryScorecards()).Items.Select(card => card.Id).ToArray());
        AsManager(world.Dan, 24, world.Ben.Id);
        Assert.True((await host.GetScorecard(submitted.Id)).IsSuccess);
        AsManager(world.Dan, 24);
        ResultAssert.Failed(await host.GetScorecard(submitted.Id), ErrorType.NotFound);
    }

    // ---- Review draft ----

    [Fact]
    public async Task The_review_draft_saves_the_overall_and_per_kpi_ratings_without_a_status_change_or_history_entry()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var before = HistoryOf(world.Card.Id).Count();

        var saved = ResultAssert.Succeeded(await host.SaveReview(
            submitted.Id,
            ReviewOf(submitted, 4.5m, " Strong ", Rating(Of(submitted, "A-1"), 5m, "Great"), Rating(Of(submitted, "A-2"), 3.25m))));

        Assert.Equal(("Submitted", 4.5m, "Strong"), (saved.Status, saved.ManagerRating, saved.ManagerComment));
        Assert.Equal((5m, "Great"), (Of(saved, "A-1").ManagerRating, Of(saved, "A-1").ManagerComment));
        Assert.Equal((3.25m, null), (Of(saved, "A-2").ManagerRating, Of(saved, "A-2").ManagerComment));
        Assert.Equal(before, HistoryOf(world.Card.Id).Count());
        Assert.NotEqual(submitted.RowVersion, saved.RowVersion);
        Assert.Equal("Review notes saved on the scorecard of EMP-0002 for \"2026 Q4\"", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task The_review_draft_is_validated_and_the_items_must_belong_to_the_scorecard()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var item = Of(submitted, "A-1");
        var valid = ReviewOf(submitted, 4m, null, Rating(item, 4m));

        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { RowVersion = null }), "rowVersion");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { ManagerRating = 0.5m }), "managerRating");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { ManagerRating = 5.5m }), "managerRating");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { ManagerRating = 3.333m }), "managerRating");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { ManagerComment = new string('c', 1001) }), "managerComment");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { Items = null }), "items");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { Items = [Rating(item, 6m)] }), "items[0].managerRating");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { Items = [Rating(item, 2m, new string('c', 1001))] }), "items[0].managerComment");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { Items = [new ReviewItemRequest(9999, 3m, null)] }), "items[0].id");
        ResultAssert.FailedOnField(await host.SaveReview(submitted.Id, valid with { Items = [Rating(item, 2m), Rating(item, 3m)] }), "items[1].id");
        Assert.Null(Stored(submitted.Id).ManagerRating);
    }

    [Fact]
    public async Task A_review_draft_cannot_be_saved_on_a_draft_or_an_approved_scorecard()
    {
        var world = await SeedAsync(hrReview: true);
        AsManager(world.Anna, 21, world.Ben.Id);
        var onDraft = ResultAssert.Failed(await host.SaveReview(world.Card.Id, ReviewOf(world.Card, 3m, null)), ErrorType.Conflict);
        Assert.Contains("Draft", onDraft.Message, StringComparison.Ordinal);

        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var approved = ResultAssert.Succeeded(await host.Approve(submitted));
        var onApproved = ResultAssert.Failed(await host.SaveReview(approved.Id, ReviewOf(approved, 3m, null)), ErrorType.Conflict);
        Assert.Contains("Approved", onApproved.Message, StringComparison.Ordinal);
    }

    // ---- Approve ----

    [Fact]
    public async Task Approving_in_a_period_without_an_hr_review_finalizes_with_the_recalculated_score_and_the_label()
    {
        var world = await SeedAsync();
        await host.CreateBandAsync("Meets Expectations", 70m);
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);

        var approved = ResultAssert.Succeeded(await host.Approve(submitted, " Well done "));

        Assert.Equal(("Finalized", 80m, "Meets Expectations", new DateOnly(2026, 11, 9)), (approved.Status, approved.OverallScore, approved.RatingLabel, approved.FinalizedOn));
        Assert.Equal(new DateOnly(2026, 11, 9), approved.ReviewedOn);
        var stored = Stored(world.Card.Id);
        Assert.Equal(21, stored.FinalizedByUserId);
        Assert.Equal(KpiCalculator.CalculationVersion, stored.CalculationVersion);
        var entry = Assert.Single(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.Approve);
        Assert.Equal((ReviewStep.Manager, ScorecardStatus.Submitted, ScorecardStatus.Finalized, "Well done", 80m, 21), (entry.Step, entry.FromStatus, entry.ToStatus, entry.Comment, entry.OverallScore, entry.ActorUserId));
        Assert.Equal("Scorecard of EMP-0002 for \"2026 Q4\" approved and finalized", host.ActivityLog.LastMessage);
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("Well done", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Approving_in_a_period_with_an_hr_review_waits_for_hr()
    {
        var world = await SeedAsync(hrReview: true);
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);

        var approved = ResultAssert.Succeeded(await host.Approve(submitted));

        Assert.Equal(("Approved", null, 80m), (approved.Status, approved.FinalizedOn, approved.OverallScore));
        Assert.Null(Stored(world.Card.Id).FinalizedByUserId);
        Assert.Equal(new DateOnly(2026, 11, 9), approved.ReviewedOn);
        Assert.Equal("Scorecard of EMP-0002 for \"2026 Q4\" approved, waiting for the HR review", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task Approving_calculates_the_score_again_from_the_stored_results_and_ignores_the_figures_stored_before()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        foreach (var item in host.Repository<ScorecardItem>().Items)
        {
            item.ApplyScore(new KpiItemScore(1m, 1m));
        }

        AsManager(world.Anna, 21, world.Ben.Id);
        var approved = ResultAssert.Succeeded(await host.Approve(submitted));

        Assert.Equal(80m, approved.OverallScore);
        Assert.Equal((100m, 60m, 50m, 20m), (Of(approved, "A-1").AchievementPct, Of(approved, "A-1").WeightedScore, Of(approved, "A-2").AchievementPct, Of(approved, "A-2").WeightedScore));
    }

    [Fact]
    public async Task Approving_from_under_review_works_and_a_second_approval_is_a_conflict()
    {
        var world = await SeedAsync(hrReview: true);
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var started = ResultAssert.Succeeded(await host.StartReview(submitted));
        var approved = ResultAssert.Succeeded(await host.Approve(started));

        var again = await host.Approve(approved);
        var stale = await host.Approve(started);

        Assert.Contains("Approved", ResultAssert.Failed(again, ErrorType.Conflict).Message, StringComparison.Ordinal);
        ResultAssert.Failed(stale, ErrorType.Conflict);
        Assert.Single(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.Approve);
    }

    [Fact]
    public async Task A_save_that_loses_the_race_for_the_row_version_is_a_conflict_with_no_second_approval_recorded()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("The record was changed or deleted by someone else. Reload it and try again.");

        var result = await host.Approve(submitted);

        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("approved", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Approving_needs_every_result_a_valid_comment_and_a_row_version()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);

        ResultAssert.FailedOnField(await host.Approve(submitted, new string('c', 1001)), "comment");
        ResultAssert.FailedOnField(await host.Approve(submitted, rowVersion: ""), "rowVersion");
        ResultAssert.Failed(await host.Approve(world.Card), ErrorType.Conflict);
    }

    [Fact]
    public async Task A_scorecard_in_another_status_cannot_be_approved()
    {
        var world = await SeedAsync();
        AsManager(world.Anna, 21, world.Ben.Id);

        var error = ResultAssert.Failed(await host.Approve(world.Card), ErrorType.Conflict);

        Assert.Contains("Draft", error.Message, StringComparison.Ordinal);
    }

    // ---- Return and reject ----

    [Fact]
    public async Task Returning_needs_a_reason_and_sends_the_scorecard_back_for_resubmission()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);

        foreach (var reason in new string?[] { null, "", "   " })
        {
            var error = ResultAssert.FailedOnField(await host.Return(submitted, reason), "comment");
            Assert.Equal("A reason is required.", error.FieldErrors!["comment"][0]);
            error = ResultAssert.FailedOnField(await host.Reject(submitted, reason), "comment");
            Assert.Equal("A reason is required.", error.FieldErrors!["comment"][0]);
        }

        ResultAssert.FailedOnField(await host.Return(submitted, new string('c', 1001)), "comment");
        var returned = ResultAssert.Succeeded(await host.Return(submitted, " A-2 looks low "));

        Assert.Equal(("Returned", null, null), (returned.Status, returned.OverallScore, returned.RatingLabel));
        var entry = Assert.Single(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.Return);
        Assert.Equal((ReviewStep.Manager, "A-2 looks low", 80m), (entry.Step, entry.Comment, entry.OverallScore));
        Assert.Equal("Scorecard of EMP-0002 for \"2026 Q4\" returned to the employee", host.ActivityLog.LastMessage);
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("looks low", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rejecting_is_final_and_nothing_can_be_done_afterwards()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var rejected = ResultAssert.Succeeded(await host.Reject(submitted, "Not credible"));
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal(80m, rejected.OverallScore);

        foreach (var attempt in new Func<Task<Result<ScorecardDetailDto>>>[]
        {
            () => host.StartReview(rejected),
            () => host.Approve(rejected),
            () => host.Return(rejected, "again"),
            () => host.Reject(rejected, "again"),
            () => host.SaveReview(rejected.Id, ReviewOf(rejected, 3m, null)),
        })
        {
            Assert.Contains("Rejected", ResultAssert.Failed(await attempt(), ErrorType.Conflict).Message, StringComparison.Ordinal);
        }

        AsHr(world.Hr, 22);
        Assert.Contains("Rejected", ResultAssert.Failed(await host.FinalizeByHr(rejected), ErrorType.Conflict).Message, StringComparison.Ordinal);
        AsEmployee(world.Ben, 20);
        ResultAssert.Failed(await host.SaveResults(rejected, Actual(Of(rejected, "A-1"), 5m)), ErrorType.Conflict);
        ResultAssert.Failed(await host.Submit(rejected), ErrorType.Conflict);
    }

    [Fact]
    public async Task A_returned_scorecard_cannot_be_returned_or_approved_before_it_is_resubmitted()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var returned = ResultAssert.Succeeded(await host.Return(submitted, "x"));

        Assert.Contains("Returned", ResultAssert.Failed(await host.Approve(returned), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("Returned", ResultAssert.Failed(await host.Return(returned, "y"), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("Returned", ResultAssert.Failed(await host.StartReview(returned), ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    // ---- The HR stage ----

    private async Task<ScorecardDetailDto> ApprovedAsync(World world, int approverUserId = 21)
    {
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, approverUserId, world.Ben.Id);
        return ResultAssert.Succeeded(await host.Approve(submitted));
    }

    [Fact]
    public async Task Hr_finalizes_an_approved_scorecard_and_the_comment_becomes_the_hr_comment()
    {
        var world = await SeedAsync(hrReview: true);
        var approved = await ApprovedAsync(world);
        AsHr(world.Hr, 22);

        var finalized = ResultAssert.Succeeded(await host.FinalizeByHr(approved, " Calibrated "));

        Assert.Equal(("Finalized", new DateOnly(2026, 11, 9), "Calibrated", 80m), (finalized.Status, finalized.FinalizedOn, finalized.HrComment, finalized.OverallScore));
        Assert.Equal(22, Stored(world.Card.Id).FinalizedByUserId);
        var entry = Assert.Single(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.Finalize);
        Assert.Equal((ReviewStep.HR, ScorecardStatus.Approved, ScorecardStatus.Finalized, "Calibrated", 22), (entry.Step, entry.FromStatus, entry.ToStatus, entry.Comment, entry.ActorUserId));
        Assert.Equal("Scorecard of EMP-0002 for \"2026 Q4\" finalized by HR", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task A_manager_who_is_not_hr_cannot_give_the_final_review()
    {
        var world = await SeedAsync(hrReview: true);
        var approved = await ApprovedAsync(world);
        AsManager(world.Anna, 25, world.Ben.Id);

        Assert.Equal("Only HR can give the final review of a scorecard.", ResultAssert.Failed(await host.FinalizeByHr(approved), ErrorType.Forbidden).Message);
        ResultAssert.Failed(await host.Return(approved, "no"), ErrorType.Forbidden);
        ResultAssert.Failed(await host.Reject(approved, "no"), ErrorType.Forbidden);
        Assert.Equal(ScorecardStatus.Approved, Stored(world.Card.Id).Status);
    }

    [Fact]
    public async Task The_person_who_approved_cannot_also_give_the_hr_final_review_but_another_hr_user_can()
    {
        var world = await SeedAsync(hrReview: true);
        var submitted = await SubmittedAsync(world);
        AsHr(world.Hr, 22);
        var approved = ResultAssert.Succeeded(await host.Approve(submitted));

        const string message = "The person who approved this scorecard cannot also give the HR final review.";
        Assert.Equal(message, ResultAssert.Failed(await host.FinalizeByHr(approved), ErrorType.Conflict).Message);
        Assert.Equal(message, ResultAssert.Failed(await host.Return(approved, "no"), ErrorType.Conflict).Message);
        Assert.Equal(message, ResultAssert.Failed(await host.Reject(approved, "no"), ErrorType.Conflict).Message);

        AsHr(null, 23);
        Assert.Equal("Finalized", ResultAssert.Succeeded(await host.FinalizeByHr(approved)).Status);
    }

    [Fact]
    public async Task Hr_can_return_or_reject_an_approved_scorecard_on_the_hr_step()
    {
        var world = await SeedAsync(hrReview: true);
        var approved = await ApprovedAsync(world);
        AsHr(world.Hr, 22);

        var returned = ResultAssert.Succeeded(await host.Return(approved, "Calibration needed"));

        Assert.Equal("Returned", returned.Status);
        var entry = Assert.Single(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.Return);
        Assert.Equal((ReviewStep.HR, ScorecardStatus.Approved, 80m), (entry.Step, entry.FromStatus, entry.OverallScore));

        var second = await ApprovedAgainAsync(world, returned);
        AsHr(world.Hr, 22);
        var rejected = ResultAssert.Succeeded(await host.Reject(second, "Final no"));
        Assert.Equal("Rejected", rejected.Status);
        Assert.Contains(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.Reject && action.Step == ReviewStep.HR);
    }

    private async Task<ScorecardDetailDto> ApprovedAgainAsync(World world, ScorecardDetailDto returned)
    {
        AsEmployee(world.Ben, 20);
        var resubmitted = ResultAssert.Succeeded(await host.Submit(host.Fresh(returned)));
        AsManager(world.Anna, 21, world.Ben.Id);
        return ResultAssert.Succeeded(await host.Approve(resubmitted));
    }

    [Fact]
    public async Task After_a_return_and_a_new_approval_by_someone_else_the_earlier_approver_may_finalize()
    {
        var world = await SeedAsync(hrReview: true);
        var submitted = await SubmittedAsync(world);
        AsHr(world.Hr, 22);
        var approved = ResultAssert.Succeeded(await host.Approve(submitted));
        AsHr(null, 23);
        var returned = ResultAssert.Succeeded(await host.Return(approved, "Again"));
        AsEmployee(world.Ben, 20);
        var resubmitted = ResultAssert.Succeeded(await host.Submit(host.Fresh(returned)));
        AsManager(world.Anna, 21, world.Ben.Id);
        var approvedAgain = ResultAssert.Succeeded(await host.Approve(resubmitted));

        AsHr(world.Hr, 22);

        Assert.Equal("Finalized", ResultAssert.Succeeded(await host.FinalizeByHr(approvedAgain)).Status);
    }

    [Fact]
    public async Task Only_an_approved_scorecard_can_be_finalized_and_the_comment_is_limited()
    {
        var world = await SeedAsync(hrReview: true);
        var submitted = await SubmittedAsync(world);
        AsHr(world.Hr, 22);

        Assert.Contains("Submitted", ResultAssert.Failed(await host.FinalizeByHr(submitted), ErrorType.Conflict).Message, StringComparison.Ordinal);
        ResultAssert.FailedOnField(await host.FinalizeByHr(submitted, new string('c', 1001)), "comment");
        ResultAssert.FailedOnField(await host.FinalizeByHr(submitted, rowVersion: ""), "rowVersion");
    }

    // ---- The period ----

    [Fact]
    public async Task Review_actions_are_still_allowed_while_the_period_is_completed()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        await MovePeriodAsync(world, close: false);
        AsManager(world.Anna, 21, world.Ben.Id);

        var started = ResultAssert.Succeeded(await host.StartReview(submitted));
        ResultAssert.Succeeded(await host.SaveReview(started.Id, ReviewOf(started, 4m, null)));
        var approved = ResultAssert.Succeeded(await host.Approve(host.Fresh(started)));

        Assert.Equal("Finalized", approved.Status);
    }

    [Fact]
    public async Task Every_write_is_refused_once_the_period_is_closed()
    {
        var world = await SeedAsync(hrReview: true);
        var submitted = await SubmittedAsync(world);
        var other = await SubmittedCardAsync(world, world.Dan, 24, "C-1");
        await MovePeriodAsync(world, close: true);
        AsHr(null, 1);

        foreach (var attempt in new Func<Task<Result<ScorecardDetailDto>>>[]
        {
            () => host.StartReview(submitted),
            () => host.SaveReview(submitted.Id, ReviewOf(submitted, 3m, null)),
            () => host.Approve(submitted),
            () => host.Return(submitted, "x"),
            () => host.Reject(submitted, "x"),
            () => host.FinalizeByHr(other),
        })
        {
            Assert.Contains("Closed", ResultAssert.Failed(await attempt(), ErrorType.Conflict).Message, StringComparison.Ordinal);
        }

        Assert.Equal(ScorecardStatus.Submitted, Stored(world.Card.Id).Status);
    }

    // ---- Scope of the reads ----

    [Fact]
    public async Task The_review_trail_is_returned_with_the_scorecard_oldest_first()
    {
        var world = await SeedAsync(hrReview: true);
        var approved = await ApprovedAsync(world);
        AsHr(world.Hr, 22);
        ResultAssert.Succeeded(await host.FinalizeByHr(approved));

        var detail = ResultAssert.Succeeded(await host.GetScorecard(world.Card.Id));

        Assert.Equal(
            ["Finalize Plan", "Submit", "Approve", "Finalize"],
            detail.History.Select(step => step.Action).ToArray());
        Assert.Equal(["HR", "Employee", "Manager", "HR"], detail.History.Select(step => step.Step).ToArray());
    }

    /// <summary>A caller in the middle of the flow: a quick check that nothing here leaves the band list used.</summary>
    [Fact]
    public async Task Inactive_bands_never_label_a_score()
    {
        var world = await SeedAsync();
        var band = await host.CreateBandAsync("Retired", 10m);
        ResultAssert.Succeeded(await host.Command<DeactivateCommand, RatingBandDto>(new DeactivateCommand(band.Id, Requests.Version(band.RowVersion))));
        var submitted = await SubmittedAsync(world);

        Assert.Null(submitted.RatingLabel);
    }

    // ---- The review queue ----

    private sealed record Queue(World World, ScorecardDetailDto Ben, ScorecardDetailDto Cara, ScorecardDetailDto Dan, ScorecardDetailDto Anna, ScorecardDetailDto Hr, EmployeeDto CaraEmployee);

    /// <summary>Submitted scorecards of Ben and Cara (reviewer Anna), Dan (no reviewer), Anna herself (no reviewer) and Hana (HR, no reviewer).</summary>
    private async Task<Queue> SeedQueueAsync(bool hrReview = false)
    {
        var world = await SeedAsync(hrReview);
        host.User.EmployeeId = null;
        host.User.UserId = 1;
        var cara = await host.CreateStaffAsync(world.Plan.Org, "EMP-0005", world.Anna.Id, "Cara", "Report");
        var ben = await SubmittedAsync(world);
        var caraCard = await SubmittedCardAsync(world, cara, 25, "Q-1");
        var dan = await SubmittedCardAsync(world, world.Dan, 24, "Q-2");
        var anna = await SubmittedCardAsync(world, world.Anna, 21, "Q-3");
        var hr = await SubmittedCardAsync(world, world.Hr, 22, "Q-4");
        return new Queue(world, ben, caraCard, dan, anna, hr, cara);
    }

    private static int[] Ids(PagedResult<ScorecardDto> page) => [.. page.Items.Select(card => card.Id).Order()];

    [Fact]
    public async Task A_manager_sees_only_the_manager_stage_scorecards_that_name_them_as_reviewer_and_never_their_own()
    {
        var queue = await SeedQueueAsync();
        AsManager(queue.World.Anna, 21, queue.World.Ben.Id, queue.CaraEmployee.Id);

        var page = ResultAssert.Succeeded(await host.PendingReviews());

        Assert.Equal(new[] { queue.Ben.Id, queue.Cara.Id }.Order().ToArray(), Ids(page));
        Assert.DoesNotContain(page.Items, card => card.EmployeeId == queue.World.Anna.Id);
    }

    [Fact]
    public async Task A_manager_without_reviewees_has_an_empty_queue_and_a_reviewed_scorecard_leaves_it()
    {
        var queue = await SeedQueueAsync();
        AsManager(queue.World.Dan, 26);
        Assert.Empty(ResultAssert.Succeeded(await host.PendingReviews()).Items);

        AsManager(queue.World.Anna, 21, queue.World.Ben.Id, queue.CaraEmployee.Id);
        ResultAssert.Succeeded(await host.Approve(queue.Ben));

        Assert.Equal([queue.Cara.Id], Ids(ResultAssert.Succeeded(await host.PendingReviews())));
    }

    [Fact]
    public async Task The_queue_is_ordered_by_the_oldest_submission_first()
    {
        var queue = await SeedQueueAsync();
        AsHr(null, 1);

        var page = ResultAssert.Succeeded(await host.PendingReviews());

        Assert.Equal(5, page.Total);
        Assert.Equal(page.Items.Select(card => card.SubmittedOn).Order().ToArray(), page.Items.Select(card => card.SubmittedOn).ToArray());
        Assert.Equal(queue.Ben.Id, page.Items[0].Id);
    }

    [Fact]
    public async Task The_hr_tier_sees_every_manager_stage_scorecard_except_its_own()
    {
        var queue = await SeedQueueAsync();
        AsHr(queue.World.Hr, 22);

        var page = ResultAssert.Succeeded(await host.PendingReviews());

        Assert.Equal(new[] { queue.Ben.Id, queue.Cara.Id, queue.Dan.Id, queue.Anna.Id }.Order().ToArray(), Ids(page));
    }

    [Fact]
    public async Task The_administrator_with_no_linked_employee_sees_every_waiting_scorecard()
    {
        var queue = await SeedQueueAsync();
        AsHr(null, 1);

        Assert.Equal(5, ResultAssert.Succeeded(await host.PendingReviews()).Total);
        Assert.Contains(queue.Hr.Id, Ids(ResultAssert.Succeeded(await host.PendingReviews())));
    }

    [Fact]
    public async Task The_hr_tier_also_sees_approved_scorecards_except_the_ones_it_approved_itself()
    {
        var queue = await SeedQueueAsync(hrReview: true);
        AsManager(queue.World.Anna, 21, queue.World.Ben.Id, queue.CaraEmployee.Id);
        var benApproved = ResultAssert.Succeeded(await host.Approve(queue.Ben));
        AsHr(null, 23);
        var caraApproved = ResultAssert.Succeeded(await host.Approve(queue.Cara));

        AsHr(queue.World.Hr, 22);
        var forHana = ResultAssert.Succeeded(await host.PendingReviews());
        AsHr(null, 23);
        var forTheApprover = ResultAssert.Succeeded(await host.PendingReviews());
        AsManager(queue.World.Anna, 21, queue.World.Ben.Id, queue.CaraEmployee.Id);
        var forAnna = ResultAssert.Succeeded(await host.PendingReviews());

        Assert.Contains(benApproved.Id, Ids(forHana));
        Assert.Contains(caraApproved.Id, Ids(forHana));
        Assert.Contains(benApproved.Id, Ids(forTheApprover));
        Assert.DoesNotContain(caraApproved.Id, Ids(forTheApprover));
        Assert.Empty(forAnna.Items);
    }

    [Fact]
    public async Task The_queue_filters_by_period_department_status_and_search()
    {
        var queue = await SeedQueueAsync();
        AsHr(null, 1);
        AsManager(queue.World.Anna, 21, queue.World.Ben.Id, queue.CaraEmployee.Id);
        ResultAssert.Succeeded(await host.StartReview(queue.Ben));
        AsHr(null, 1);

        var underReview = ResultAssert.Succeeded(await host.PendingReviews(new ScorecardFilter(Status: "Under Review")));
        var submitted = ResultAssert.Succeeded(await host.PendingReviews(new ScorecardFilter(Status: "Submitted")));
        var byPeriod = ResultAssert.Succeeded(await host.PendingReviews(new ScorecardFilter(PeriodId: queue.World.Plan.Period.Id)));
        var otherPeriod = ResultAssert.Succeeded(await host.PendingReviews(new ScorecardFilter(PeriodId: 999)));
        var byDepartment = ResultAssert.Succeeded(await host.PendingReviews(new ScorecardFilter(DepartmentId: queue.World.Plan.Org.Department.Id)));
        var byName = ResultAssert.Succeeded(await host.PendingReviews(search: "Cara"));
        var byCode = ResultAssert.Succeeded(await host.PendingReviews(search: "EMP-0003"));

        Assert.Equal([queue.Ben.Id], Ids(underReview));
        Assert.Equal(4, submitted.Total);
        Assert.Equal(5, byPeriod.Total);
        Assert.Empty(otherPeriod.Items);
        Assert.Equal(5, byDepartment.Total);
        Assert.Equal([queue.Cara.Id], Ids(byName));
        Assert.Equal([queue.Dan.Id], Ids(byCode));
        ResultAssert.FailedOnField(await host.PendingReviews(search: new string('x', 101)), "search");
    }

    [Fact]
    public async Task The_queue_is_paged_on_the_server()
    {
        await SeedQueueAsync();
        AsHr(null, 1);

        var first = ResultAssert.Succeeded(await host.PendingReviews(pageIndex: 0, pageSize: 2));
        var last = ResultAssert.Succeeded(await host.PendingReviews(pageIndex: 2, pageSize: 2));

        Assert.Equal((5, 2, 0), (first.Total, first.Items.Count, first.PageIndex));
        Assert.Equal((5, 1, 2), (last.Total, last.Items.Count, last.PageIndex));
    }
}
