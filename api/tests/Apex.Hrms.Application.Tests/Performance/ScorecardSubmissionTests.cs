using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using static Apex.Hrms.Application.Tests.Fakes.ReviewFixtures;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>The employee's part: saving results (validation, scoring, who may, which status and period) and submitting (scoring, label, history, resubmission).</summary>
public sealed class ScorecardSubmissionTests : ScorecardWorkflowTestBase
{
    private TestHost host => Host;

    // ---- Saving results ----

    [Fact]
    public async Task The_employee_saves_results_and_each_gets_its_achievement_and_weighted_score_but_the_scorecard_gets_no_score()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);

        var saved = ResultAssert.Succeeded(await host.SaveResults(
            world.Card.Id,
            ResultsOf(world.Card, " Busy quarter ", Actual(Of(world.Card, "A-1"), 100m, " Hit it "), Actual(Of(world.Card, "A-2"), 50m))));

        var first = Of(saved, "A-1");
        var second = Of(saved, "A-2");
        Assert.Equal((100m, "Hit it", 100m, 60m), (first.ActualValue, first.EmployeeComment, first.AchievementPct, first.WeightedScore));
        Assert.Equal((50m, null, 50m, 20m), (second.ActualValue, second.EmployeeComment, second.AchievementPct, second.WeightedScore));
        Assert.Equal(("Draft", "Busy quarter", null), (saved.Status, saved.EmployeeComment, saved.OverallScore));
        Assert.Null(saved.RatingLabel);
        Assert.NotEqual(world.Card.RowVersion, saved.RowVersion);
        Assert.DoesNotContain(HistoryOf(world.Card.Id), action => action.Action != ReviewActionType.FinalizePlan);
    }

    [Fact]
    public async Task A_result_of_null_clears_the_result_and_its_scores_and_the_other_items_stay()
    {
        var world = await SeedAsync();
        var filled = await FilledAsync(world);

        var cleared = ResultAssert.Succeeded(await host.SaveResults(filled, Actual(Of(filled, "A-1"), null)));

        Assert.Equal((null, null, null), (Of(cleared, "A-1").ActualValue, Of(cleared, "A-1").AchievementPct, Of(cleared, "A-1").WeightedScore));
        Assert.Equal(50m, Of(cleared, "A-2").ActualValue);
    }

    [Fact]
    public async Task Saving_results_moves_the_scorecard_row_version_even_when_only_an_item_changed()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var first = ResultAssert.Succeeded(await host.SaveResults(world.Card, Actual(Of(world.Card, "A-1"), 10m)));

        var stale = await host.SaveResults(world.Card.Id, ResultsOf(world.Card, null, Actual(Of(world.Card, "A-1"), 20m)));

        Assert.NotEqual(world.Card.RowVersion, first.RowVersion);
        ResultAssert.Failed(stale, ErrorType.Conflict);
        Assert.Equal(10m, host.Repository<ScorecardItem>().Items.Single(item => item.KpiCode == "A-1").ActualValue);
    }

    [Fact]
    public async Task The_shape_of_the_results_is_validated()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var item = Of(world.Card, "A-1");
        var valid = ResultsOf(world.Card, null, Actual(item, 1m));

        ResultAssert.FailedOnField(await host.SaveResults(world.Card.Id, valid with { RowVersion = null }), "rowVersion");
        ResultAssert.FailedOnField(await host.SaveResults(world.Card.Id, valid with { Items = null }), "items");
        ResultAssert.FailedOnField(await host.SaveResults(world.Card.Id, valid with { EmployeeComment = new string('c', 1001) }), "employeeComment");
        ResultAssert.FailedOnField(await host.SaveResults(world.Card.Id, valid with { Items = [Actual(item, 1.23456m)] }), "items[0].actualValue");
        ResultAssert.FailedOnField(await host.SaveResults(world.Card.Id, valid with { Items = [Actual(item, 1m, new string('c', 1001))] }), "items[0].employeeComment");
        ResultAssert.FailedOnField(await host.SaveResults(world.Card.Id, valid with { Items = [new ResultItemRequest(0, 1m, null)] }), "items[0].id");
        Assert.True((await host.SaveResults(world.Card.Id, valid with { Items = [Actual(item, 1.2345m, new string('c', 1000))] })).IsSuccess);
    }

    [Fact]
    public async Task An_item_that_is_not_on_the_scorecard_or_appears_twice_is_a_400_on_its_id()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var item = Of(world.Card, "A-1");

        var foreign = await host.SaveResults(world.Card.Id, ResultsOf(world.Card, null, new ResultItemRequest(9999, 1m, null)));
        var twice = await host.SaveResults(world.Card.Id, ResultsOf(world.Card, null, Actual(item, 1m), Actual(item, 2m)));

        ResultAssert.FailedOnField(foreign, "items[0].id");
        ResultAssert.FailedOnField(twice, "items[1].id");
        Assert.Null(host.Repository<ScorecardItem>().Items.First(row => row.Id == item.Id).ActualValue);
    }

    [Fact]
    public async Task An_item_of_another_scorecard_is_a_400_and_never_changes_that_scorecard()
    {
        var world = await SeedAsync();
        var template = (await host.CreateTemplateAsync(world.Plan.Category.Id, "B-1")).Id;
        var danCard = ResultAssert.Succeeded(await host.FinalizePlan(await host.AssignedAsync(world.Dan.Id, world.Plan.Period.Id, Item(template, weight: 100m))));
        AsEmployee(world.Ben, 20);

        var result = await host.SaveResults(world.Card.Id, ResultsOf(world.Card, null, Actual(danCard.Items[0], 5m)));

        ResultAssert.FailedOnField(result, "items[0].id");
        Assert.Null(host.Repository<ScorecardItem>().Items.Single(item => item.Id == danCard.Items[0].Id).ActualValue);
    }

    [Fact]
    public async Task Results_that_break_the_scoring_rule_are_refused_on_their_item_and_nothing_is_saved()
    {
        var org = await host.SeedOrgAsync();
        var category = await host.CreateCategoryAsync();
        var period = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(await host.CreatePeriodAsync(org.Company.Id)));
        var ben = await host.CreateStaffAsync(org, "EMP-0002");
        var boolean = ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(
            KpiFixtures.NewTemplate(category.Id, "BOOL", "Training done", measurement: "Boolean", scoring: "Boolean", target: 1m, weight: 25m)));
        var rating = ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(
            KpiFixtures.NewTemplate(category.Id, "RATE", "Peer rating", measurement: "Rating", scoring: "Rating Based", target: 5m, maxRating: 5m, weight: 25m)));
        var lower = ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(
            KpiFixtures.NewTemplate(category.Id, "COST", "Cost", direction: "Lower Is Better", scoring: "Capped Percentage", target: 10m, cap: 120m, weight: 25m)));
        var plain = (await host.CreateTemplateAsync(category.Id, "PLAIN")).Id;
        var card = ResultAssert.Succeeded(await host.FinalizePlan(await host.AssignedAsync(
            ben.Id, period.Id, Item(boolean.Id), Item(rating.Id), Item(lower.Id), Item(plain, weight: 25m))));
        AsEmployee(ben, 20);

        var result = await host.SaveResults(
            card.Id,
            ResultsOf(card, null, Actual(Of(card, "BOOL"), 0.5m), Actual(Of(card, "RATE"), 5.5m), Actual(Of(card, "COST"), -1m), Actual(Of(card, "PLAIN"), 80m)));

        var error = ResultAssert.Failed(result, ErrorType.Validation);
        Assert.Equal(["items[0].actualValue", "items[1].actualValue", "items[2].actualValue"], error.FieldErrors!.Keys.Order().ToArray());
        Assert.All(host.Repository<ScorecardItem>().Items, item => Assert.Null(item.ActualValue));
    }

    [Fact]
    public async Task Valid_boolean_rating_and_lower_is_better_results_are_scored()
    {
        var org = await host.SeedOrgAsync();
        var category = await host.CreateCategoryAsync();
        var period = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(await host.CreatePeriodAsync(org.Company.Id)));
        var ben = await host.CreateStaffAsync(org, "EMP-0002");
        var boolean = ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(
            KpiFixtures.NewTemplate(category.Id, "BOOL", "Training done", measurement: "Boolean", scoring: "Boolean", target: 1m, weight: 50m)));
        var rating = ResultAssert.Succeeded(await host.Create<KpiTemplateCreateRequest, KpiTemplateDto>(
            KpiFixtures.NewTemplate(category.Id, "RATE", "Peer rating", measurement: "Rating", scoring: "Rating Based", target: 5m, maxRating: 5m, weight: 50m)));
        var card = ResultAssert.Succeeded(await host.FinalizePlan(await host.AssignedAsync(ben.Id, period.Id, Item(boolean.Id), Item(rating.Id))));
        AsEmployee(ben, 20);

        var saved = ResultAssert.Succeeded(await host.SaveResults(card.Id, ResultsOf(card, null, Actual(Of(card, "BOOL"), 1m), Actual(Of(card, "RATE"), 4m))));

        Assert.Equal((100m, 50m), (Of(saved, "BOOL").AchievementPct, Of(saved, "BOOL").WeightedScore));
        Assert.Equal((80m, 40m), (Of(saved, "RATE").AchievementPct, Of(saved, "RATE").WeightedScore));
    }

    [Fact]
    public async Task Only_the_scorecards_own_employee_may_enter_results_everyone_else_is_a_403_or_a_404()
    {
        var world = await SeedAsync();
        var request = ResultsOf(world.Card, null, Actual(Of(world.Card, "A-1"), 1m));
        const string message = "You can only enter results on your own scorecard.";

        AsManager(world.Anna, 21, world.Ben.Id);
        Assert.Equal(message, ResultAssert.Failed(await host.SaveResults(world.Card.Id, request), ErrorType.Forbidden).Message);
        AsHr(world.Hr, 22);
        Assert.Equal(message, ResultAssert.Failed(await host.SaveResults(world.Card.Id, request), ErrorType.Forbidden).Message);
        AsHr(null, 23);
        Assert.Equal(message, ResultAssert.Failed(await host.SaveResults(world.Card.Id, request), ErrorType.Forbidden).Message);
        AsEmployee(world.Dan, 24);
        ResultAssert.Failed(await host.SaveResults(world.Card.Id, request), ErrorType.NotFound);
        Assert.Null(host.Repository<ScorecardItem>().Items[0].ActualValue);
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("Under Review")]
    [InlineData("Approved")]
    [InlineData("Finalized")]
    [InlineData("Rejected")]
    public async Task Results_cannot_be_saved_once_the_scorecard_left_draft_and_returned(string status)
    {
        var world = await SeedAsync(hrReview: true);
        var submitted = await SubmittedAsync(world);
        var card = await MoveToAsync(world, submitted, status);
        AsEmployee(world.Ben, 20);

        var error = ResultAssert.Failed(
            await host.SaveResults(card.Id, ResultsOf(card, null, Actual(Of(card, "A-1"), 1m))),
            ErrorType.Conflict);

        Assert.Contains(status, error.Message, StringComparison.Ordinal);
    }

    private async Task<ScorecardDetailDto> MoveToAsync(World world, ScorecardDetailDto submitted, string status)
    {
        AsManager(world.Anna, 21, world.Ben.Id);
        switch (status)
        {
            case "Under Review":
                return ResultAssert.Succeeded(await host.StartReview(submitted));
            case "Approved":
                return ResultAssert.Succeeded(await host.Approve(submitted));
            case "Rejected":
                return ResultAssert.Succeeded(await host.Reject(submitted, "No"));
            case "Finalized":
                var approved = ResultAssert.Succeeded(await host.Approve(submitted));
                AsHr(world.Hr, 22);
                return ResultAssert.Succeeded(await host.FinalizeByHr(approved));
            default:
                return submitted;
        }
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Closed")]
    public async Task Results_and_submission_need_an_active_period(string periodStatus)
    {
        var world = await SeedAsync();
        var filled = await FilledAsync(world);
        await SetPeriodAsync(world, periodStatus);

        await AssertNeedsActivePeriodAsync(filled, periodStatus);
    }

    [Fact]
    public async Task Results_and_submission_are_refused_in_a_draft_period_even_though_the_plan_could_be_finalized_there()
    {
        var world = await SeedAsync(activate: false);
        AsEmployee(world.Ben, 20);

        await AssertNeedsActivePeriodAsync(world.Card, "Draft");
    }

    private async Task AssertNeedsActivePeriodAsync(ScorecardDetailDto card, string periodStatus)
    {
        var results = await host.SaveResults(card.Id, ResultsOf(card, null, Actual(Of(card, "A-1"), 90m)));
        var submit = await host.Submit(card);

        Assert.Contains(periodStatus, ResultAssert.Failed(results, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains(periodStatus, ResultAssert.Failed(submit, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Equal(ScorecardStatus.Draft, Stored(card.Id).Status);
    }

    /// <summary>Moves the seeded period to Completed or Closed as an administrator, then goes back to Ben.</summary>
    private async Task SetPeriodAsync(World world, string status)
    {
        host.User.EmployeeId = null;
        host.User.UserId = 1;
        host.Permissions.Only("kpi:View", "kpi:Create", "kpi:Approve", "employees:Edit", "kpi-setup:Edit");
        var current = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<EvaluationPeriodDto>>().HandleAsync(world.Plan.Period.Id, default));
        var completed = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(current));
        if (status == "Closed")
        {
            ResultAssert.Succeeded(await host.MoveAsync<ClosePeriodCommand>(completed));
        }

        AsEmployee(world.Ben, 20);
    }

    [Fact]
    public async Task Results_can_be_saved_again_on_a_returned_scorecard()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var returned = ResultAssert.Succeeded(await host.Return(submitted, "Check A-2"));
        AsEmployee(world.Ben, 20);

        var saved = ResultAssert.Succeeded(await host.SaveResults(returned, Actual(Of(returned, "A-2"), 90m)));

        Assert.Equal(("Returned", 90m, 90m, 36m), (saved.Status, Of(saved, "A-2").ActualValue, Of(saved, "A-2").AchievementPct, Of(saved, "A-2").WeightedScore));
    }

    [Fact]
    public async Task Saving_results_records_an_activity_line_with_codes_only()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);

        ResultAssert.Succeeded(await host.SaveResults(world.Card.Id, ResultsOf(world.Card, "private words", Actual(Of(world.Card, "A-1"), 77m, "secret note"))));

        Assert.Equal("Results saved on the scorecard of EMP-0002 for \"2026 Q4\"", host.ActivityLog.LastMessage);
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("secret", StringComparison.Ordinal) || message.Contains("77", StringComparison.Ordinal));
    }

    // ---- Submit ----

    [Fact]
    public async Task Submitting_scores_the_scorecard_from_the_results_labels_it_and_writes_the_employee_step()
    {
        var world = await SeedAsync();
        await host.CreateBandAsync("Meets Expectations", 70m);
        await host.CreateBandAsync("Exceeds", 90m);
        var inactive = await host.CreateBandAsync("Top", 75m);
        ResultAssert.Succeeded(await host.Command<DeactivateCommand, RatingBandDto>(new DeactivateCommand(inactive.Id, Requests.Version(inactive.RowVersion))));
        var filled = await FilledAsync(world, first: 100m, second: 50m);

        var submitted = ResultAssert.Succeeded(await host.Submit(filled));

        Assert.Equal(("Submitted", 80m, "Meets Expectations", new DateOnly(2026, 11, 9)), (submitted.Status, submitted.OverallScore, submitted.RatingLabel, submitted.SubmittedOn));
        Assert.Equal((100m, 60m, 50m, 20m), (Of(submitted, "A-1").AchievementPct, Of(submitted, "A-1").WeightedScore, Of(submitted, "A-2").AchievementPct, Of(submitted, "A-2").WeightedScore));
        Assert.Equal(KpiCalculator.CalculationVersion, Stored(world.Card.Id).CalculationVersion);
        var entry = Assert.Single(HistoryOf(world.Card.Id), action => action.Action == ReviewActionType.Submit);
        Assert.Equal((ReviewStep.Employee, ScorecardStatus.Draft, ScorecardStatus.Submitted, 20, world.Ben.Id, "Ben Report", 80m), (entry.Step, entry.FromStatus, entry.ToStatus, entry.ActorUserId, entry.ActorEmployeeId, entry.ActorName, entry.OverallScore));
        Assert.Equal("Scorecard of EMP-0002 for \"2026 Q4\" submitted", host.ActivityLog.LastMessage);
        Assert.Contains(submitted.History, step => step.Action == "Submit" && step.Step == "Employee");
    }

    [Fact]
    public async Task Without_any_band_the_label_is_empty_and_the_score_is_still_stored()
    {
        var world = await SeedAsync();
        var filled = await FilledAsync(world);

        var submitted = ResultAssert.Succeeded(await host.Submit(filled));

        Assert.Equal((80m, null), (submitted.OverallScore, submitted.RatingLabel));
    }

    [Fact]
    public async Task Submitting_needs_every_result_and_lists_the_missing_items()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var none = await host.Submit(world.Card);
        Assert.Equal(["items[0].actualValue", "items[1].actualValue"], ResultAssert.Failed(none, ErrorType.Validation).FieldErrors!.Keys.Order().ToArray());

        var partial = ResultAssert.Succeeded(await host.SaveResults(world.Card, Actual(Of(world.Card, "A-2"), 50m)));
        var result = await host.Submit(partial);

        var error = ResultAssert.Failed(result, ErrorType.Validation);
        Assert.Equal(["items[0].actualValue"], error.FieldErrors!.Keys.ToArray());
        Assert.Equal(ScorecardStatus.Draft, Stored(world.Card.Id).Status);
    }

    [Fact]
    public async Task A_submission_after_the_due_date_is_allowed_and_dated_the_day_it_happened()
    {
        var world = await SeedAsync();
        var filled = await FilledAsync(world);
        host.Clock.Today = new DateOnly(2027, 2, 1);

        var submitted = ResultAssert.Succeeded(await host.Submit(filled));

        Assert.Equal(new DateOnly(2027, 2, 1), submitted.SubmittedOn);
        Assert.True(submitted.SubmissionDueDate < submitted.SubmittedOn);
    }

    [Fact]
    public async Task Only_the_own_employee_may_submit_and_a_submitted_scorecard_cannot_be_submitted_again()
    {
        var world = await SeedAsync();
        var filled = await FilledAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        Assert.Equal("You can only submit your own scorecard.", ResultAssert.Failed(await host.Submit(filled), ErrorType.Forbidden).Message);
        AsEmployee(world.Dan, 24);
        ResultAssert.Failed(await host.Submit(filled), ErrorType.NotFound);

        AsEmployee(world.Ben, 20);
        var submitted = ResultAssert.Succeeded(await host.Submit(filled));
        var again = ResultAssert.Failed(await host.Submit(submitted), ErrorType.Conflict);

        Assert.Contains("Submitted", again.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scorecard_that_is_still_in_planning_cannot_be_submitted_or_saved()
    {
        var org = await host.SeedOrgAsync();
        var category = await host.CreateCategoryAsync();
        var period = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(await host.CreatePeriodAsync(org.Company.Id)));
        var ben = await host.CreateStaffAsync(org, "EMP-0002");
        var template = (await host.CreateTemplateAsync(category.Id, "A-1")).Id;
        var planning = await host.AssignedAsync(ben.Id, period.Id, Item(template, weight: 100m));
        AsEmployee(ben, 20);

        var submit = ResultAssert.Failed(await host.Submit(planning), ErrorType.Conflict);
        var save = ResultAssert.Failed(await host.SaveResults(planning, Actual(planning.Items[0], 5m)), ErrorType.Conflict);

        Assert.Contains("Planning", submit.Message, StringComparison.Ordinal);
        Assert.Contains("Planning", save.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stale_or_missing_row_version_is_refused_on_submit()
    {
        var world = await SeedAsync();
        var filled = await FilledAsync(world);

        ResultAssert.FailedOnField(await host.Submit(filled, rowVersion: ""), "rowVersion");
        ResultAssert.Failed(await host.Submit(filled, rowVersion: Requests.Version(world.Card.RowVersion)), ErrorType.Conflict);
        Assert.True((await host.Submit(filled)).IsSuccess);
    }

    [Fact]
    public async Task A_returned_scorecard_is_resubmitted_with_the_new_figures_and_the_history_keeps_every_step()
    {
        var world = await SeedAsync();
        await host.CreateBandAsync("Meets Expectations", 70m);
        var submitted = await SubmittedAsync(world);
        AsManager(world.Anna, 21, world.Ben.Id);
        var returned = ResultAssert.Succeeded(await host.Return(submitted, "A-2 looks low"));
        Assert.Equal((null, null), (returned.OverallScore, returned.RatingLabel));
        AsEmployee(world.Ben, 20);
        var edited = ResultAssert.Succeeded(await host.SaveResults(returned, Actual(Of(returned, "A-2"), 100m)));
        host.Clock.Today = new DateOnly(2026, 11, 12);

        var again = ResultAssert.Succeeded(await host.Submit(edited));

        Assert.Equal(("Resubmitted", 100m, "Meets Expectations", new DateOnly(2026, 11, 12)), (again.Status, again.OverallScore, again.RatingLabel, again.SubmittedOn));
        Assert.Equal(
            [ReviewActionType.FinalizePlan, ReviewActionType.Submit, ReviewActionType.Return, ReviewActionType.Resubmit],
            HistoryOf(world.Card.Id).Select(action => action.Action).ToArray());
        Assert.Equal("Scorecard of EMP-0002 for \"2026 Q4\" resubmitted", host.ActivityLog.LastMessage);
    }

    // ---- My scorecards ----

    [Fact]
    public async Task My_scorecards_lists_only_the_callers_scorecards_without_planning_newest_period_first()
    {
        var world = await SeedAsync();
        var org = world.Plan.Org;
        var earlier = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(
            await host.CreatePeriodAsync(org.Company.Id, "2026 Q3", start: "2026-07-01", end: "2026-09-30", due: null)));
        var later = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(
            await host.CreatePeriodAsync(org.Company.Id, "2027 Q1", start: "2027-01-01", end: "2027-03-31", due: null)));
        var template = (await host.CreateTemplateAsync(world.Plan.Category.Id, "B-1")).Id;
        ResultAssert.Succeeded(await host.FinalizePlan(await host.AssignedAsync(world.Ben.Id, earlier.Id, Item(template, weight: 100m))));
        await host.AssignedAsync(world.Ben.Id, later.Id, Item(template, weight: 100m));
        ResultAssert.Succeeded(await host.FinalizePlan(await host.AssignedAsync(world.Dan.Id, world.Plan.Period.Id, Item(template, weight: 100m))));
        AsEmployee(world.Ben, 20);

        var page = ResultAssert.Succeeded(await host.MyScorecards());

        Assert.Equal(["2026 Q4", "2026 Q3"], page.Items.Select(card => card.PeriodName).ToArray());
        Assert.All(page.Items, card => Assert.Equal(world.Ben.Id, card.EmployeeId));
        Assert.Equal(2, page.Total);
    }

    [Fact]
    public async Task My_scorecards_filters_pages_and_refuses_unknown_sorts()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);

        var draft = ResultAssert.Succeeded(await host.MyScorecards(new ScorecardFilter(Status: "Draft")));
        var submitted = ResultAssert.Succeeded(await host.MyScorecards(new ScorecardFilter(Status: "Submitted")));
        var sorted = ResultAssert.Succeeded(await host.MyScorecards(sort: "periodName", direction: "desc"));

        Assert.Single(draft.Items);
        Assert.Empty(submitted.Items);
        Assert.Single(sorted.Items);
        ResultAssert.FailedOnField(await host.MyScorecards(sort: "nonsense"), "sort");
    }

    [Fact]
    public async Task A_caller_with_no_linked_employee_has_no_scorecards_of_their_own_and_gets_an_empty_page()
    {
        await SeedAsync();
        host.User.EmployeeId = null;

        var page = ResultAssert.Succeeded(await host.MyScorecards(pageSize: 20));

        Assert.Empty(page.Items);
        Assert.Equal((0, 0, 20), (page.Total, page.PageIndex, page.PageSize));
    }

    [Fact]
    public async Task The_detail_carries_the_comments_the_period_state_and_the_hr_review_flag()
    {
        var world = await SeedAsync(hrReview: true);
        AsEmployee(world.Ben, 20);
        var saved = ResultAssert.Succeeded(await host.SaveResults(world.Card.Id, ResultsOf(world.Card, "Hello", Actual(Of(world.Card, "A-1"), 10m))));

        Assert.Equal(("Hello", "Active", true, new DateOnly(2027, 1, 10)), (saved.EmployeeComment, saved.PeriodStatus, saved.RequiresHrReview, saved.SubmissionDueDate));
        Assert.Null(saved.ManagerComment);
        Assert.Null(saved.HrComment);
    }
}
