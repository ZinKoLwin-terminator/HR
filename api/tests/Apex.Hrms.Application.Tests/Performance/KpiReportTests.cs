using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Performance.Insights;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// The KPI reports over the fixture of <see cref="KpiInsightFixtures"/>: the figures, the ranking with ties, the scope of the caller,
/// the filters and the sort whitelist.
/// </summary>
public sealed class KpiReportTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static PageRequest<KpiReportFilter> Page(KpiReportFilter? filter = null, int pageIndex = 0, int pageSize = 50, string? sort = null, string direction = "asc", string? search = null) =>
        new(pageIndex, pageSize, sort is null ? null : new SortState(sort, direction), search, filter ?? new KpiReportFilter());

    private async Task<PagedResult<KpiTargetVsActualRowDto>> TargetVsActualAsync(KpiReportFilter? filter = null, string? sort = null, string direction = "asc", string? search = null) =>
        ResultAssert.Succeeded(await host.Get<IKpiTargetVsActualReportHandler>().HandleAsync(Page(filter, sort: sort, direction: direction, search: search), default));

    private async Task<PagedResult<KpiRankingRowDto>> RankingAsync(KpiReportFilter? filter = null, int pageIndex = 0, int pageSize = 50) =>
        ResultAssert.Succeeded(await host.Get<IKpiRankingReportHandler>().HandleAsync(Page(filter, pageIndex, pageSize), default));

    // ---- Summary ----

    [Fact]
    public async Task The_summary_has_one_row_per_period_newest_first_with_the_figures_of_finalized_scorecards()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var summary = ResultAssert.Succeeded(await host.Get<IKpiSummaryReportHandler>().HandleAsync(Page(), default));

        Assert.Equal(2, summary.Total);
        var q4 = summary.Items[0];
        var q3 = summary.Items[1];
        Assert.Equal((world.Q4.Id, "Quarterly", "Active"), (q4.PeriodId, q4.PeriodType, q4.PeriodStatus));
        Assert.Equal((10, 1, 4, 1), (q4.Scorecards, q4.InPlanning, q4.Finalized, q4.Rejected));
        Assert.Equal((81.25m, 65m, 90m, 44.44m), (q4.AverageScore.GetValueOrDefault(), q4.MinScore.GetValueOrDefault(), q4.MaxScore.GetValueOrDefault(), q4.CompletionRatePct));
        Assert.Equal((3, 0, 3, 0), (q3.Scorecards, q3.InPlanning, q3.Finalized, q3.Rejected));
        Assert.Equal((66.67m, 60m, 70m, 100m), (q3.AverageScore.GetValueOrDefault(), q3.MinScore.GetValueOrDefault(), q3.MaxScore.GetValueOrDefault(), q3.CompletionRatePct));
    }

    [Fact]
    public async Task A_period_with_nothing_past_planning_has_a_zero_completion_rate_and_no_average()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var summary = ResultAssert.Succeeded(await host.Get<IKpiSummaryReportHandler>().HandleAsync(Page(new KpiReportFilter(Status: "Planning")), default));

        var q4 = Assert.Single(summary.Items);
        Assert.Equal((1, 1, 0), (q4.Scorecards, q4.InPlanning, q4.Finalized));
        Assert.Equal(0m, q4.CompletionRatePct);
        Assert.Null(q4.AverageScore);
        Assert.Null(q4.MinScore);
    }

    [Fact]
    public async Task A_period_of_the_page_whose_figures_are_gone_reads_as_zero_figures_instead_of_failing()
    {
        // The page and the figures are two queries; here the figures query finds nothing (the scorecards were deleted in between).
        using var racing = new TestHost(services => services.AddSingleton<IReadModel>(sp => new FiguresLostReadModel(new FakeReadModel(sp))));
        var world = await racing.SeedInsightsAsync();
        racing.AsHr(world);

        var summary = ResultAssert.Succeeded(await racing.Get<IKpiSummaryReportHandler>().HandleAsync(Page(), default));

        Assert.Equal(2, summary.Total);
        Assert.All(summary.Items, row =>
        {
            Assert.Equal((0, 0, 0, 0), (row.Scorecards, row.InPlanning, row.Finalized, row.Rejected));
            Assert.Equal((null, null, null, 0m), (row.AverageScore, row.MinScore, row.MaxScore, row.CompletionRatePct));
        });
    }

    /// <summary>Answers every query as the fake does, except the summary's figures query (rows with a <c>ScoreSum</c>), which finds nothing.</summary>
    private sealed class FiguresLostReadModel(IReadModel inner) : IReadModel
    {
        public IQueryable<TEntity> Query<TEntity>()
            where TEntity : Entity => inner.Query<TEntity>();

        public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
            typeof(T).GetProperty("ScoreSum") is not null && typeof(T).GetProperty("PeriodId") is not null
                ? Task.FromResult(new List<T>())
                : inner.ToListAsync(query, cancellationToken);

        public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => inner.CountAsync(query, cancellationToken);

        public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => inner.FirstOrDefaultAsync(query, cancellationToken);
    }

    [Fact]
    public async Task The_period_type_and_year_filters_make_the_monthly_quarterly_and_annual_reports()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var handler = host.Get<IKpiSummaryReportHandler>();

        var quarterly = ResultAssert.Succeeded(await handler.HandleAsync(Page(new KpiReportFilter(PeriodType: "Quarterly", Year: 2026)), default));
        var annual = ResultAssert.Succeeded(await handler.HandleAsync(Page(new KpiReportFilter(PeriodType: "Annual")), default));
        var lastYear = ResultAssert.Succeeded(await handler.HandleAsync(Page(new KpiReportFilter(Year: 2025)), default));
        var unknown = await handler.HandleAsync(Page(new KpiReportFilter(PeriodType: "Weekly")), default);
        var badYear = await handler.HandleAsync(Page(new KpiReportFilter(Year: 0)), default);

        Assert.Equal(2, quarterly.Total);
        Assert.Equal(0, annual.Total);
        Assert.Equal(0, lastYear.Total);
        ResultAssert.FailedOnField(unknown, "periodType");
        ResultAssert.FailedOnField(badYear, "year");
    }

    // ---- Ranking ----

    [Fact]
    public async Task The_ranking_lists_finalized_scorecards_only_and_equal_scores_share_a_rank_ordered_by_employee_code()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var q4 = await RankingAsync(new KpiReportFilter(PeriodId: world.Q4.Id));
        var both = await RankingAsync();

        // Q4: Anna 90, Cara 90, Ben 80, Gail 65 (Dan Submitted, Eve Rejected, Jon Approved are not ranked).
        Assert.Equal(["EMP-0001", "EMP-0003", "EMP-0002", "EMP-0007"], q4.Items.Select(row => row.EmployeeCode));
        Assert.Equal([1, 1, 3, 4], q4.Items.Select(row => row.Rank));
        // Both periods: 90, 90, 80, 70 (Ben Q3), 70 (Cara Q3), 65, 60.
        Assert.Equal([1, 1, 3, 4, 4, 6, 7], both.Items.Select(row => row.Rank));
        Assert.Equal(["EMP-0002", "EMP-0003"], both.Items.Where(row => row.Rank == 4).Select(row => row.EmployeeCode));
    }

    [Fact]
    public async Task A_ranking_page_that_starts_inside_a_tie_keeps_the_shared_rank()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var filter = new KpiReportFilter(PeriodId: world.Q4.Id);

        var second = await RankingAsync(filter, pageIndex: 1, pageSize: 1);
        var third = await RankingAsync(filter, pageIndex: 1, pageSize: 2);

        Assert.Equal(("EMP-0003", 1), (second.Items[0].EmployeeCode, second.Items[0].Rank));
        Assert.Equal(4, second.Total);
        Assert.Equal([3, 4], third.Items.Select(row => row.Rank));
    }

    [Fact]
    public async Task The_ranking_refuses_any_other_order()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var handler = host.Get<IKpiRankingReportHandler>();

        ResultAssert.FailedOnField(await handler.HandleAsync(Page(sort: "employeeCode"), default), "sort");
        ResultAssert.FailedOnField(await handler.HandleAsync(Page(sort: "rank", direction: "desc"), default), "sort");
        Assert.True((await handler.HandleAsync(Page(sort: "rank"), default)).IsSuccess);
    }

    [Fact]
    public async Task An_employee_ranks_only_their_own_scorecards()
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, "Ben");

        var ranking = await RankingAsync();

        Assert.All(ranking.Items, row => Assert.Equal("EMP-0002", row.EmployeeCode));
        Assert.Equal([1, 2], ranking.Items.Select(row => row.Rank));
    }

    // ---- Department ----

    [Fact]
    public async Task The_department_report_uses_the_department_copied_onto_the_scorecard()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var handler = host.Get<IKpiDepartmentReportHandler>();

        // Gail's employee record is in ENG, but her Q4 scorecard was made while she was in OPS.
        var ops = ResultAssert.Succeeded(await handler.HandleAsync(world.Ops.Id, Page(), default));
        var eng = ResultAssert.Succeeded(await handler.HandleAsync(world.Org.Department.Id, Page(new KpiReportFilter(PeriodId: world.Q4.Id)), default));

        Assert.Equal(world.Q4.Id, ops.Period!.Id);
        Assert.Equal(["EMP-0005", "EMP-0006", "EMP-0007", "EMP-0008", "EMP-0009", "EMP-0010"], ops.Rows.Items.Select(row => row.EmployeeCode));
        Assert.Equal((6, 1, 65m), (ops.Scorecards, ops.Finalized, ops.AverageScore.GetValueOrDefault()));
        Assert.Equal(["EMP-0001", "EMP-0002", "EMP-0003", "EMP-0004"], eng.Rows.Items.Select(row => row.EmployeeCode));
        Assert.Equal((4, 3, 86.67m), (eng.Scorecards, eng.Finalized, eng.AverageScore.GetValueOrDefault()));
        Assert.Contains(ops.Rows.Items, row => row.Status == "Rejected");
    }

    [Fact]
    public async Task The_department_report_sorts_by_the_whitelist_and_an_unknown_department_is_a_404()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var handler = host.Get<IKpiDepartmentReportHandler>();

        var byScore = ResultAssert.Succeeded(await handler.HandleAsync(world.Org.Department.Id, Page(sort: "overallScore", direction: "desc"), default));

        Assert.Equal([90m, 90m, 80m, 55m], byScore.Rows.Items.Select(row => row.OverallScore));
        ResultAssert.FailedOnField(await handler.HandleAsync(world.Org.Department.Id, Page(sort: "salary"), default), "sort");
        ResultAssert.Failed(await handler.HandleAsync(9999, Page(), default), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_department_the_caller_sees_no_scorecard_of_is_the_same_404_as_an_unknown_one_unless_they_may_read_departments()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var empty = await host.CreateDepartmentAsync(world.Org.Company.Id, world.Org.Branch.Id, "FIN");
        var handler = host.Get<IKpiDepartmentReportHandler>();
        var opsMessage = $"Department #{world.Ops.Id} was not found.";

        var hrEmpty = await handler.HandleAsync(empty.Id, Page(), default);

        host.AsEmployee(world, "Ben");
        var benEng = await handler.HandleAsync(world.Org.Department.Id, Page(), default);
        var benOps = ResultAssert.Failed(await handler.HandleAsync(world.Ops.Id, Page(), default), ErrorType.NotFound);
        var benUnknown = ResultAssert.Failed(await handler.HandleAsync(9999, Page(), default), ErrorType.NotFound);

        // Hana's only scorecard (in OPS) is still in Planning, which an employee does not see.
        host.AsEmployee(world, "Hana");
        var hanaOps = ResultAssert.Failed(await handler.HandleAsync(world.Ops.Id, Page(), default), ErrorType.NotFound);

        // Anna reviews Gail's OPS scorecard.
        host.AsManager(world, "Anna", "Ben", "Cara", "Dan");
        var annaOps = await handler.HandleAsync(world.Ops.Id, Page(), default);
        var annaEmpty = ResultAssert.Failed(await handler.HandleAsync(empty.Id, Page(), default), ErrorType.NotFound);

        host.AsEmployee(world, "Ben");
        host.Permissions.Only("kpi:View", "kpi:Create", "organization:View");
        var benWithOrganization = ResultAssert.Succeeded(await handler.HandleAsync(world.Ops.Id, Page(), default));

        Assert.True(hrEmpty.IsSuccess);
        Assert.True(benEng.IsSuccess);
        Assert.Equal(opsMessage, benOps.Message);
        Assert.Equal("Department #9999 was not found.", benUnknown.Message);
        Assert.Equal(opsMessage, hanaOps.Message);
        Assert.True(annaOps.IsSuccess);
        Assert.Equal(["EMP-0007"], annaOps.Value.Rows.Items.Select(row => row.EmployeeCode));
        Assert.Equal($"Department #{empty.Id} was not found.", annaEmpty.Message);
        Assert.Equal((world.Ops.Id, 0), (benWithOrganization.DepartmentId, benWithOrganization.Rows.Total));
    }

    // ---- Employee ----

    [Fact]
    public async Task The_employee_report_lists_the_history_newest_first_with_the_kpis()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var report = ResultAssert.Succeeded(await host.Get<IKpiEmployeeReportHandler>().HandleAsync(world.Id("Ben"), Page(), default));

        Assert.Equal(("EMP-0002", "Ben Test"), (report.EmployeeCode, report.EmployeeName));
        Assert.Equal((2, 75m), (report.Finalized, report.AverageScore.GetValueOrDefault())); // (80 + 70) / 2
        Assert.Equal([world.Q4.Id, world.Q3.Id], report.Scorecards.Items.Select(card => card.PeriodId));
        var q4 = report.Scorecards.Items[0];
        Assert.Equal(["A-1", "Q-1"], q4.Items.Select(item => item.KpiCode));
        Assert.Equal(["Productivity", "Quality"], q4.Items.Select(item => item.CategoryName));
        Assert.Equal([80m, 80m], q4.Items.Select(item => item.AchievementPct));
    }

    [Fact]
    public async Task The_employee_report_is_a_404_outside_the_callers_scope()
    {
        var world = await host.SeedInsightsAsync();
        var handler = host.Get<IKpiEmployeeReportHandler>();

        host.AsEmployee(world, "Ben");
        var own = await handler.HandleAsync(world.Id("Ben"), Page(), default);
        var colleague = await handler.HandleAsync(world.Id("Cara"), Page(), default);
        host.AsManager(world, "Anna", "Ben", "Cara", "Dan");
        var reviewed = await handler.HandleAsync(world.Id("Gail"), Page(), default);
        var stranger = await handler.HandleAsync(world.Id("Eve"), Page(), default);
        host.AsHr(world);
        var unknown = await handler.HandleAsync(9999, Page(), default);

        Assert.True(own.IsSuccess);
        ResultAssert.Failed(colleague, ErrorType.NotFound);
        Assert.Equal(1, ResultAssert.Succeeded(reviewed).Scorecards.Total);
        ResultAssert.Failed(stranger, ErrorType.NotFound);
        ResultAssert.Failed(unknown, ErrorType.NotFound);
    }

    // ---- KPI achievement ----

    [Fact]
    public async Task Kpi_achievement_averages_the_finalized_scorecards_per_template()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var handler = host.Get<IKpiAchievementReportHandler>();

        var all = ResultAssert.Succeeded(await handler.HandleAsync(Page(new KpiReportFilter(PeriodId: world.Q4.Id)), default));
        var quality = ResultAssert.Succeeded(await handler.HandleAsync(Page(new KpiReportFilter(PeriodId: world.Q4.Id, CategoryId: world.Quality.Id)), default));

        // A-1 is assigned on 9 Q4 scorecards; the Finalized ones are Anna 90, Ben 80, Cara 90, Gail 65 (Eve's Rejected 40 does not count).
        var a1 = all.Items.Single(row => row.KpiCode == "A-1");
        Assert.Equal((9, 4, 81.25m, 65m, 90m), (a1.Assigned, a1.Counted, a1.AverageAchievementPct.GetValueOrDefault(), a1.MinAchievementPct.GetValueOrDefault(), a1.MaxAchievementPct.GetValueOrDefault()));
        var q1 = Assert.Single(quality.Items);
        Assert.Equal(("Q-1", "Quality", 1, 1, 80m), (q1.KpiCode, q1.CategoryName, q1.Assigned, q1.Counted, q1.AverageAchievementPct.GetValueOrDefault()));
    }

    // ---- Target vs actual ----

    [Fact]
    public async Task Target_vs_actual_lists_every_kpi_row_with_its_status_and_filters_on_the_server()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);

        var q4 = await TargetVsActualAsync(new KpiReportFilter(PeriodId: world.Q4.Id));
        var rejected = await TargetVsActualAsync(new KpiReportFilter(Status: "Rejected"));
        var ops = await TargetVsActualAsync(new KpiReportFilter(DepartmentId: world.Ops.Id, PeriodId: world.Q4.Id));
        var quality = await TargetVsActualAsync(new KpiReportFilter(CategoryId: world.Quality.Id));
        var searched = await TargetVsActualAsync(search: "Gail");
        var byAchievement = await TargetVsActualAsync(new KpiReportFilter(PeriodId: world.Q4.Id), sort: "achievementPct", direction: "desc");

        Assert.Equal(10, q4.Total);
        var eve = Assert.Single(rejected.Items);
        Assert.Equal(("EMP-0005", "Rejected", 40m), (eve.EmployeeCode, eve.Status, eve.AchievementPct.GetValueOrDefault()));
        Assert.Equal(5, ops.Total); // Eve, Finn, Gail, Ivy, Jon (Hana has no KPI)
        Assert.Equal(("EMP-0002", "Q-1", 50m, 40m, 20m, 16m), (quality.Items[0].EmployeeCode, quality.Items[0].KpiCode, quality.Items[0].Target, quality.Items[0].ActualValue.GetValueOrDefault(), quality.Items[0].Weight, quality.Items[0].WeightedScore.GetValueOrDefault()));
        Assert.Equal(1, quality.Total);
        Assert.All(searched.Items, row => Assert.Equal("EMP-0007", row.EmployeeCode));
        Assert.Equal(90m, byAchievement.Items[0].AchievementPct);
    }

    [Fact]
    public async Task Target_vs_actual_shows_an_employee_only_their_own_rows_and_a_manager_their_team()
    {
        var world = await host.SeedInsightsAsync();
        host.AsEmployee(world, "Ben");
        var own = await TargetVsActualAsync();
        host.AsManager(world, "Anna", "Ben", "Cara", "Dan");
        var team = await TargetVsActualAsync(new KpiReportFilter(PeriodId: world.Q4.Id));

        Assert.Equal(3, own.Total); // Q4 A-1 and Q-1, Q3 A-1
        Assert.All(own.Items, row => Assert.Equal("EMP-0002", row.EmployeeCode));
        // The Team tier: Anna herself, her reports Ben, Cara, Dan and Gail, whose reviewer she is.
        Assert.Equal(["EMP-0001", "EMP-0002", "EMP-0003", "EMP-0004", "EMP-0007"], team.Items.Select(row => row.EmployeeCode).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_report_refuses_an_unknown_status_and_an_unknown_sort_field()
    {
        var world = await host.SeedInsightsAsync();
        host.AsHr(world);
        var handler = host.Get<IKpiTargetVsActualReportHandler>();

        ResultAssert.FailedOnField(await handler.HandleAsync(Page(new KpiReportFilter(Status: "Done")), default), "status");
        ResultAssert.FailedOnField(await handler.HandleAsync(Page(sort: "managerComment"), default), "sort");
        ResultAssert.FailedOnField(await handler.HandleAsync(Page(search: new string('x', 101)), default), "search");
    }
}
