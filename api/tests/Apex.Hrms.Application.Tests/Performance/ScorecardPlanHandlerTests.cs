using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>Changing the plan (update and remove a KPI, delete a scorecard) and finalizing it: Planning-only edits, the weight cap, the 100% rule, the review history and the reviewer.</summary>
public sealed class ScorecardPlanHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private async Task<int> TemplateAsync(PlanSeed plan, string code) => (await host.CreateTemplateAsync(plan.Category.Id, code)).Id;

    /// <summary>A scorecard with two KPIs of 60 and 40 percent.</summary>
    private async Task<(PlanSeed Plan, Apex.Hrms.Application.Employees.EmployeeDto Employee, ScorecardDetailDto Scorecard)> PlannedAsync(int? managerId = null)
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001", managerId);
        var first = await TemplateAsync(plan, "A-1");
        var second = await TemplateAsync(plan, "A-2");
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(first, weight: 60m), Item(second, weight: 40m));
        return (plan, employee, scorecard);
    }

    // ---- Updating a KPI ----

    [Fact]
    public async Task A_planned_kpi_changes_its_target_weight_dates_and_order_and_keeps_its_snapshot()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];

        var updated = ResultAssert.Succeeded(await host.UpdateItem(
            item.Id,
            ItemUpdateOf(item, target: 300m, weight: 50m, start: new DateOnly(2026, 11, 1), end: new DateOnly(2026, 11, 30), sortOrder: 9)));

        Assert.Equal((300m, 50m, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), 9), (updated.Target, updated.Weight, updated.StartDate, updated.EndDate, updated.SortOrder));
        Assert.Equal(("A-1", "Productivity"), (updated.KpiCode, updated.CategoryName));
        Assert.NotEqual(item.RowVersion, updated.RowVersion);
        Assert.Equal(90m, ResultAssert.Succeeded(await host.GetScorecard(scorecard.Id)).TotalWeight);
    }

    [Fact]
    public async Task Leaving_out_the_sort_order_keeps_it()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[1];

        var updated = ResultAssert.Succeeded(await host.UpdateItem(item.Id, ItemUpdateOf(item) with { SortOrder = null }));

        Assert.Equal(item.SortOrder, updated.SortOrder);
    }

    [Fact]
    public async Task Updating_a_kpi_is_refused_when_the_weights_would_exceed_100_when_the_rule_breaks_or_the_dates_leave_the_period()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];

        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 60.01m)), "weight");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, ItemUpdateOf(item, target: 0m)), "target");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, ItemUpdateOf(item, start: new DateOnly(2026, 9, 1))), "startDate");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, ItemUpdateOf(item, end: new DateOnly(2027, 1, 1))), "endDate");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, ItemUpdateOf(item, start: new DateOnly(2026, 11, 5), end: new DateOnly(2026, 11, 4))), "endDate");
        Assert.True((await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 60m))).IsSuccess);
    }

    [Fact]
    public async Task The_shape_of_an_item_update_is_validated()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];
        var valid = ItemUpdateOf(item);

        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { Target = null }), "target");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { Target = -1m }), "target");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { Weight = null }), "weight");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { Weight = 0m }), "weight");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { Weight = 5.123m }), "weight");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { StartDate = null }), "startDate");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { EndDate = null }), "endDate");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { SortOrder = 40000 }), "sortOrder");
        ResultAssert.FailedOnField(await host.UpdateItem(item.Id, valid with { RowVersion = null }), "rowVersion");
    }

    [Fact]
    public async Task An_item_update_needs_the_current_row_version_and_an_existing_item()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];
        var first = ResultAssert.Succeeded(await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 55m)));

        var stale = await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 50m));
        var missing = await host.UpdateItem(9999, ItemUpdateOf(first));

        ResultAssert.Failed(stale, ErrorType.Conflict);
        ResultAssert.Failed(missing, ErrorType.NotFound);
        Assert.Equal(55m, ResultAssert.Succeeded(await host.GetScorecard(scorecard.Id)).Items[0].Weight);
    }

    [Fact]
    public async Task A_kpi_cannot_be_changed_once_the_plan_is_final()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];
        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        var error = ResultAssert.Failed(await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 50m)), ErrorType.Conflict);

        Assert.Contains("Draft", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_kpi_cannot_be_changed_in_a_completed_period_but_can_still_be_removed()
    {
        var (plan, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];
        ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(plan.Period));

        var error = ResultAssert.Failed(await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 50m)), ErrorType.Conflict);

        Assert.Contains("Completed", error.Message, StringComparison.Ordinal);
        Assert.True((await host.DeleteItem(item)).IsSuccess);
    }

    [Fact]
    public async Task Updating_a_kpi_records_an_activity_entry_with_codes_only()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];

        ResultAssert.Succeeded(await host.UpdateItem(item.Id, ItemUpdateOf(item, weight: 55m)));

        Assert.Equal("KPI \"A-1\" of EMP-0001 for \"2026 Q4\" updated", host.ActivityLog.LastMessage);
        Assert.Equal(ActivityCategories.Performance, host.ActivityLog.Entries[^1].Category);
    }

    // ---- Removing a KPI ----

    [Fact]
    public async Task A_planned_kpi_can_be_removed_with_its_row_version()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var item = scorecard.Items[0];

        ResultAssert.Failed(await host.DeleteItem(item, rowVersion: Requests.Version([1, 2, 3, 4, 5, 6, 7, 8])), ErrorType.Conflict);
        ResultAssert.FailedOnField(await host.DeleteItem(item, rowVersion: "not base64!"), "rowVersion");
        Assert.True((await host.DeleteItem(item)).IsSuccess);

        var after = ResultAssert.Succeeded(await host.GetScorecard(scorecard.Id));
        Assert.Equal(["A-2"], after.Items.Select(other => other.KpiCode));
        Assert.Equal("KPI \"A-1\" removed from EMP-0001 for \"2026 Q4\"", host.ActivityLog.LastMessage);
        ResultAssert.Failed(await host.DeleteItem(item), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_kpi_cannot_be_removed_after_the_plan_is_final()
    {
        var (_, _, scorecard) = await PlannedAsync();
        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        ResultAssert.Failed(await host.DeleteItem(scorecard.Items[0]), ErrorType.Conflict);
    }

    // ---- Deleting a scorecard ----

    [Fact]
    public async Task A_scorecard_in_planning_is_deleted_with_its_kpis()
    {
        var (_, _, scorecard) = await PlannedAsync();

        Assert.True((await host.DeleteScorecard(scorecard)).IsSuccess);

        ResultAssert.Failed(await host.GetScorecard(scorecard.Id), ErrorType.NotFound);
        Assert.Empty(host.Repository<ScorecardItem>().Items);
        Assert.Equal("Scorecard of EMP-0001 for \"2026 Q4\" deleted", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task A_scorecard_cannot_be_deleted_with_a_stale_version_after_planning_or_with_a_review_history()
    {
        var (plan, _, scorecard) = await PlannedAsync();
        var withHistory = await PlannedWithHistoryAsync(plan);

        ResultAssert.Failed(await host.DeleteScorecard(scorecard, rowVersion: Requests.Version([9, 9, 9, 9, 9, 9, 9, 9])), ErrorType.Conflict);
        ResultAssert.Failed(await host.DeleteScorecard(withHistory), ErrorType.Conflict);
        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));
        var final = ResultAssert.Succeeded(await host.GetScorecard(scorecard.Id));
        var error = ResultAssert.Failed(await host.DeleteScorecard(final), ErrorType.Conflict);

        Assert.Contains("Draft", error.Message, StringComparison.Ordinal);
    }

    /// <summary>A scorecard still in Planning that has a history entry (impossible through the API, so stored directly) to prove the history protects it.</summary>
    private async Task<ScorecardDetailDto> PlannedWithHistoryAsync(PlanSeed plan)
    {
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0060", first: "Hist", last: "Ory");
        var template = await TemplateAsync(plan, "H-1");
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template, weight: 100m));
        host.Store(ReviewAction.Create(scorecard.Id, ReviewStep.HR, ReviewActionType.FinalizePlan, ScorecardStatus.Planning, ScorecardStatus.Draft, 1, null, "Test", null, null, new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc)).Value);
        return scorecard;
    }

    // ---- Finalizing the plan ----

    [Fact]
    public async Task Finalizing_moves_to_draft_sets_the_reviewer_to_the_current_manager_and_writes_the_history()
    {
        var plan = await host.SeedPlanAsync();
        var manager = await host.CreateStaffAsync(plan.Org, "EMP-0010", first: "Hana", last: "Boss");
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001", manager.Id);
        var template = await TemplateAsync(plan, "A-1");
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template, weight: 100m));
        host.User.EmployeeId = 77;
        host.User.DisplayName = "Hr Tester";

        var final = ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        Assert.Equal("Draft", final.Status);
        Assert.Equal((manager.Id, "Hana Boss"), (final.ReviewerEmployeeId, final.ReviewerName));
        Assert.Equal(new DateOnly(2026, 11, 9), final.PlanFinalizedOn);
        Assert.NotEqual(scorecard.RowVersion, final.RowVersion);
        var entry = Assert.Single(final.History);
        Assert.Equal(("HR", "Finalize Plan", "Planning", "Draft", "Hr Tester"), (entry.Step, entry.Action, entry.FromStatus, entry.ToStatus, entry.ActorName));
        Assert.Null(entry.Comment);
        Assert.Null(entry.OverallScore);
        var stored = Assert.Single(host.Repository<ReviewAction>().Items);
        Assert.Equal((1, 77), (stored.ActorUserId, stored.ActorEmployeeId));
        Assert.Equal(scorecard.Id, stored.ScorecardId);
    }

    [Fact]
    public async Task An_employee_without_a_manager_is_finalized_with_no_reviewer()
    {
        var (_, _, scorecard) = await PlannedAsync();

        var final = ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        Assert.Equal("Draft", final.Status);
        Assert.Null(final.ReviewerEmployeeId);
        Assert.Null(final.ReviewerName);
    }

    [Fact]
    public async Task The_reviewer_is_the_manager_at_the_time_of_finalizing_not_at_assignment()
    {
        var plan = await host.SeedPlanAsync();
        var first = await host.CreateStaffAsync(plan.Org, "EMP-0010", first: "Old", last: "Boss");
        var second = await host.CreateStaffAsync(plan.Org, "EMP-0011", first: "New", last: "Boss");
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001", first.Id);
        var template = await TemplateAsync(plan, "A-1");
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template, weight: 100m));
        var current = ResultAssert.Succeeded(await host.GetEmployee(employee.Id));
        ResultAssert.Succeeded(await host.UpdateEmployee(employee.Id, Apex.Hrms.Application.Tests.Fakes.EmployeeFixtures.UpdateOf(current) with { ManagerId = second.Id }));

        var final = ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        Assert.Equal(second.Id, final.ReviewerEmployeeId);
    }

    [Fact]
    public async Task Weights_must_add_up_to_exactly_100_to_finalize()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var other = await host.CreateStaffAsync(plan.Org, "EMP-0002", first: "Zoe", last: "Two");
        var third = await host.CreateStaffAsync(plan.Org, "EMP-0003", first: "Yan", last: "Three");
        var template = await TemplateAsync(plan, "A-1");

        var under = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template, weight: 99.99m));
        var exact = await host.AssignedAsync(other.Id, plan.Period.Id, Item(template, weight: 100m));
        var over = await host.AssignedAsync(third.Id, plan.Period.Id, Item(template, weight: 100m));
        host.Store(ScorecardItem.Create(ScorecardItemDetails.FromTemplate(
            await NewTemplateEntityAsync(plan, "X-1"), over.Id, null, 0.01m, new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), 5, null, new DateOnly(2026, 11, 9))).Value);
        var overFresh = ResultAssert.Succeeded(await host.GetScorecard(over.Id));

        var shortError = ResultAssert.FailedOnField(await host.FinalizePlan(under), "weights");
        Assert.True((await host.FinalizePlan(exact)).IsSuccess);
        var longError = ResultAssert.FailedOnField(await host.FinalizePlan(overFresh), "weights");

        Assert.Equal("The KPI weights must add up to 100%. They add up to 99.99%.", shortError.Message);
        Assert.Equal("The KPI weights must add up to 100%. They add up to 100.01%.", longError.Message);
        Assert.Equal("Planning", ResultAssert.Succeeded(await host.GetScorecard(under.Id)).Status);
    }

    private async Task<KpiTemplate> NewTemplateEntityAsync(PlanSeed plan, string code)
    {
        await host.CreateTemplateAsync(plan.Category.Id, code);
        return host.Repository<KpiTemplate>().Items.Single(template => template.Code == code);
    }

    [Fact]
    public async Task A_plan_without_kpis_and_a_75_percent_plan_are_not_finalized()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var other = await host.CreateStaffAsync(plan.Org, "EMP-0002", first: "Zoe", last: "Two");
        var template = await TemplateAsync(plan, "A-1");
        var seventyFive = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template, weight: 75m));
        var emptied = await host.AssignedAsync(other.Id, plan.Period.Id, Item(template, weight: 100m));
        Assert.True((await host.DeleteItem(emptied.Items[0])).IsSuccess);
        var empty = ResultAssert.Succeeded(await host.GetScorecard(emptied.Id));

        var short75 = ResultAssert.FailedOnField(await host.FinalizePlan(seventyFive), "weights");
        var none = ResultAssert.FailedOnField(await host.FinalizePlan(empty), "weights");

        Assert.Equal("The KPI weights must add up to 100%. They add up to 75.00%.", short75.Message);
        Assert.Contains("at least one KPI", none.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_plan_is_finalized_once_and_needs_the_current_row_version()
    {
        var (_, _, scorecard) = await PlannedAsync();

        ResultAssert.FailedOnField(await host.FinalizePlan(scorecard, rowVersion: ""), "rowVersion");
        ResultAssert.Failed(await host.FinalizePlan(scorecard, rowVersion: Requests.Version([1, 1, 1, 1, 1, 1, 1, 1])), ErrorType.Conflict);
        var final = ResultAssert.Succeeded(await host.FinalizePlan(scorecard));
        var again = await host.FinalizePlan(scorecard);
        var againFresh = await host.FinalizePlan(final);

        ResultAssert.Failed(again, ErrorType.Conflict);
        var error = ResultAssert.Failed(againFresh, ErrorType.Conflict);
        Assert.Contains("Draft", error.Message, StringComparison.Ordinal);
        Assert.Single(host.Repository<ReviewAction>().Items);
    }

    [Fact]
    public async Task A_plan_in_a_completed_period_is_not_finalized_and_a_missing_scorecard_is_a_404()
    {
        var (plan, _, scorecard) = await PlannedAsync();
        ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(plan.Period));

        ResultAssert.Failed(await host.FinalizePlan(scorecard), ErrorType.Conflict);
        ResultAssert.Failed(
            await host.Command<FinalizePlanCommand, ScorecardDetailDto>(new FinalizePlanCommand(9999, Requests.Version(scorecard.RowVersion))),
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Finalizing_records_an_activity_entry_without_the_reviewer_or_any_comment()
    {
        var (_, _, scorecard) = await PlannedAsync();

        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        Assert.Equal("KPI plan of EMP-0001 for \"2026 Q4\" finalized", host.ActivityLog.LastMessage);
        Assert.Equal(ActivityCategories.Performance, host.ActivityLog.Entries[^1].Category);
    }

    [Fact]
    public async Task A_failed_finalize_changes_nothing_and_logs_nothing()
    {
        var (_, _, scorecard) = await PlannedAsync();
        var entries = host.ActivityLog.Entries.Count;
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("The record was changed or deleted by someone else. Reload it and try again.");

        var result = await host.FinalizePlan(scorecard);

        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Equal(entries, host.ActivityLog.Entries.Count);
    }

    [Fact]
    public async Task Every_plan_write_runs_inside_the_plan_lock()
    {
        var (_, _, scorecard) = await PlannedAsync();
        host.UnitOfWork.TransactionLocks.Clear();

        var updated = ResultAssert.Succeeded(await host.UpdateItem(scorecard.Items[0].Id, ItemUpdateOf(scorecard.Items[0], weight: 55m)));
        Assert.True((await host.DeleteItem(scorecard.Items[1])).IsSuccess);
        ResultAssert.Succeeded(await host.UpdateItem(updated.Id, ItemUpdateOf(updated, weight: 100m)));
        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        Assert.Equal(["kpi-plan", "kpi-plan", "kpi-plan", "kpi-plan"], host.UnitOfWork.TransactionLocks);
    }
}
