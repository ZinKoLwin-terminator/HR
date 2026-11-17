using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>Assigning KPIs to one employee: the scorecard it creates or extends, the snapshot, the eligibility rules and the weight cap.</summary>
public sealed class ScorecardAssignmentTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private async Task<KpiTemplateDto> TemplateAsync(PlanSeed plan, string code, string name = "A KPI", int? departmentId = null) =>
        await host.CreateTemplateAsync(plan.Category.Id, code, name, departmentId);

    // ---- A new scorecard ----

    [Fact]
    public async Task A_first_assignment_creates_a_planning_scorecard_with_the_placement_copied_and_the_template_snapshotted()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01", "Monthly sales");

        var result = ResultAssert.Succeeded(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id)));

        Assert.True(result.Created);
        var scorecard = result.Scorecard;
        Assert.Equal("Planning", scorecard.Status);
        Assert.Equal((employee.Id, "EMP-0001", "Mia Hartley"), (scorecard.EmployeeId, scorecard.EmployeeCode, scorecard.EmployeeName));
        Assert.Equal((plan.Period.Id, plan.Period.Name), (scorecard.PeriodId, scorecard.PeriodName));
        Assert.Equal(plan.Org.Department.Id, scorecard.DepartmentId);
        Assert.Null(scorecard.ReviewerEmployeeId);
        Assert.Null(scorecard.PlanFinalizedOn);
        Assert.Empty(scorecard.History);
        Assert.Equal((1, 20m), (scorecard.ItemCount, scorecard.TotalWeight));
        Assert.NotEmpty(scorecard.RowVersion);

        var item = Assert.Single(scorecard.Items);
        Assert.Equal((template.Id, plan.Category.Id, "Productivity"), (item.TemplateId, item.CategoryId, item.CategoryName));
        Assert.Equal(("SALES-01", "Monthly sales"), (item.KpiCode, item.KpiName));
        Assert.Equal(("Number", "Higher Is Better", "Direct Percentage"), (item.MeasurementType, item.Direction, item.ScoringMethod));
        Assert.Equal((100m, 20m), (item.Target, item.Weight));
        Assert.Equal((plan.Period.StartDate, plan.Period.EndDate, 0), (item.StartDate, item.EndDate, item.SortOrder));
        Assert.Null(item.ActualValue);
        Assert.Null(item.AchievementPct);
        Assert.NotEmpty(item.RowVersion);
    }

    [Fact]
    public async Task The_scorecard_keeps_the_placement_of_the_day_it_was_created_when_the_employee_moves_later()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template.Id));
        var other = await host.CreateDepartmentAsync(plan.Org.Company.Id, plan.Org.Branch.Id, "OPS");
        var otherPosition = await host.CreatePositionAsync(other.Id, plan.Org.Grade.Id, "OPS-LEAD");
        var current = await host.GetEmployee(employee.Id);

        ResultAssert.Succeeded(await host.UpdateEmployee(employee.Id, EmployeeFixtures.UpdateOf(current.Value) with { DepartmentId = other.Id, PositionId = otherPosition.Id }));

        var after = ResultAssert.Succeeded(await host.GetScorecard(scorecard.Id));
        Assert.Equal(plan.Org.Department.Id, after.DepartmentId);
    }

    [Fact]
    public async Task Overridden_target_weight_dates_and_order_are_used_and_the_rest_default()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var first = await TemplateAsync(plan, "A-1");
        var second = await TemplateAsync(plan, "A-2");

        var scorecard = await host.AssignedAsync(
            employee.Id,
            plan.Period.Id,
            Item(first.Id, target: 250.5m, weight: 35.25m, start: new DateOnly(2026, 11, 1), end: new DateOnly(2026, 11, 30), sortOrder: 7),
            Item(second.Id));

        Assert.Equal([7, 1], scorecard.Items.OrderBy(item => item.KpiCode).Select(item => item.SortOrder));
        var overridden = scorecard.Items.Single(item => item.KpiCode == "A-1");
        Assert.Equal((250.5m, 35.25m, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)), (overridden.Target, overridden.Weight, overridden.StartDate, overridden.EndDate));
        Assert.Equal(55.25m, scorecard.TotalWeight);
    }

    [Fact]
    public async Task A_template_edited_later_does_not_change_the_assigned_kpi()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01", "Monthly sales");
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template.Id));

        ResultAssert.Succeeded(await host.Update<KpiTemplateUpdateRequest, KpiTemplateDto>(
            template.Id,
            KpiFixtures.TemplateUpdateOf(template) with { Name = "Renamed", DefaultTarget = 500m, DefaultWeight = 60m }));

        var item = Assert.Single(ResultAssert.Succeeded(await host.GetScorecard(scorecard.Id)).Items);
        Assert.Equal(("SALES-01", "Monthly sales", 100m, 20m), (item.KpiCode, item.KpiName, item.Target, item.Weight));
    }

    [Fact]
    public async Task An_assignment_records_one_activity_entry_naming_the_employee_by_code_and_the_period()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var one = await TemplateAsync(plan, "A-1");
        var two = await TemplateAsync(plan, "A-2");
        var before = host.ActivityLog.Entries.Count;

        await host.AssignedAsync(employee.Id, plan.Period.Id, Item(one.Id), Item(two.Id));

        var entry = Assert.Single(host.ActivityLog.Entries.Skip(before));
        Assert.Equal(ActivityCategories.Performance, entry.Category);
        Assert.Equal("2 KPI(s) assigned to EMP-0001 for \"2026 Q4\"", entry.Message);
        Assert.DoesNotContain("Mia", entry.Message, StringComparison.Ordinal);
    }

    // ---- An existing scorecard ----

    [Fact]
    public async Task A_second_assignment_adds_to_the_scorecard_in_planning_and_is_not_a_creation()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var first = await TemplateAsync(plan, "A-1");
        var second = await TemplateAsync(plan, "A-2");
        var created = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(first.Id, weight: 30m));

        var added = ResultAssert.Succeeded(await host.Assign(employee.Id, plan.Period.Id, Item(second.Id, weight: 70m)));

        Assert.False(added.Created);
        Assert.Equal(created.Id, added.Scorecard.Id);
        Assert.Equal(["A-1", "A-2"], added.Scorecard.Items.Select(item => item.KpiCode));
        Assert.Equal([0, 1], added.Scorecard.Items.Select(item => item.SortOrder));
        Assert.Equal((2, 100m), (added.Scorecard.ItemCount, added.Scorecard.TotalWeight));
    }

    [Fact]
    public async Task A_kpi_already_on_the_scorecard_is_a_conflict_naming_its_code()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template.Id));

        var error = ResultAssert.Failed(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id)), ErrorType.Conflict);

        Assert.Contains("SALES-01", error.Message, StringComparison.Ordinal);
        Assert.Contains("already assigned", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_scorecard_past_planning_takes_no_more_kpis()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var first = await TemplateAsync(plan, "A-1");
        var second = await TemplateAsync(plan, "A-2");
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(first.Id, weight: 100m));
        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        var error = ResultAssert.Failed(await host.Assign(employee.Id, plan.Period.Id, Item(second.Id, weight: 10m)), ErrorType.Conflict);

        Assert.Contains("Draft", error.Message, StringComparison.Ordinal);
    }

    // ---- Eligibility ----

    [Fact]
    public async Task An_employee_that_does_not_exist_is_a_400_on_employee_id()
    {
        var plan = await host.SeedPlanAsync();
        var template = await TemplateAsync(plan, "SALES-01");

        ResultAssert.FailedOnField(await host.Assign(999, plan.Period.Id, Item(template.Id)), "employeeId");
    }

    [Theory]
    [InlineData("Resigned")]
    [InlineData("Terminated")]
    public async Task An_employee_who_has_left_cannot_be_assigned_KPIs(string status)
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(employee.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 11, 1), status)));

        var error = ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id)), "employeeId");

        Assert.Contains("has left", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_employee_of_another_company_than_the_period_is_a_400_on_employee_id()
    {
        var plan = await host.SeedPlanAsync();
        var template = await TemplateAsync(plan, "SALES-01");
        var otherCompany = await host.CreateCompanyAsync("OTH", "Other Holdings");
        var branch = await host.CreateBranchAsync(otherCompany.Id, "OTH-YGN");
        var department = await host.CreateDepartmentAsync(otherCompany.Id, branch.Id, "OTH-ENG");
        var position = await host.CreatePositionAsync(department.Id, plan.Org.Grade.Id, "OTH-DEV");
        var outsider = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(
            plan.Org with { Company = otherCompany, Branch = branch, Department = department, Position = position }, "EMP-0099", "outsider@example.test"));

        var error = ResultAssert.FailedOnField(await host.Assign(outsider.Id, plan.Period.Id, Item(template.Id)), "employeeId");

        Assert.Contains("company", error.Message, StringComparison.Ordinal);
    }

    // ---- The period ----

    [Fact]
    public async Task A_period_that_does_not_exist_is_a_400_on_period_id()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");

        ResultAssert.FailedOnField(await host.Assign(employee.Id, 999, Item(template.Id)), "periodId");
    }

    [Fact]
    public async Task A_draft_period_takes_assignments()
    {
        var plan = await host.SeedPlanAsync(activate: false);
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");

        Assert.True((await host.Assign(employee.Id, plan.Period.Id, Item(template.Id))).IsSuccess);
    }

    [Fact]
    public async Task A_completed_or_closed_period_is_a_conflict()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        var completed = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(plan.Period));

        var whenCompleted = ResultAssert.Failed(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id)), ErrorType.Conflict);
        ResultAssert.Succeeded(await host.MoveAsync<ClosePeriodCommand>(completed));
        var whenClosed = ResultAssert.Failed(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id)), ErrorType.Conflict);

        Assert.Contains("Completed", whenCompleted.Message, StringComparison.Ordinal);
        Assert.Contains("Closed", whenClosed.Message, StringComparison.Ordinal);
    }

    // ---- Templates ----

    [Fact]
    public async Task A_missing_template_is_a_400_on_the_item_and_every_bad_item_is_reported()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");

        var error = ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(999), Item(template.Id), Item(998)), "items[0].templateId");

        Assert.Contains("items[2].templateId", error.FieldErrors!.Keys);
        Assert.DoesNotContain("items[1].templateId", error.FieldErrors.Keys);
    }

    [Fact]
    public async Task An_inactive_template_cannot_be_assigned()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        ResultAssert.Succeeded(await host.Command<DeactivateCommand, KpiTemplateDto>(new DeactivateCommand(template.Id, Requests.Version(template.RowVersion))));

        var error = ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id)), "items[0].templateId");

        Assert.Contains("inactive", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_department_template_must_match_the_employees_department()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var other = await host.CreateDepartmentAsync(plan.Org.Company.Id, plan.Org.Branch.Id, "OPS");
        var elsewhere = await TemplateAsync(plan, "OPS-01", departmentId: other.Id);
        var here = await TemplateAsync(plan, "ENG-01", departmentId: plan.Org.Department.Id);

        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(elsewhere.Id)), "items[0].templateId");
        Assert.True((await host.Assign(employee.Id, plan.Period.Id, Item(here.Id))).IsSuccess);
    }

    [Fact]
    public async Task The_same_template_twice_in_one_request_is_a_400_on_the_second_item()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");

        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, weight: 10m), Item(template.Id, weight: 10m)), "items[1].templateId");
    }

    [Fact]
    public async Task A_zero_target_override_on_higher_is_better_is_a_400_on_the_items_target()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");

        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, target: 0m)), "items[0].target");
    }

    [Fact]
    public async Task Dates_outside_the_period_are_400s_on_the_items_dates()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");

        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, start: new DateOnly(2026, 9, 30))), "items[0].startDate");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, end: new DateOnly(2027, 1, 1))), "items[0].endDate");
        Assert.True((await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, start: new DateOnly(2026, 10, 1), end: new DateOnly(2026, 12, 31)))).IsSuccess);
    }

    // ---- The request shape ----

    [Fact]
    public async Task The_request_shape_is_validated_with_camel_case_field_errors()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");

        ResultAssert.FailedOnField(await host.Assign(0, plan.Period.Id, Item(template.Id)), "employeeId");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, 0, Item(template.Id)), "periodId");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id), "items");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(0)), "items[0].templateId");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, target: -1m)), "items[0].target");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, target: 1.00001m)), "items[0].target");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, weight: 0m)), "items[0].weight");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, weight: 100.01m)), "items[0].weight");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, weight: 10.123m)), "items[0].weight");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, sortOrder: -1)), "items[0].sortOrder");
        ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(template.Id, start: new DateOnly(2026, 11, 5), end: new DateOnly(2026, 11, 4))), "items[0].endDate");
    }

    [Fact]
    public async Task One_to_fifty_items_are_accepted_and_fifty_one_are_not()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var ids = new List<int>();
        for (var i = 1; i <= 51; i++)
        {
            ids.Add((await TemplateAsync(plan, $"K-{i:00}")).Id);
        }

        var tooMany = await host.Assign(employee.Id, plan.Period.Id, [.. ids.Select(id => Item(id, weight: 1m))]);
        var fifty = await host.Assign(employee.Id, plan.Period.Id, [.. ids.Take(50).Select(id => Item(id, weight: 1m))]);

        ResultAssert.FailedOnField(tooMany, "items");
        Assert.Equal(50, ResultAssert.Succeeded(fifty).Scorecard.ItemCount);
    }

    // ---- The weight cap ----

    [Fact]
    public async Task Weights_may_add_up_to_exactly_100_but_not_more_in_one_request()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var one = await TemplateAsync(plan, "A-1");
        var two = await TemplateAsync(plan, "A-2");

        var over = ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(one.Id, weight: 60m), Item(two.Id, weight: 40.01m)), "items");
        var exact = await host.Assign(employee.Id, plan.Period.Id, Item(one.Id, weight: 60m), Item(two.Id, weight: 40m));

        Assert.Contains("100.01%", over.Message, StringComparison.Ordinal);
        Assert.True(exact.IsSuccess);
    }

    [Fact]
    public async Task The_total_includes_the_kpis_already_on_the_scorecard_and_names_it()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var one = await TemplateAsync(plan, "A-1");
        var two = await TemplateAsync(plan, "A-2");
        await host.AssignedAsync(employee.Id, plan.Period.Id, Item(one.Id, weight: 80m));

        var error = ResultAssert.FailedOnField(await host.Assign(employee.Id, plan.Period.Id, Item(two.Id, weight: 20.5m)), "items");

        Assert.Contains("100.50%", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, ResultAssert.Succeeded(await host.GetScorecard(1)).ItemCount);
    }

    [Fact]
    public async Task A_total_below_100_is_fine_while_planning()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "A-1");

        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template.Id, weight: 33.33m));

        Assert.Equal(33.33m, scorecard.TotalWeight);
        Assert.Equal("Planning", scorecard.Status);
    }

    // ---- A failed save leaves no activity entry ----

    [Fact]
    public async Task A_conflict_on_save_such_as_a_lost_race_is_returned_and_leaves_no_activity_entry()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var template = await TemplateAsync(plan, "SALES-01");
        var before = host.ActivityLog.Entries.Count;
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("This employee already has a scorecard for this period. Reload and try again.");

        var result = await host.Assign(employee.Id, plan.Period.Id, Item(template.Id));

        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Equal(before, host.ActivityLog.Entries.Count);
    }
}
