using Apex.Hrms.Application.Performance.Insights;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// The employee, manager and HR dashboards over the fixture of <see cref="KpiInsightFixtures"/>, whose figures are worked out by hand in
/// the comments: what each caller may see, every metric, and the choice of the period when none is given.
/// </summary>
public sealed class KpiDashboardTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private Task<Result<KpiEmployeeDashboardDto>> Employee(int? periodId = null) =>
        host.Get<IKpiEmployeeDashboardHandler>().HandleAsync(periodId, default);

    private Task<Result<KpiManagerDashboardDto>> Manager(int? periodId = null) =>
        host.Get<IKpiManagerDashboardHandler>().HandleAsync(periodId, default);

    private Task<Result<KpiHrDashboardDto>> Hr(int? periodId = null, int? companyId = null) =>
        host.Get<IKpiHrDashboardHandler>().HandleAsync(periodId, companyId, default);

    // ---- Employee ----

    [Fact]
    public async Task The_employee_dashboard_shows_the_callers_current_scorecard_items_feedback_and_history()
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, "Ben");

        var dashboard = ResultAssert.Succeeded(await Employee());

        Assert.Equal(world.Q4.Id, dashboard.Period!.Id);
        Assert.Equal("Active", dashboard.Period.Status);
        Assert.Equal(world.Card("Ben", world.Q4).Id, dashboard.Scorecard!.Id);
        Assert.Equal("Finalized", dashboard.Scorecard.Status);
        Assert.Equal(80m, dashboard.Scorecard.OverallScore);
        Assert.Equal("Meets Expectations", dashboard.Scorecard.RatingLabel);
        Assert.Equal("Anna Lead", dashboard.Scorecard.ReviewerName);

        // A-1: target 100, actual 80, weight 80 → 80% and 64 points; Q-1: target 50, actual 40, weight 20 → 80% and 16 points.
        Assert.Equal(["A-1", "Q-1"], dashboard.Items.Select(item => item.KpiCode));
        Assert.Equal([100m, 50m], dashboard.Items.Select(item => item.Target));
        Assert.Equal([80m, 40m], dashboard.Items.Select(item => item.ActualValue));
        Assert.Equal([80m, 80m], dashboard.Items.Select(item => item.AchievementPct));
        Assert.Equal([64m, 16m], dashboard.Items.Select(item => item.WeightedScore));

        Assert.False(dashboard.Pending.IsPending);
        Assert.Equal(new KpiManagerFeedbackDto(KpiInsightFixtures.ManagerRating, KpiInsightFixtures.ManagerNote), dashboard.Feedback);
        var history = Assert.Single(dashboard.History);
        Assert.Equal((world.Q3.Id, "Finalized", 70m, "Meets Expectations"), (history.PeriodId, history.Status, history.OverallScore, history.RatingLabel));
    }

    [Fact]
    public async Task The_employee_dashboard_never_shows_another_employees_scorecard()
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, "Eve");

        var dashboard = ResultAssert.Succeeded(await Employee(world.Q4.Id));

        Assert.Equal(world.Card("Eve", world.Q4).Id, dashboard.Scorecard!.Id);
        Assert.Equal("Rejected", dashboard.Scorecard.Status);
        Assert.Empty(dashboard.History);
    }

    [Fact]
    public async Task A_draft_scorecard_in_an_active_period_is_a_pending_submission_and_overdue_after_the_due_date()
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, "Finn");

        var before = ResultAssert.Succeeded(await Employee());
        host.Clock.Today = new DateOnly(2027, 1, 11);
        var after = ResultAssert.Succeeded(await Employee());

        Assert.Equal(new KpiPendingSubmissionDto(true, new DateOnly(2027, 1, 10), false), before.Pending);
        Assert.Equal(new KpiPendingSubmissionDto(true, new DateOnly(2027, 1, 10), true), after.Pending);
        Assert.Null(Assert.Single(before.Items).ActualValue);
    }

    /// <summary>Every one of these scorecards stored a manager's review draft (a return clears it); the employee reads it only once the decision is final.</summary>
    [Theory]
    [InlineData("Finn", "Draft", false)]
    [InlineData("Dan", "Submitted", false)]
    [InlineData("Jon", "Approved", false)]
    [InlineData("Ivy", "Returned", false)]
    [InlineData("Eve", "Rejected", true)]
    [InlineData("Ben", "Finalized", true)]
    public async Task The_feedback_is_the_managers_review_only_once_the_decision_is_final(string who, string status, bool shown)
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, who);

        var dashboard = ResultAssert.Succeeded(await Employee(world.Q4.Id));

        Assert.Equal(status, dashboard.Scorecard!.Status);
        Assert.Equal(
            shown ? new KpiManagerFeedbackDto(KpiInsightFixtures.ManagerRating, KpiInsightFixtures.ManagerNote) : new KpiManagerFeedbackDto(null, null),
            dashboard.Feedback);
        if (status is "Submitted" or "Approved")
        {
            Assert.Equal(KpiInsightFixtures.ManagerRating, world.Card(who, world.Q4).ManagerRating);
        }
    }

    [Fact]
    public async Task A_returned_scorecard_is_pending_too()
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, "Ivy");

        var dashboard = ResultAssert.Succeeded(await Employee());

        Assert.True(dashboard.Pending.IsPending);
        Assert.Null(dashboard.Scorecard!.OverallScore);
    }

    [Fact]
    public async Task Once_the_period_is_completed_a_returned_scorecard_is_still_pending_and_a_draft_one_is_not()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world, who: null);
        host.Permissions.Only("kpi-setup:Edit", "kpi:View", "kpi:Approve", "employees:Edit");
        ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(world.Q4));

        host.AsEmployee(world, "Ivy");
        var returned = ResultAssert.Succeeded(await Employee(world.Q4.Id));
        host.AsEmployee(world, "Finn");
        var draft = ResultAssert.Succeeded(await Employee(world.Q4.Id));

        Assert.Equal(("Returned", true), (returned.Scorecard!.Status, returned.Pending.IsPending));
        Assert.Equal(("Draft", false), (draft.Scorecard!.Status, draft.Pending.IsPending));
    }

    [Fact]
    public async Task A_caller_without_a_linked_employee_or_with_only_a_planning_scorecard_gets_an_empty_dashboard()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world, who: null);
        var administrator = ResultAssert.Succeeded(await Employee());
        host.AsEmployee(world, "Hana");
        var planning = ResultAssert.Succeeded(await Employee());

        foreach (var dashboard in new[] { administrator, planning })
        {
            Assert.Null(dashboard.Period);
            Assert.Null(dashboard.Scorecard);
            Assert.Empty(dashboard.Items);
            Assert.Empty(dashboard.History);
            Assert.False(dashboard.Pending.IsPending);
        }
    }

    [Fact]
    public async Task An_unknown_period_is_a_404()
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, "Ben");

        ResultAssert.Failed(await Employee(9999), ErrorType.NotFound);
    }

    // ---- The default period ----

    [Fact]
    public async Task Without_a_period_the_most_recent_active_period_wins_over_a_later_completed_one_and_else_the_most_recent()
    {
        var world = await host.SeedInsightsAsync();
        var november = await host.CreatePeriodAsync(world.Org.Company.Id, "2026-11", "Monthly", "2026-11-01", "2026-11-30", "2026-12-05");
        november = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(november));
        november = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(november));
        host.StoreScorecard(world, world.Id("Ben"), november, world.Org.Department.Id, world.Id("Anna"), ScorecardStatus.Finalized, 85m, "Meets Expectations");
        host.AsEmployee(world, "Ben");

        var whileActive = ResultAssert.Succeeded(await Employee());

        host.AsHr(world, who: null);
        var q4 = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<EvaluationPeriodDto>>().HandleAsync(world.Q4.Id, default));
        ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(q4));
        host.AsEmployee(world, "Ben");
        var afterwards = ResultAssert.Succeeded(await Employee());

        Assert.Equal(world.Q4.Id, whileActive.Period!.Id);
        Assert.Equal(november.Id, afterwards.Period!.Id);
        Assert.Equal(85m, afterwards.Scorecard!.OverallScore);
        Assert.Equal([world.Q4.Id, world.Q3.Id], afterwards.History.Select(entry => entry.PeriodId));
    }

    [Fact]
    public async Task The_hr_dashboard_without_a_period_picks_the_active_one_and_without_any_period_is_empty()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var chosen = ResultAssert.Succeeded(await Hr());
        var otherCompany = await host.CreateCompanyAsync("ZZZ", "Another Company");
        var empty = ResultAssert.Succeeded(await Hr(companyId: otherCompany.Id));

        Assert.Equal(world.Q4.Id, chosen.Period!.Id);
        Assert.Null(empty.Period);
        Assert.Equal(0, empty.EmployeesWithKpis);
        Assert.Equal(0m, empty.CompletionRatePct);
        Assert.Null(empty.AverageScore);
        Assert.Equal(70m, empty.AttentionThreshold);
    }

    // ---- Manager ----

    [Fact]
    public async Task The_manager_dashboard_covers_direct_reports_and_reviewed_scorecards_and_never_the_managers_own()
    {
        var world = await host.SeedInsightsAsync();
        host.AsManager(world, "Anna", "Ben", "Cara", "Dan");

        var dashboard = ResultAssert.Succeeded(await Manager());

        // Ben F80, Cara F90, Dan Submitted 55 (reports) and Gail F65 (Anna reviews her); not Anna's own F90, not Eve or Jon.
        Assert.Equal(world.Q4.Id, dashboard.Period!.Id);
        Assert.Equal(4, dashboard.TeamSize);
        Assert.Equal(["EMP-0002", "EMP-0003", "EMP-0004", "EMP-0007"], dashboard.Employees.Select(row => row.EmployeeCode));
        Assert.Equal([80m, 90m, 55m, 65m], dashboard.Employees.Select(row => row.OverallScore));
        Assert.Equal((4, false), (dashboard.EmployeeCount, dashboard.EmployeesTruncated));
        Assert.Equal(1, dashboard.PendingReviews);
        Assert.Equal(3, dashboard.ApprovedOrFinalized);
        Assert.Equal(78.33m, dashboard.AverageScore); // (80 + 90 + 65) / 3
        Assert.Equal(3, dashboard.ByStatus.Single(status => status.Status == "Finalized").Count);
        Assert.Equal(1, dashboard.ByStatus.Single(status => status.Status == "Submitted").Count);
        Assert.Equal(4, dashboard.ByStatus.Sum(status => status.Count));
    }

    [Fact]
    public async Task The_manager_dashboard_lists_at_most_200_employees_by_code_and_says_how_many_there_are()
    {
        var world = await host.SeedInsightsAsync();
        host.User.EmployeeId = null;
        host.User.UserId = 1;
        host.Permissions.Only("employees:View", "employees:Create", "employees:Edit", "kpi:View", "kpi:Create", "kpi:Approve");
        for (var number = 1001; number <= 1201; number++)
        {
            // 201 more people whose scorecards name Anna as their reviewer: 205 in her set with Ben, Cara, Dan and Gail.
            var person = await host.CreateStaffAsync(world.Org, $"EMP-{number}", first: "Many", last: "Test");
            host.StoreScorecard(world, person.Id, world.Q4, world.Org.Department.Id, world.Id("Anna"), ScorecardStatus.Draft, null, null);
        }

        host.AsManager(world, "Anna", "Ben", "Cara", "Dan");
        var dashboard = ResultAssert.Succeeded(await Manager());

        Assert.Equal(205, dashboard.TeamSize);
        Assert.Equal(205, dashboard.EmployeeCount);
        Assert.True(dashboard.EmployeesTruncated);
        Assert.Equal(KpiFigures.ManagerEmployeeLimit, dashboard.Employees.Count);
        Assert.Equal(["EMP-0002", "EMP-0003", "EMP-0004", "EMP-0007", "EMP-1001"], dashboard.Employees.Take(5).Select(row => row.EmployeeCode));
        Assert.Equal("EMP-1196", dashboard.Employees[^1].EmployeeCode);
    }

    [Fact]
    public async Task The_manager_dashboard_of_an_earlier_period_and_of_a_manager_without_a_team()
    {
        var world = await host.SeedInsightsAsync();
        host.AsManager(world, "Anna", "Ben", "Cara", "Dan");
        var q3 = ResultAssert.Succeeded(await Manager(world.Q3.Id));
        host.AsManager(world, "Jon");
        var alone = ResultAssert.Succeeded(await Manager());

        Assert.Equal(3, q3.TeamSize);
        Assert.Equal(66.67m, q3.AverageScore); // (70 + 70 + 60) / 3
        Assert.Equal(0, q3.PendingReviews);
        Assert.Null(alone.Period);
        Assert.Equal(0, alone.TeamSize);
        Assert.Empty(alone.Employees);
        Assert.Equal((0, false), (alone.EmployeeCount, alone.EmployeesTruncated));
        Assert.Equal((3, false), (q3.EmployeeCount, q3.EmployeesTruncated));
    }

    // ---- HR ----

    [Fact]
    public async Task The_hr_dashboard_figures_over_every_scorecard_of_the_period()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var dashboard = ResultAssert.Succeeded(await Hr(world.Q4.Id));

        Assert.Equal(9, dashboard.EmployeesWithKpis);       // all but Hana (Planning, no KPI)
        Assert.Equal(10, dashboard.TotalAssignments);       // one KPI each, Ben two
        Assert.Equal(4, dashboard.CompletedEvaluations);    // Anna, Ben, Cara, Gail
        Assert.Equal(2, dashboard.PendingSubmissions);      // Finn Draft, Ivy Returned
        Assert.Equal(2, dashboard.PendingApprovals);        // Dan Submitted, Jon Approved
        Assert.Equal(81.25m, dashboard.AverageScore);       // (90 + 80 + 90 + 65) / 4: not Eve's Rejected 40, not Jon's Approved 70
        Assert.Equal(44.44m, dashboard.CompletionRatePct);  // 4 / 9 scorecards past Planning
        Assert.Equal([1, 1, 1, 0, 1, 0, 1, 1, 4], dashboard.ByStatus.Select(status => status.Count));

        var eng = dashboard.Departments.Single(row => row.DepartmentId == world.Org.Department.Id);
        var ops = dashboard.Departments.Single(row => row.DepartmentId == world.Ops.Id);
        Assert.Equal((4, 3, 86.67m), (eng.Scorecards, eng.Finalized, eng.AverageScore.GetValueOrDefault()));
        Assert.Equal((6, 1, 65m), (ops.Scorecards, ops.Finalized, ops.AverageScore.GetValueOrDefault()));
        Assert.Equal(world.Ops.Name, ops.DepartmentName);

        Assert.Equal(["EMP-0001", "EMP-0003", "EMP-0002", "EMP-0007"], dashboard.TopPerformers.Select(row => row.EmployeeCode));
        Assert.Equal(70m, dashboard.AttentionThreshold);    // lowest active band at or above 60 (the inactive 62 is ignored)
        Assert.Equal(1, dashboard.RequiringAttentionCount);
        Assert.Equal("EMP-0007", Assert.Single(dashboard.RequiringAttention).EmployeeCode);
        Assert.Equal(
            [("Exceeds", 2), ("Meets Expectations", 1), ("Needs Improvement", 1)],
            dashboard.RatingDistribution.Select(row => (row.RatingLabel, row.Count)));
    }

    [Fact]
    public async Task The_hr_dashboard_shows_at_most_five_top_performers_by_score_then_code()
    {
        var world = await host.SeedInsightsAsync();
        foreach (var who in new[] { "Eve", "Finn", "Ivy" })
        {
            host.StoreScorecard(world, world.Id(who), world.Q3, world.Ops.Id, null, ScorecardStatus.Finalized, 70m, "Meets Expectations");
        }

        host.AsHr(world);
        var dashboard = ResultAssert.Succeeded(await Hr(world.Q3.Id));

        // Q3: Ben 70, Cara 70, Eve 70, Finn 70, Ivy 70, Dan 60.
        Assert.Equal(["EMP-0002", "EMP-0003", "EMP-0005", "EMP-0006", "EMP-0009"], dashboard.TopPerformers.Select(row => row.EmployeeCode));
        Assert.Equal(100m, dashboard.CompletionRatePct);
        Assert.Equal("EMP-0004", Assert.Single(dashboard.RequiringAttention).EmployeeCode);
    }

    [Fact]
    public async Task The_attention_threshold_falls_back_to_sixty_without_a_band_at_or_above_sixty()
    {
        var world = await host.SeedInsightsAsync();
        foreach (var band in host.Repository<RatingBand>().Items.Where(band => band.IsActive && band.MinScore >= 60m).ToList())
        {
            Assert.True(band.Deactivate().IsSuccess);
        }

        host.AsHr(world);
        var dashboard = ResultAssert.Succeeded(await Hr(world.Q3.Id));

        Assert.Equal(60m, dashboard.AttentionThreshold);
        Assert.Equal(0, dashboard.RequiringAttentionCount); // Dan's 60 is not below 60
    }

    [Fact]
    public async Task The_hr_dashboard_refuses_a_caller_without_hr_rights()
    {
        var world = await host.SeedInsightsAsync();
        host.AsManager(world, "Anna", "Ben", "Cara", "Dan");

        var refused = ResultAssert.Failed(await Hr(), ErrorType.Forbidden);

        Assert.Equal("The HR dashboard needs HR rights.", refused.Message);
    }

    [Fact]
    public async Task The_hr_dashboard_of_one_company_counts_only_its_scorecards()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var own = ResultAssert.Succeeded(await Hr(world.Q4.Id, world.Org.Company.Id));
        var other = ResultAssert.Succeeded(await Hr(world.Q4.Id, world.Org.Company.Id + 100));

        Assert.Equal(9, own.EmployeesWithKpis);
        Assert.Equal(0, other.EmployeesWithKpis);
        Assert.Null(other.AverageScore);
        Assert.Equal(0m, other.CompletionRatePct);
    }
}
