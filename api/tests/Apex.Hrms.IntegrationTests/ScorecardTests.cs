using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Org = (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position);

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Assigning KPIs, planning and finalizing a scorecard, and reading scorecards, against SQL Server: the handlers with the real EF
/// mapping and projections, the composite foreign keys that hold the copied placement, the unique indexes that settle a race, the
/// review history row, concurrency and delete protection. Fictional data only; every row carries the tag of its test.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ScorecardTests : DatabaseTest
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private async Task<(Org Org, KpiCategoryDto Category, EvaluationPeriodDto Period)> SeedPlanAsync()
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var period = await SeedActivePeriodAsync(org.Company.Id);
        return (org, category, period);
    }

    private async Task<EmployeeDto> EmployeeAsync(Org org, string suffix, string local, int? managerId = null) =>
        Succeeded(await Create(
            Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, suffix, local) with { ManagerId = managerId }));

    private Task<Result<ScorecardDetailDto>> GetScorecard(int id) =>
        InScopeAsync(sp => sp.GetRequiredService<IGetByIdHandler<ScorecardDetailDto>>().HandleAsync(id, default));

    private Task<Result<PagedResult<ScorecardDto>>> QueryScorecards(ScorecardFilter? filter = null, string? search = null, string? sort = null, string direction = "asc", int pageIndex = 0, int pageSize = 50) =>
        Query<ScorecardDto, ScorecardFilter>(new PageRequest<ScorecardFilter>(pageIndex, pageSize, sort is null ? null : new SortState(sort, direction), search, filter ?? new ScorecardFilter()));

    private Task<Result<ScorecardItemDto>> UpdateItem(int id, ScorecardItemUpdateRequest request) =>
        InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<ScorecardItemUpdateRequest, ScorecardItemDto>>().HandleAsync(id, request, default));

    private Task<Result> DeleteItem(ScorecardItemDto item) => Delete<ScorecardItemDto>(item.Id, Requests.Version(item.RowVersion));

    private Task<Result> DeleteScorecard(ScorecardDetailDto scorecard) => Delete<ScorecardDto>(scorecard.Id, Requests.Version(scorecard.RowVersion));

    private static ScorecardItemUpdateRequest UpdateOf(ScorecardItemDto item, decimal? weight = null, decimal? target = null) =>
        new(target ?? item.Target, weight ?? item.Weight, item.StartDate, item.EndDate, item.SortOrder, Requests.Version(item.RowVersion));

    private static Task<T> Scalar<T>(string sql, params (string Name, object? Value)[] parameters) => Sql.ScalarAsync<T>(sql, parameters);

    // ---- Assignment round trip ----

    [IntegrationFact]
    public async Task An_assignment_creates_the_planning_scorecard_with_the_placement_and_the_snapshot_and_a_later_template_edit_changes_nothing()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "mia");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01", "Monthly sales");

        var assigned = Succeeded(await Assign(employee.Id, period.Id, ItemOf(template.Id)));

        Assert.True(assigned.Created);
        var id = assigned.Scorecard.Id;
        Assert.Equal("Planning", await Scalar<string>("SELECT Status FROM kpi.Scorecard WHERE Id = @id", ("@id", id)));
        Assert.Equal(
            $"{org.Company.Id}/{org.Branch.Id}/{org.Department.Id}/{org.Position.Id}",
            await Scalar<string>("SELECT CONCAT(CompanyId, '/', BranchId, '/', DepartmentId, '/', PositionId) FROM kpi.Scorecard WHERE Id = @id", ("@id", id)));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @id AND ReviewerEmployeeId IS NULL AND PlanFinalizedOn IS NULL", ("@id", id)));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @id AND TemplateId = @template AND CategoryId = @category AND KpiCode = @code AND Target = 100 AND Weight = 20 AND AssignedOn = '2026-11-09' AND AssignedByUserId = @user AND StartDate = '2026-10-01' AND EndDate = '2026-12-31'",
            ("@id", id),
            ("@template", template.Id),
            ("@category", category.Id),
            ("@code", template.Code),
            ("@user", TestAdministrator.AccountId)));

        Succeeded(await Update(template.Id, Requests.KpiTemplateUpdate(template, name: "Renamed", target: 500m, weight: 60m)));

        var detail = Succeeded(await GetScorecard(id));
        var item = Assert.Single(detail.Items);
        Assert.Equal((template.Code, Name("Monthly sales"), 100m, 20m), (item.KpiCode, item.KpiName, item.Target, item.Weight));
        Assert.Equal((employee.EmployeeCode, "Planning", period.Name), (detail.EmployeeCode, detail.Status, detail.PeriodName));
        Assert.Equal("Renamed", await Scalar<string>("SELECT Name FROM kpi.Template WHERE Id = @id", ("@id", template.Id)));
    }

    [IntegrationFact]
    public async Task The_scorecard_keeps_the_placement_of_creation_when_the_employee_moves_and_a_second_assignment_extends_it()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "mia");
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02");
        var created = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 30m));
        var other = await SeedDepartmentAsync(org.Company.Id, org.Branch.Id, "A-OPS", "Operations");
        var otherPosition = await SeedPositionAsync(other.Id, org.Grade.Id, "A-OL");
        var current = Succeeded(await Get<EmployeeDto>(employee.Id));

        Succeeded(await Update(employee.Id, Requests.EmployeeUpdate(current) with { DepartmentId = other.Id, PositionId = otherPosition.Id }));
        var extended = Succeeded(await Assign(employee.Id, period.Id, ItemOf(second.Id, weight: 70m)));

        Assert.False(extended.Created);
        Assert.Equal(created.Id, extended.Scorecard.Id);
        Assert.Equal(org.Department.Id, await Scalar<int>("SELECT DepartmentId FROM kpi.Scorecard WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal(org.Position.Id, await Scalar<int>("SELECT PositionId FROM kpi.Scorecard WHERE Id = @id", ("@id", created.Id)));
        Assert.Equal([0, 1], extended.Scorecard.Items.Select(item => item.SortOrder));
        Assert.Equal(100m, extended.Scorecard.TotalWeight);
    }

    [IntegrationFact]
    public async Task One_activity_entry_per_assignment_names_the_employee_by_code_and_the_period()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "mia");
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02");

        await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id), ItemOf(second.Id));

        var messages = await ActivityMessagesAsync("%KPI(s) assigned%");
        Assert.Equal([$"2 KPI(s) assigned to {employee.EmployeeCode} for \"{period.Name}\""], messages);
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Message LIKE @m AND Category = 'performance'", ("@m", "%KPI(s) assigned%" + period.Name + "%")));
    }

    private static async Task<List<string>> ActivityMessagesAsync(string like)
    {
        var count = await Scalar<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Message LIKE @m", ("@m", like));
        var list = new List<string>();
        for (var i = 0; i < count; i++)
        {
            list.Add(await Scalar<string>("SELECT Message FROM app.ActivityLog WHERE Message LIKE @m ORDER BY Id OFFSET @i ROWS FETCH NEXT 1 ROWS ONLY", ("@m", like), ("@i", i)));
        }

        return list;
    }

    // ---- Eligibility against the database ----

    [IntegrationFact]
    public async Task A_left_employee_a_missing_period_and_a_closed_period_are_refused()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "mia");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        Succeeded(await RunCommand<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(employee.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 11, 1), "Resigned")));

        FailedOnField(await Assign(employee.Id, period.Id, ItemOf(template.Id)), "employeeId");
        FailedOnField(await Assign(employee.Id, 2_000_000_000, ItemOf(template.Id)), "periodId");

        var active = await EmployeeAsync(org, "2", "ben");
        var completed = Succeeded(await RunCommand<CompletePeriodCommand, EvaluationPeriodDto>(new CompletePeriodCommand(period.Id, Requests.Version((await Get<EvaluationPeriodDto>(period.Id)).Value.RowVersion))));
        Failed(await Assign(active.Id, completed.Id, ItemOf(template.Id)), ErrorType.Conflict);
    }

    // ---- Races settled by the unique indexes ----

    [IntegrationFact]
    public async Task A_scorecard_created_by_another_session_between_the_check_and_the_save_is_a_conflict_and_leaves_nothing_of_ours()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "mia");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        BeforeNextSave = () => Sql.ExecuteAsync(
            "INSERT INTO kpi.Scorecard (EmployeeId, PeriodId, CompanyId, BranchId, DepartmentId, PositionId) VALUES (@e, @p, @c, @b, @d, @pos);",
            ("@e", employee.Id),
            ("@p", period.Id),
            ("@c", org.Company.Id),
            ("@b", org.Branch.Id),
            ("@d", org.Department.Id),
            ("@pos", org.Position.Id));

        var raced = await Assign(employee.Id, period.Id, ItemOf(template.Id));

        var error = Failed(raced, ErrorType.Conflict);
        Assert.Contains("already has a scorecard", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE EmployeeId = @e AND PeriodId = @p", ("@e", employee.Id), ("@p", period.Id)));
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItem i JOIN kpi.Scorecard s ON s.Id = i.ScorecardId WHERE s.EmployeeId = @e", ("@e", employee.Id)));
        Assert.Empty(await ActivityMessagesAsync("%KPI(s) assigned%"));
    }

    [IntegrationFact]
    public async Task A_kpi_assigned_by_another_session_between_the_check_and_the_save_is_a_conflict()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "mia");
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 30m));
        BeforeNextSave = () => Sql.ExecuteAsync(
            """
            INSERT INTO kpi.ScorecardItem (ScorecardId, TemplateId, CategoryId, KpiCode, KpiName, MeasurementType, Direction, ScoringMethod, Target, Weight, StartDate, EndDate, AssignedOn)
            VALUES (@s, @t, @c, @code, @name, 'Number', 'Higher Is Better', 'Direct Percentage', 100, 10, '2026-10-01', '2026-12-31', '2026-11-09');
            """,
            ("@s", scorecard.Id),
            ("@t", second.Id),
            ("@c", category.Id),
            ("@code", second.Code),
            ("@name", second.Name));

        var raced = await Assign(employee.Id, period.Id, ItemOf(second.Id, weight: 20m));

        Assert.Contains("was just assigned", Failed(raced, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Equal(2, await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @s", ("@s", scorecard.Id)));
    }

    // ---- Bulk ----

    [IntegrationFact]
    public async Task A_bulk_assignment_assigns_some_and_skips_others_and_running_it_again_assigns_nothing_twice()
    {
        var (org, category, period) = await SeedPlanAsync();
        var ready = await EmployeeAsync(org, "1", "ria");
        var gone = await EmployeeAsync(org, "2", "rex");
        var already = await EmployeeAsync(org, "3", "nora");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        var other = await SeedKpiTemplateAsync(category.Id, "S-02");
        Succeeded(await RunCommand<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(gone.Id, Requests.Version(gone.RowVersion), new DateOnly(2026, 11, 1), "Terminated")));
        await SeedScorecardAsync(already.Id, period.Id, ItemOf(template.Id));
        var request = new BulkAssignKpiRequest(period.Id, [ready.Id, gone.Id, already.Id, 2_000_000_000], null, null, null, null, [ItemOf(template.Id), ItemOf(other.Id)]);

        var result = Succeeded(await AssignBulk(request));

        var assigned = Assert.Single(result.Assigned);
        Assert.Equal((ready.Id, ready.EmployeeCode, 2), (assigned.EmployeeId, assigned.EmployeeCode, assigned.ItemsAdded));
        Assert.Equal(3, result.Skipped.Count);
        Assert.Contains("already assigned", result.Skipped.Single(entry => entry.EmployeeId == already.Id).Reason, StringComparison.Ordinal);
        Assert.Contains("has left", result.Skipped.Single(entry => entry.EmployeeId == gone.Id).Reason, StringComparison.Ordinal);
        Assert.Null(result.Skipped.Single(entry => entry.EmployeeId == 2_000_000_000).EmployeeCode);
        Assert.Equal(2, await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @s", ("@s", assigned.ScorecardId)));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId IN (SELECT Id FROM kpi.Scorecard WHERE EmployeeId = @e)", ("@e", already.Id)));
        Assert.Equal(
            [$"KPIs assigned in bulk for \"{period.Name}\": 1 employee(s) assigned, 3 skipped"],
            await ActivityMessagesAsync("%assigned in bulk%"));

        var again = Succeeded(await AssignBulk(request));

        Assert.Empty(again.Assigned);
        Assert.Equal(4, again.Skipped.Count(entry => entry.Reason.Contains("already assigned", StringComparison.Ordinal) || entry.Reason.Contains("has left", StringComparison.Ordinal) || entry.Reason.Contains("not found", StringComparison.Ordinal)));
        Assert.Equal(2, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE PeriodId = @p", ("@p", period.Id)));
        Assert.Single(await ActivityMessagesAsync("%assigned in bulk%"));
    }

    [IntegrationFact]
    public async Task A_bulk_assignment_by_filters_reaches_the_current_employees_of_the_company_that_match()
    {
        var (org, category, period) = await SeedPlanAsync();
        var inDepartment = await EmployeeAsync(org, "1", "ria");
        var gone = await EmployeeAsync(org, "2", "rex");
        var other = await SeedDepartmentAsync(org.Company.Id, org.Branch.Id, "A-OPS", "Operations");
        var otherPosition = await SeedPositionAsync(other.Id, org.Grade.Id, "A-OL");
        var inOther = Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, other.Id, otherPosition.Id, org.Grade.Id, "3", "nora")));
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        Succeeded(await RunCommand<ExitEmployeeCommand, EmployeeDto>(new ExitEmployeeCommand(gone.Id, Requests.Version(gone.RowVersion), new DateOnly(2026, 11, 1), "Resigned")));

        var byDepartment = Succeeded(await AssignBulk(new BulkAssignKpiRequest(period.Id, null, org.Department.Id, null, null, null, [ItemOf(template.Id)])));
        var byGradeAndBranch = Succeeded(await AssignBulk(new BulkAssignKpiRequest(period.Id, null, null, org.Branch.Id, otherPosition.Id, org.Grade.Id, [ItemOf(template.Id)])));

        Assert.Equal([inDepartment.Id], byDepartment.Assigned.Select(entry => entry.EmployeeId));
        Assert.Empty(byDepartment.Skipped);
        Assert.Equal([inOther.Id], byGradeAndBranch.Assigned.Select(entry => entry.EmployeeId));
    }

    // ---- Finalizing ----

    [IntegrationFact]
    public async Task Finalizing_writes_the_review_history_row_and_sets_the_reviewer_and_the_date()
    {
        var (org, category, period) = await SeedPlanAsync();
        var manager = await EmployeeAsync(org, "1", "maya");
        var employee = await EmployeeAsync(org, "2", "ria", manager.Id);
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 60m), ItemOf(second.Id, weight: 40m));
        CurrentEmployeeId = manager.Id;

        var final = Succeeded(await FinalizePlan(scorecard));

        Assert.Equal("Draft", final.Status);
        Assert.Equal(manager.Id, final.ReviewerEmployeeId);
        Assert.Equal(Today, final.PlanFinalizedOn);
        var entry = Assert.Single(final.History);
        Assert.Equal(("HR", "Finalize Plan", "Planning", "Draft", $"{Tag} Test User"), (entry.Step, entry.Action, entry.FromStatus, entry.ToStatus, entry.ActorName));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @s AND Step = 'HR' AND Action = 'Finalize Plan' AND FromStatus = 'Planning' AND ToStatus = 'Draft' AND ActorEmployeeId = @actor AND ActorUserId = @user AND Comment IS NULL AND OverallScore IS NULL",
            ("@s", scorecard.Id),
            ("@actor", manager.Id),
            ("@user", TestAdministrator.AccountId)));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @s AND Status = 'Draft' AND ReviewerEmployeeId = @r AND PlanFinalizedOn = '2026-11-09'",
            ("@s", scorecard.Id),
            ("@r", manager.Id)));
        Assert.Equal([$"KPI plan of {employee.EmployeeCode} for \"{period.Name}\" finalized"], await ActivityMessagesAsync("%plan of%finalized"));
    }

    [IntegrationFact]
    public async Task An_employee_without_a_manager_finalizes_with_no_reviewer_and_a_wrong_total_is_refused_without_a_history_row()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var other = await EmployeeAsync(org, "2", "rex");
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var short75 = await SeedScorecardAsync(other.Id, period.Id, ItemOf(first.Id, weight: 75m));
        var full = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 100m));

        var refused = await FinalizePlan(short75);
        var final = Succeeded(await FinalizePlan(full));

        Assert.Equal("The KPI weights must add up to 100%. They add up to 75.00%.", FailedOnField(refused, "weights"));
        Assert.Null(final.ReviewerEmployeeId);
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @s", ("@s", short75.Id)));
        Assert.Equal("Planning", await Scalar<string>("SELECT Status FROM kpi.Scorecard WHERE Id = @s", ("@s", short75.Id)));
    }

    [IntegrationFact]
    public async Task Two_finalize_calls_with_the_same_version_cannot_both_succeed()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(template.Id, weight: 100m));

        var first = await FinalizePlan(scorecard);
        var second = await FinalizePlan(scorecard);

        Assert.True(first.IsSuccess);
        Assert.Equal(RowVersionToken.Stale, Failed(second, ErrorType.Conflict));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @s", ("@s", scorecard.Id)));
    }

    [IntegrationFact]
    public async Task A_finalize_that_loses_the_race_to_another_session_is_stale_and_writes_no_history()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(template.Id, weight: 100m));
        BeforeNextSave = () => Sql.ExecuteAsync(
            "UPDATE kpi.Scorecard SET Status = 'Draft', PlanFinalizedOn = '2026-11-09' WHERE Id = @s;",
            ("@s", scorecard.Id));

        var raced = await FinalizePlan(scorecard);

        Assert.Equal(RowVersionToken.Stale, Failed(raced, ErrorType.Conflict));
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @s", ("@s", scorecard.Id)));
    }

    [IntegrationFact]
    public async Task A_finalized_plan_takes_no_more_changes_and_cannot_be_deleted()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 100m));
        var final = Succeeded(await FinalizePlan(scorecard));

        Failed(await Assign(employee.Id, period.Id, ItemOf(second.Id, weight: 10m)), ErrorType.Conflict);
        Failed(await UpdateItem(scorecard.Items[0].Id, UpdateOf(scorecard.Items[0], weight: 90m)), ErrorType.Conflict);
        Failed(await DeleteItem(scorecard.Items[0]), ErrorType.Conflict);
        Failed(await DeleteScorecard(final), ErrorType.Conflict);

        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @s AND Weight = 100", ("@s", scorecard.Id)));
    }

    // ---- Changing and removing KPIs ----

    [IntegrationFact]
    public async Task A_planned_kpi_is_updated_and_removed_with_its_row_version_and_a_racing_edit_is_stale()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 60m), ItemOf(second.Id, weight: 40m));
        var item = scorecard.Items[0];

        var updated = Succeeded(await UpdateItem(item.Id, UpdateOf(item, weight: 50m, target: 300m)));
        var stale = await UpdateItem(item.Id, UpdateOf(item, weight: 55m));
        var tooHeavy = await UpdateItem(item.Id, UpdateOf(updated, weight: 60.01m));
        BeforeNextSave = () => Sql.ExecuteAsync("UPDATE kpi.ScorecardItem SET SortOrder = 9 WHERE Id = @i", ("@i", item.Id));
        var raced = await UpdateItem(item.Id, UpdateOf(updated, weight: 45m));

        Assert.Equal((50m, 300m), (updated.Weight, updated.Target));
        Assert.Equal(RowVersionToken.Stale, Failed(stale, ErrorType.Conflict));
        FailedOnField(tooHeavy, "weight");
        Assert.Equal(RowVersionToken.Stale, Failed(raced, ErrorType.Conflict));
        Assert.Equal(50m, await Scalar<decimal>("SELECT Weight FROM kpi.ScorecardItem WHERE Id = @i", ("@i", item.Id)));

        var current = Succeeded(await GetScorecard(scorecard.Id)).Items.Single(other => other.Id == item.Id);
        Assert.True((await DeleteItem(current)).IsSuccess);
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @s", ("@s", scorecard.Id)));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Message LIKE @m", ("@m", $"%KPI \"{first.Code}\" removed from {employee.EmployeeCode}%")));
    }

    [IntegrationFact]
    public async Task A_scorecard_in_planning_is_deleted_with_its_kpis_and_one_with_history_is_refused()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var other = await EmployeeAsync(org, "2", "rex");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(template.Id, weight: 100m));
        var withHistory = await SeedScorecardAsync(other.Id, period.Id, ItemOf(template.Id, weight: 100m));
        await Sql.ExecuteAsync(
            "INSERT INTO kpi.ReviewAction (ScorecardId, Step, Action, FromStatus, ToStatus, ActorName) VALUES (@s, 'HR', 'Finalize Plan', 'Planning', 'Draft', @name);",
            ("@s", withHistory.Id),
            ("@name", Name("Hr Tester")));

        Failed(await DeleteScorecard(withHistory), ErrorType.Conflict);
        Assert.True((await DeleteScorecard(scorecard)).IsSuccess);

        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @s", ("@s", scorecard.Id)));
        Assert.Equal(0, await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @s", ("@s", scorecard.Id)));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @s", ("@s", withHistory.Id)));
        Failed(await Get<ScorecardDetailDto>(scorecard.Id), ErrorType.NotFound);
    }

    // ---- Delete protection ----

    [IntegrationFact]
    public async Task An_employee_with_a_scorecard_and_a_draft_period_with_scorecards_cannot_be_deleted()
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var period = await SeedPeriodAsync(org.Company.Id);
        var employee = await EmployeeAsync(org, "1", "ria");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        await SeedScorecardAsync(employee.Id, period.Id, ItemOf(template.Id));

        var deleteEmployee = await Delete<EmployeeDto>(employee.Id, Requests.Version(employee.RowVersion));
        var deletePeriod = await Delete<EvaluationPeriodDto>(period.Id, Requests.Version((await Get<EvaluationPeriodDto>(period.Id)).Value.RowVersion));

        Assert.Contains("KPI scorecard(s)", Failed(deleteEmployee, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("scorecard(s)", Failed(deletePeriod, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    // ---- Reading ----

    [IntegrationFact]
    public async Task The_list_filters_searches_sorts_and_pages_in_the_database()
    {
        var (org, category, period) = await SeedPlanAsync();
        var ria = await EmployeeAsync(org, "1", "ria");
        var rex = await EmployeeAsync(org, "2", "rex");
        var nora = await EmployeeAsync(org, "3", "nora");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        var rexCard = await SeedScorecardAsync(rex.Id, period.Id, ItemOf(template.Id, weight: 100m));
        await SeedScorecardAsync(ria.Id, period.Id, ItemOf(template.Id, weight: 60m));
        await SeedScorecardAsync(nora.Id, period.Id, ItemOf(template.Id, weight: 50m));
        Succeeded(await FinalizePlan(rexCard));
        var ofPeriod = new ScorecardFilter(PeriodId: period.Id);

        var all = Succeeded(await QueryScorecards(ofPeriod));
        var drafts = Succeeded(await QueryScorecards(ofPeriod with { Status = "Draft" }));
        var byCode = Succeeded(await QueryScorecards(ofPeriod, search: nora.EmployeeCode.ToLowerInvariant()));
        var byName = Succeeded(await QueryScorecards(ofPeriod, search: "testwell"));
        var byDepartment = Succeeded(await QueryScorecards(new ScorecardFilter(DepartmentId: org.Department.Id, CompanyId: org.Company.Id, EmployeeId: ria.Id)));
        var codeDesc = Succeeded(await QueryScorecards(ofPeriod, sort: "employeeCode", direction: "desc"));
        var paged = Succeeded(await QueryScorecards(ofPeriod, sort: "employeeCode", pageIndex: 1, pageSize: 2));

        Assert.Equal(3, all.Total);
        Assert.Equal([rex.Id], drafts.Items.Select(row => row.EmployeeId));
        Assert.Equal([nora.Id], byCode.Items.Select(row => row.EmployeeId));
        Assert.Equal(3, byName.Total);
        Assert.Equal([ria.Id], byDepartment.Items.Select(row => row.EmployeeId));
        Assert.Equal([nora.Id, rex.Id, ria.Id], codeDesc.Items.Select(row => row.EmployeeId));
        Assert.Equal((1, 3, 1), (paged.Items.Count, paged.Total, paged.PageIndex));
        var row = all.Items.Single(candidate => candidate.EmployeeId == rex.Id);
        Assert.Equal((rex.EmployeeCode, "Mia Testwell", period.Name, "Draft"), (row.EmployeeCode, row.EmployeeName, row.PeriodName, row.Status));
        Assert.Equal((1, 100m, org.Department.Name), (row.ItemCount, row.TotalWeight, row.DepartmentName));
        Assert.Equal(Today, row.PlanFinalizedOn);
        Assert.Null(row.ReviewerEmployeeId);
        Assert.Null(row.OverallScore);
    }

    [IntegrationFact]
    public async Task Every_sort_key_of_the_whitelist_is_translated_to_sql()
    {
        var (org, category, period) = await SeedPlanAsync();
        var manager = await EmployeeAsync(org, "1", "maya");
        var ria = await EmployeeAsync(org, "2", "ria", manager.Id);
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        var scorecard = await SeedScorecardAsync(ria.Id, period.Id, ItemOf(template.Id, weight: 100m));
        Succeeded(await FinalizePlan(scorecard));

        foreach (var key in new[] { "id", "employeeCode", "employeeName", "periodName", "departmentName", "status", "overallScore", "planFinalizedOn", "modifiedAt" })
        {
            foreach (var direction in new[] { "asc", "desc" })
            {
                var page = Succeeded(await QueryScorecards(new ScorecardFilter(PeriodId: period.Id), sort: key, direction: direction));
                var only = Assert.Single(page.Items);
                Assert.Equal(manager.Id, only.ReviewerEmployeeId);
                Assert.Equal("Mia Testwell", only.ReviewerName);
            }
        }

        FailedOnField(await QueryScorecards(sort: "rowVersion"), "sort");
    }

    [IntegrationFact]
    public async Task The_detail_has_the_items_by_sort_order_the_category_name_and_the_history()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var first = await SeedKpiTemplateAsync(category.Id, "S-01");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 50m, sortOrder: 5), ItemOf(second.Id, weight: 50m, sortOrder: 2));
        Succeeded(await FinalizePlan(scorecard));

        var detail = Succeeded(await GetScorecard(scorecard.Id));

        Assert.Equal([second.Code, first.Code], detail.Items.Select(item => item.KpiCode));
        Assert.All(detail.Items, item => Assert.Equal(category.Name, item.CategoryName));
        Assert.Equal(DateTimeKind.Utc, Assert.Single(detail.History).OccurredAtUtc.Kind);
        Failed(await GetScorecard(2_000_000_000), ErrorType.NotFound);
    }

    // ---- The temporal history of the scorecard ----

    [IntegrationFact]
    public async Task Finalizing_keeps_the_earlier_state_of_the_scorecard_in_its_system_versioned_history()
    {
        var (org, category, period) = await SeedPlanAsync();
        var employee = await EmployeeAsync(org, "1", "ria");
        var template = await SeedKpiTemplateAsync(category.Id, "S-01");
        var scorecard = await SeedScorecardAsync(employee.Id, period.Id, ItemOf(template.Id, weight: 100m));

        Succeeded(await FinalizePlan(scorecard));

        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard FOR SYSTEM_TIME ALL WHERE Id = @s AND Status = 'Planning'", ("@s", scorecard.Id)));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard FOR SYSTEM_TIME ALL WHERE Id = @s AND Status = 'Draft'", ("@s", scorecard.Id)));
    }

    // ---- The review history is append-only for the API login ----

    [IntegrationFact]
    public async Task The_database_denies_update_and_delete_on_the_review_history_to_the_api_login()
    {
        // The tests connect as the database owner, so they cannot try the delete as the API login. The deployed schema is checked
        // instead: the DENY that makes the trail append-only must be in the database the API runs against.
        var denied = await Scalar<int>(
            """
            SELECT COUNT(*) FROM sys.database_permissions p
            JOIN sys.database_principals g ON g.principal_id = p.grantee_principal_id
            WHERE g.name = N'hrms_api' AND p.state_desc = 'DENY' AND p.permission_name IN (N'UPDATE', N'DELETE')
              AND p.class_desc = 'OBJECT_OR_COLUMN' AND p.major_id = OBJECT_ID(N'kpi.ReviewAction');
            """);

        Assert.Equal(2, denied);
    }
}
