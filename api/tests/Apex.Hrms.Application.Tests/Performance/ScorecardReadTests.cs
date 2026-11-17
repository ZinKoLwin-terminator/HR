using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using static Apex.Hrms.Application.Tests.Fakes.ScorecardFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>Reading scorecards: the list and its filters, search, sort and paging, the detail, and the row-level scope (own, team, all).</summary>
public sealed class ScorecardReadTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    /// <summary>Anna (the manager), Ben and Cara (Anna's reports) and Dan (nobody's report) each with a scorecard in the Active period.</summary>
    private async Task<Team> SeedTeamAsync()
    {
        var plan = await host.SeedPlanAsync();
        var anna = await host.CreateStaffAsync(plan.Org, "EMP-0001", first: "Anna", last: "Lead");
        var ben = await host.CreateStaffAsync(plan.Org, "EMP-0002", anna.Id, "Ben", "Report");
        var cara = await host.CreateStaffAsync(plan.Org, "EMP-0003", anna.Id, "Cara", "Report");
        var dan = await host.CreateStaffAsync(plan.Org, "EMP-0004", first: "Dan", last: "Other");
        var template = (await host.CreateTemplateAsync(plan.Category.Id, "A-1")).Id;
        var cards = new Dictionary<int, ScorecardDetailDto>();
        foreach (var employee in new[] { anna, ben, cara, dan })
        {
            cards[employee.Id] = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(template, weight: 100m));
        }

        return new Team(plan, anna.Id, ben.Id, cara.Id, dan.Id, cards);
    }

    private sealed record Team(PlanSeed Plan, int Anna, int Ben, int Cara, int Dan, Dictionary<int, ScorecardDetailDto> Cards);

    private async Task<int[]> VisibleEmployeesAsync() =>
        [.. ResultAssert.Succeeded(await host.QueryScorecards()).Items.Select(card => card.EmployeeId).Order()];

    // ---- Scope ----

    [Fact]
    public async Task A_caller_with_hr_rights_sees_every_scorecard()
    {
        var team = await SeedTeamAsync();
        host.User.EmployeeId = team.Dan;

        Assert.Equal([team.Anna, team.Ben, team.Cara, team.Dan], await VisibleEmployeesAsync());
        Assert.True((await host.GetScorecard(team.Cards[team.Ben].Id)).IsSuccess);
    }

    [Fact]
    public async Task A_caller_with_no_linked_employee_but_hr_rights_sees_every_scorecard()
    {
        await SeedTeamAsync();
        host.User.EmployeeId = null;

        Assert.Equal(4, (await VisibleEmployeesAsync()).Length);
    }

    [Fact]
    public async Task A_manager_sees_their_own_scorecard_and_their_direct_reports_and_no_other()
    {
        var team = await SeedTeamAsync();
        host.User.EmployeeId = team.Anna;
        host.Permissions.Only("kpi:View", "kpi:Approve");
        host.Team.Set([team.Ben, team.Cara]);

        Assert.Equal([team.Anna, team.Ben, team.Cara], await VisibleEmployeesAsync());
        Assert.True((await host.GetScorecard(team.Cards[team.Ben].Id)).IsSuccess);
        Assert.True((await host.GetScorecard(team.Cards[team.Anna].Id)).IsSuccess);
        ResultAssert.Failed(await host.GetScorecard(team.Cards[team.Dan].Id), ErrorType.NotFound);
    }

    [Fact]
    public async Task An_employee_sees_only_their_own_scorecard_and_another_employees_is_a_404()
    {
        var team = await SeedTeamAsync();
        host.User.EmployeeId = team.Ben;
        host.Permissions.Only("kpi:View", "kpi:Create");
        host.Team.Set([]);

        Assert.Equal([team.Ben], await VisibleEmployeesAsync());
        Assert.True((await host.GetScorecard(team.Cards[team.Ben].Id)).IsSuccess);
        ResultAssert.Failed(await host.GetScorecard(team.Cards[team.Anna].Id), ErrorType.NotFound);
        ResultAssert.Failed(await host.GetScorecard(team.Cards[team.Cara].Id), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_manager_without_a_team_sees_only_themselves_and_an_unlinked_employee_role_sees_nothing()
    {
        var team = await SeedTeamAsync();
        host.Permissions.Only("kpi:View", "kpi:Approve");
        host.User.EmployeeId = team.Dan;
        host.Team.Set([]);
        var alone = await VisibleEmployeesAsync();
        host.User.EmployeeId = null;
        var nobody = await VisibleEmployeesAsync();

        Assert.Equal([team.Dan], alone);
        Assert.Empty(nobody);
    }

    [Fact]
    public async Task Approve_with_employee_edit_is_the_hr_tier_and_sees_everyone()
    {
        var team = await SeedTeamAsync();
        host.User.EmployeeId = team.Dan;
        host.Permissions.Only("kpi:View", "kpi:Approve", "employees:Edit");
        host.Team.Set([]);

        Assert.Equal(4, (await VisibleEmployeesAsync()).Length);
    }

    [Fact]
    public async Task The_scope_applies_to_filters_search_and_totals_too()
    {
        var team = await SeedTeamAsync();
        host.User.EmployeeId = team.Ben;
        host.Permissions.Only("kpi:View");

        var all = ResultAssert.Succeeded(await host.QueryScorecards(search: "Report"));
        var other = ResultAssert.Succeeded(await host.QueryScorecards(new ScorecardFilter(EmployeeId: team.Dan)));

        Assert.Equal(1, all.Total);
        Assert.Equal(0, other.Total);
        Assert.Empty(other.Items);
    }

    // ---- The list ----

    [Fact]
    public async Task A_list_row_has_the_documented_fields_and_nothing_personal_beyond_code_and_name()
    {
        var plan = await host.SeedPlanAsync();
        var manager = await host.CreateStaffAsync(plan.Org, "EMP-0010", first: "Hana", last: "Boss");
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001", manager.Id);
        var first = (await host.CreateTemplateAsync(plan.Category.Id, "A-1")).Id;
        var second = (await host.CreateTemplateAsync(plan.Category.Id, "A-2")).Id;
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(first, weight: 60m), Item(second, weight: 40m));
        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        var row = Assert.Single(ResultAssert.Succeeded(await host.QueryScorecards(new ScorecardFilter(EmployeeId: employee.Id))).Items);

        Assert.Equal(("EMP-0001", "Mia Hartley", "2026 Q4", "Draft"), (row.EmployeeCode, row.EmployeeName, row.PeriodName, row.Status));
        Assert.Equal((2, 100m), (row.ItemCount, row.TotalWeight));
        Assert.Equal((manager.Id, "Hana Boss"), (row.ReviewerEmployeeId, row.ReviewerName));
        Assert.Equal(new DateOnly(2026, 11, 9), row.PlanFinalizedOn);
        Assert.Null(row.OverallScore);
        Assert.Null(row.RatingLabel);
        Assert.Null(row.SubmittedOn);
        Assert.Null(row.FinalizedOn);
        Assert.Equal(new DateOnly(2027, 1, 10), row.SubmissionDueDate);
        Assert.Equal(plan.Org.Department.Id, row.DepartmentId);
        Assert.NotEmpty(row.DepartmentName);
        Assert.NotEmpty(row.RowVersion);
    }

    [Fact]
    public async Task The_list_filters_by_period_employee_department_company_and_status()
    {
        var team = await SeedTeamAsync();
        ResultAssert.Succeeded(await host.FinalizePlan(team.Cards[team.Ben]));
        var org = team.Plan.Org;

        async Task<int> CountOf(ScorecardFilter filter) => ResultAssert.Succeeded(await host.QueryScorecards(filter)).Total;

        Assert.Equal(4, await CountOf(new ScorecardFilter(PeriodId: team.Plan.Period.Id)));
        Assert.Equal(0, await CountOf(new ScorecardFilter(PeriodId: 999)));
        Assert.Equal(1, await CountOf(new ScorecardFilter(EmployeeId: team.Cara)));
        Assert.Equal(4, await CountOf(new ScorecardFilter(DepartmentId: org.Department.Id)));
        Assert.Equal(0, await CountOf(new ScorecardFilter(DepartmentId: 999)));
        Assert.Equal(4, await CountOf(new ScorecardFilter(CompanyId: org.Company.Id)));
        Assert.Equal(1, await CountOf(new ScorecardFilter(Status: "Draft")));
        Assert.Equal(3, await CountOf(new ScorecardFilter(Status: "Planning")));
        Assert.Equal(4, await CountOf(new ScorecardFilter(Status: "all")));
        Assert.Equal(0, await CountOf(new ScorecardFilter(Status: "Nonsense")));
        Assert.Equal(0, await CountOf(new ScorecardFilter(Status: "Under Review")));
    }

    [Fact]
    public async Task Search_matches_the_employee_code_or_name_and_is_limited_to_100_characters()
    {
        var team = await SeedTeamAsync();

        var byCode = ResultAssert.Succeeded(await host.QueryScorecards(search: "EMP-0003"));
        var byName = ResultAssert.Succeeded(await host.QueryScorecards(search: "Cara"));
        var byFullName = ResultAssert.Succeeded(await host.QueryScorecards(search: "Dan Other"));
        var tooLong = await host.QueryScorecards(search: new string('x', 101));

        Assert.Equal([team.Cara], byCode.Items.Select(row => row.EmployeeId));
        Assert.Equal([team.Cara], byName.Items.Select(row => row.EmployeeId));
        Assert.Equal([team.Dan], byFullName.Items.Select(row => row.EmployeeId));
        ResultAssert.FailedOnField(tooLong, "search");
    }

    [Fact]
    public async Task The_list_sorts_by_the_whitelist_and_an_unknown_field_is_a_400_on_sort()
    {
        var team = await SeedTeamAsync();

        var byCodeDesc = ResultAssert.Succeeded(await host.QueryScorecards(sort: "employeeCode", direction: "desc"));
        var byName = ResultAssert.Succeeded(await host.QueryScorecards(sort: "employeeName"));
        var byStatus = ResultAssert.Succeeded(await host.QueryScorecards(sort: "status", direction: "desc"));
        var byPeriod = ResultAssert.Succeeded(await host.QueryScorecards(sort: "periodName"));
        var byScore = ResultAssert.Succeeded(await host.QueryScorecards(sort: "overallScore"));
        var byModified = ResultAssert.Succeeded(await host.QueryScorecards(sort: "modifiedAt"));
        var byFinalized = ResultAssert.Succeeded(await host.QueryScorecards(sort: "planFinalizedOn"));
        var bad = await host.QueryScorecards(sort: "rowVersion");

        Assert.Equal(["EMP-0004", "EMP-0003", "EMP-0002", "EMP-0001"], byCodeDesc.Items.Select(row => row.EmployeeCode));
        Assert.Equal([team.Anna, team.Ben, team.Cara, team.Dan], byName.Items.Select(row => row.EmployeeId));
        Assert.Equal(4, byStatus.Total);
        Assert.Equal(4, byPeriod.Total);
        Assert.Equal(4, byScore.Total);
        Assert.Equal(4, byModified.Total);
        Assert.Equal(4, byFinalized.Total);
        ResultAssert.FailedOnField(bad, "sort");
    }

    [Fact]
    public async Task The_list_pages_on_the_server_and_clamps_the_page()
    {
        await SeedTeamAsync();

        var first = ResultAssert.Succeeded(await host.QueryScorecards(pageSize: 3));
        var second = ResultAssert.Succeeded(await host.QueryScorecards(pageIndex: 1, pageSize: 3));
        var beyond = ResultAssert.Succeeded(await host.QueryScorecards(pageIndex: 9, pageSize: 3));
        var huge = ResultAssert.Succeeded(await host.QueryScorecards(pageSize: 5000));

        Assert.Equal((3, 4, 0), (first.Items.Count, first.Total, first.PageIndex));
        Assert.Equal((1, 1), (second.Items.Count, second.PageIndex));
        Assert.Equal(1, beyond.PageIndex);
        Assert.Equal(Paging.MaxPageSize, huge.PageSize);
    }

    // ---- The detail ----

    [Fact]
    public async Task The_detail_has_the_items_by_sort_order_and_the_history_oldest_first()
    {
        var plan = await host.SeedPlanAsync();
        var employee = await host.CreateStaffAsync(plan.Org, "EMP-0001");
        var first = (await host.CreateTemplateAsync(plan.Category.Id, "A-1")).Id;
        var second = (await host.CreateTemplateAsync(plan.Category.Id, "A-2")).Id;
        var scorecard = await host.AssignedAsync(employee.Id, plan.Period.Id, Item(first, weight: 50m, sortOrder: 5), Item(second, weight: 50m, sortOrder: 2));
        ResultAssert.Succeeded(await host.FinalizePlan(scorecard));

        var detail = ResultAssert.Succeeded(await host.GetScorecard(scorecard.Id));

        Assert.Equal(["A-2", "A-1"], detail.Items.Select(item => item.KpiCode));
        var entry = Assert.Single(detail.History);
        Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind);
        Assert.Equal("Draft", detail.Status);
    }

    [Fact]
    public async Task A_missing_scorecard_is_a_404()
    {
        await SeedTeamAsync();

        ResultAssert.Failed(await host.GetScorecard(9999), ErrorType.NotFound);
    }
}
