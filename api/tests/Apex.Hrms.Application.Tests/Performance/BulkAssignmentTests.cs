using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>Assigning KPIs to many employees at once: who is assigned, who is skipped and why, the idempotent re-run, the cap and the selectors.</summary>
public sealed class BulkAssignmentTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private async Task<int> TemplateAsync(PlanSeed plan, string code, int? departmentId = null) =>
        (await host.CreateTemplateAsync(plan.Category.Id, code, "A KPI", departmentId)).Id;

    private static BulkAssignKpiRequest Filtered(int periodId, int? departmentId = null, int? branchId = null, int? positionId = null, int? jobGradeId = null, params AssignmentItemRequest[] items) =>
        new(periodId, null, departmentId, branchId, positionId, jobGradeId, items);

    // ---- Mixed outcomes ----

    [Fact]
    public async Task Employees_are_assigned_or_skipped_each_with_its_own_outcome()
    {
        var plan = await host.SeedPlanAsync();
        var ready = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var gone = await host.CreateStaffAsync(plan.Org, "EMP-0002");
        var already = await host.CreateStaffAsync(plan.Org, "EMP-0003");
        var template = await TemplateAsync(plan, "SALES-01");
        var other = await TemplateAsync(plan, "SALES-02");
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(gone.Id, Requests.Version(gone.RowVersion), new DateOnly(2026, 11, 1), "Resigned")));
        var existing = await host.AssignedAsync(already.Id, plan.Period.Id, Item(template));

        var result = ResultAssert.Succeeded(await host.AssignBulk(BulkOf(plan.Period.Id, [ready.Id, gone.Id, already.Id, 9999], Item(template), Item(other))));

        var assigned = Assert.Single(result.Assigned);
        Assert.Equal((ready.Id, "EMP-0001", 2), (assigned.EmployeeId, assigned.EmployeeCode, assigned.ItemsAdded));
        var scorecard = ResultAssert.Succeeded(await host.GetScorecard(assigned.ScorecardId));
        Assert.Equal(["SALES-01", "SALES-02"], scorecard.Items.Select(item => item.KpiCode));
        Assert.Equal("Planning", scorecard.Status);

        Assert.Equal([gone.Id, already.Id, 9999], result.Skipped.Select(skipped => skipped.EmployeeId));
        Assert.Contains("already assigned", result.Skipped.Single(skipped => skipped.EmployeeId == already.Id).Reason, StringComparison.Ordinal);
        Assert.Contains("has left", result.Skipped.Single(skipped => skipped.EmployeeId == gone.Id).Reason, StringComparison.Ordinal);
        var missing = result.Skipped.Single(skipped => skipped.EmployeeId == 9999);
        Assert.Null(missing.EmployeeCode);
        Assert.Contains("not found", missing.Reason, StringComparison.Ordinal);
        Assert.Equal(1, ResultAssert.Succeeded(await host.GetScorecard(existing.Id)).ItemCount);
    }

    [Fact]
    public async Task An_employee_of_another_company_and_a_department_template_mismatch_are_skipped_with_a_reason()
    {
        var plan = await host.SeedPlanAsync();
        var mine = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var otherCompany = await host.CreateCompanyAsync("OTH", "Other Holdings");
        var branch = await host.CreateBranchAsync(otherCompany.Id, "OTH-YGN");
        var department = await host.CreateDepartmentAsync(otherCompany.Id, branch.Id, "OTH-ENG");
        var position = await host.CreatePositionAsync(department.Id, plan.Org.Grade.Id, "OTH-DEV");
        var outsider = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(
            plan.Org with { Company = otherCompany, Branch = branch, Department = department, Position = position }, "EMP-0099", "outsider@example.test"));
        var other = await host.CreateDepartmentAsync(plan.Org.Company.Id, plan.Org.Branch.Id, "OPS");
        var opsOnly = await TemplateAsync(plan, "OPS-01", other.Id);
        var everyone = await TemplateAsync(plan, "ALL-01");

        var company = ResultAssert.Succeeded(await host.AssignBulk(BulkOf(plan.Period.Id, [mine.Id, outsider.Id], Item(everyone))));
        var mismatch = ResultAssert.Succeeded(await host.AssignBulk(BulkOf(plan.Period.Id, [mine.Id], Item(opsOnly))));

        Assert.Equal([mine.Id], company.Assigned.Select(entry => entry.EmployeeId));
        Assert.Contains("company", Assert.Single(company.Skipped).Reason, StringComparison.Ordinal);
        Assert.Empty(mismatch.Assigned);
        Assert.Contains("OPS-01", Assert.Single(mismatch.Skipped).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_employee_whose_weights_would_exceed_100_or_whose_plan_is_final_is_skipped()
    {
        var plan = await host.SeedPlanAsync();
        var heavy = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var final = await host.CreateStaffAsync(plan.Org, "EMP-0002");
        var fresh = await host.CreateStaffAsync(plan.Org, "EMP-0003");
        var first = await TemplateAsync(plan, "A-1");
        var second = await TemplateAsync(plan, "A-2");
        await host.AssignedAsync(heavy.Id, plan.Period.Id, Item(first, weight: 90m));
        var finalized = await host.AssignedAsync(final.Id, plan.Period.Id, Item(first, weight: 100m));
        ResultAssert.Succeeded(await host.FinalizePlan(finalized));

        var result = ResultAssert.Succeeded(await host.AssignBulk(BulkOf(plan.Period.Id, [heavy.Id, final.Id, fresh.Id], Item(second, weight: 20m))));

        Assert.Equal([fresh.Id], result.Assigned.Select(entry => entry.EmployeeId));
        Assert.Contains("110.00%", result.Skipped.Single(entry => entry.EmployeeId == heavy.Id).Reason, StringComparison.Ordinal);
        Assert.Contains("Draft", result.Skipped.Single(entry => entry.EmployeeId == final.Id).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_employee_with_a_planning_scorecard_gets_the_new_kpis_added_to_it()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var first = await TemplateAsync(plan, "A-1");
        var second = await TemplateAsync(plan, "A-2");
        var existing = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(first, weight: 40m));

        var result = ResultAssert.Succeeded(await host.AssignBulk(BulkOf(plan.Period.Id, [employee.Id], Item(second, weight: 60m))));

        var entry = Assert.Single(result.Assigned);
        Assert.Equal((existing.Id, 1), (entry.ScorecardId, entry.ItemsAdded));
        Assert.Equal(100m, ResultAssert.Succeeded(await host.GetScorecard(existing.Id)).TotalWeight);
    }

    // ---- Idempotence ----

    [Fact]
    public async Task Running_the_same_request_again_assigns_nothing_twice()
    {
        var plan = await host.SeedPlanAsync();
        var one = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var two = await host.CreateStaffAsync(plan.Org, "EMP-0002");
        var template = await TemplateAsync(plan, "SALES-01");
        var request = BulkOf(plan.Period.Id, [one.Id, two.Id], Item(template));
        ResultAssert.Succeeded(await host.AssignBulk(request));
        var entries = host.ActivityLog.Entries.Count;
        var saves = host.UnitOfWork.SaveCount;

        var again = ResultAssert.Succeeded(await host.AssignBulk(request));

        Assert.Empty(again.Assigned);
        Assert.Equal(2, again.Skipped.Count);
        Assert.All(again.Skipped, skipped => Assert.Contains("already assigned", skipped.Reason, StringComparison.Ordinal));
        Assert.Equal(2, host.Repository<Apex.Hrms.Domain.Performance.ScorecardItem>().Items.Count);
        Assert.Equal(entries, host.ActivityLog.Entries.Count);
        Assert.Equal(saves, host.UnitOfWork.SaveCount);
    }

    // ---- Activity and writing ----

    [Fact]
    public async Task One_activity_entry_summarises_the_run_with_counts_and_the_period_name()
    {
        var plan = await host.SeedPlanAsync();
        var one = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var two = await host.CreateStaffAsync(plan.Org, "EMP-0002");
        var template = await TemplateAsync(plan, "SALES-01");
        var before = host.ActivityLog.Entries.Count;

        ResultAssert.Succeeded(await host.AssignBulk(BulkOf(plan.Period.Id, [one.Id, two.Id, 9999], Item(template))));

        var entry = Assert.Single(host.ActivityLog.Entries.Skip(before));
        Assert.Equal(ActivityCategories.Performance, entry.Category);
        Assert.Equal("KPIs assigned in bulk for \"2026 Q4\": 2 employee(s) assigned, 1 skipped", entry.Message);
    }

    // ---- Resolving by filters ----

    [Fact]
    public async Task Filters_pick_the_current_employees_of_the_periods_company_that_match_all_of_them()
    {
        var plan = await host.SeedPlanAsync();
        var inDepartment = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var gone = await host.CreateStaffAsync(plan.Org, "EMP-0002");
        var other = await host.CreateDepartmentAsync(plan.Org.Company.Id, plan.Org.Branch.Id, "OPS");
        var otherPosition = await host.CreatePositionAsync(other.Id, plan.Org.Grade.Id, "OPS-LEAD");
        var inOther = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(
            plan.Org with { Department = other, Position = otherPosition }, "EMP-0003", "ops@example.test"));
        var template = await TemplateAsync(plan, "ALL-01");
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(gone.Id, Requests.Version(gone.RowVersion), new DateOnly(2026, 11, 1), "Terminated")));

        var byDepartment = ResultAssert.Succeeded(await host.AssignBulk(Filtered(plan.Period.Id, departmentId: plan.Org.Department.Id, items: Item(template))));
        var byBranchAndPosition = ResultAssert.Succeeded(await host.AssignBulk(Filtered(plan.Period.Id, branchId: plan.Org.Branch.Id, positionId: otherPosition.Id, items: Item(template))));

        Assert.Equal([inDepartment.Id], byDepartment.Assigned.Select(entry => entry.EmployeeId));
        Assert.Empty(byDepartment.Skipped);
        Assert.Equal([inOther.Id], byBranchAndPosition.Assigned.Select(entry => entry.EmployeeId));
    }

    [Fact]
    public async Task Filters_that_match_nobody_give_an_empty_result()
    {
        var plan = await host.SeedPlanAsync();
        var template = await TemplateAsync(plan, "ALL-01");

        var result = ResultAssert.Succeeded(await host.AssignBulk(Filtered(plan.Period.Id, departmentId: 9999, items: Item(template))));

        Assert.Empty(result.Assigned);
        Assert.Empty(result.Skipped);
    }

    // ---- The selectors and the cap ----

    [Fact]
    public async Task Exactly_one_kind_of_selector_is_accepted()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "ALL-01");

        ResultAssert.FailedOnField(await host.AssignBulk(new BulkAssignKpiRequest(plan.Period.Id, null, null, null, null, null, [Item(template)])), "employeeIds");
        ResultAssert.FailedOnField(await host.AssignBulk(new BulkAssignKpiRequest(plan.Period.Id, [employee.Id], plan.Org.Department.Id, null, null, null, [Item(template)])), "employeeIds");
        ResultAssert.FailedOnField(await host.AssignBulk(new BulkAssignKpiRequest(plan.Period.Id, [], null, null, null, null, [Item(template)])), "employeeIds");
        ResultAssert.FailedOnField(await host.AssignBulk(new BulkAssignKpiRequest(plan.Period.Id, [0], null, null, null, null, [Item(template)])), "employeeIds");
        Assert.True((await host.AssignBulk(new BulkAssignKpiRequest(plan.Period.Id, [employee.Id], null, null, null, null, [Item(template)]))).IsSuccess);
    }

    [Fact]
    public async Task Five_hundred_employees_are_the_most_one_call_takes()
    {
        var plan = await host.SeedPlanAsync();
        var template = await TemplateAsync(plan, "ALL-01");

        var tooMany = await host.AssignBulk(BulkOf(plan.Period.Id, [.. Enumerable.Range(1, 501)], Item(template)));
        var atTheCap = await host.AssignBulk(BulkOf(plan.Period.Id, [.. Enumerable.Range(1, 500)], Item(template)));

        ResultAssert.FailedOnField(tooMany, "employeeIds");
        Assert.Equal(500, ResultAssert.Succeeded(atTheCap).Skipped.Count);
    }

    // ---- Failures of the whole call ----

    [Fact]
    public async Task A_bad_period_template_or_item_fails_the_whole_call_before_anything_is_written()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        var scorecards = host.Repository<Apex.Hrms.Domain.Performance.Scorecard>();

        ResultAssert.FailedOnField(await host.AssignBulk(BulkOf(9999, [employee.Id], Item(template))), "periodId");
        ResultAssert.FailedOnField(await host.AssignBulk(BulkOf(plan.Period.Id, [employee.Id], Item(9999))), "items[0].templateId");
        ResultAssert.FailedOnField(await host.AssignBulk(BulkOf(plan.Period.Id, [employee.Id], Item(template, target: 0m))), "items[0].target");
        ResultAssert.FailedOnField(await host.AssignBulk(BulkOf(plan.Period.Id, [employee.Id], Item(template, weight: 60m), Item(await TemplateAsync(plan, "SALES-02"), weight: 60m))), "items");
        ResultAssert.FailedOnField(await host.AssignBulk(BulkOf(plan.Period.Id, [employee.Id])), "items");
        Assert.Empty(scorecards.Items);
    }

    [Fact]
    public async Task A_completed_period_fails_the_whole_call_with_a_conflict()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(plan.Period));

        ResultAssert.Failed(await host.AssignBulk(BulkOf(plan.Period.Id, [employee.Id], Item(template))), ErrorType.Conflict);
    }
}
