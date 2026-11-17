using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.IntegrationTests.Support;
using ClosedXML.Excel;
using Org = (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position);

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The KPI dashboards, reports and Excel exports over HTTP against the test database, with real roles: every query runs on SQL Server,
/// the figures of a seeded set are known, ties share a rank, the department report keeps the department copied onto the scorecard after a
/// transfer, and the exports follow the caller's scope and the filters. The scorecards are planned through the real handlers and then put
/// into their state with plain SQL (the workflow has its own tests); every row carries the test's tag.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class KpiInsightHttpTests : DatabaseTest, IDisposable
{
    private static readonly string[] EmployeeGrants = ["kpi:View", "kpi:Create", "excel:View", "excel:Export"];
    private static readonly string[] ManagerGrants = ["kpi:View", "kpi:Create", "kpi:Approve", "excel:View", "excel:Export"];
    private static readonly decimal[] FinalizedQ4Scores = [90m, 80m, 90m];

    private readonly ApiFactory factory = new();
    private readonly HttpClient admin;
    private readonly List<HttpClient> others = [];

    public KpiInsightHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        admin = factory.CreateBrowserClient(out _);
    }

    public override Task InitializeAsync() => admin.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        admin.Dispose();
        foreach (var other in others)
        {
            other.Dispose();
        }

        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private async Task<HttpClient> SignedInAsync(string suffix, int? employeeId, params string[] grants)
    {
        var role = $"{Tag}-{suffix}";
        var roleId = await Sql.ScalarAsync<int>("INSERT INTO sec.Role (Name, IsSystem, Status) OUTPUT INSERTED.Id VALUES (@name, 0, 'Active');", ("@name", role));
        foreach (var grant in grants.OrderBy(grant => grant.EndsWith(":View", StringComparison.Ordinal) ? 0 : 1))
        {
            var parts = grant.Split(':');
            await Sql.ExecuteAsync(
                "INSERT INTO sec.RolePermission (RoleId, ModuleKey, Action) VALUES (@role, @module, @action);",
                ("@role", roleId),
                ("@module", parts[0]),
                ("@action", parts[1]));
        }

        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, $"{suffix}-login"), roleName: role, employeeId: employeeId);
        var signedIn = factory.CreateBrowserClient(out _);
        others.Add(signedIn);
        await signedIn.SignInAsync(account);
        return signedIn;
    }

    private static async Task<JsonElement> OkAsync(Task<HttpResponseMessage> request)
    {
        var response = await request;
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static decimal Dec(JsonElement json, string name) => json.GetProperty(name).GetDecimal();

    private static int Int(JsonElement json, string name) => json.GetProperty(name).GetInt32();

    private static string Str(JsonElement json, string name) => json.GetProperty(name).GetString()!;

    private static List<JsonElement> Items(JsonElement json, string name = "items") => [.. json.GetProperty(name).EnumerateArray()];

    // ---- The seeded set ----

    private sealed record World(Org Org, EvaluationPeriodDto Q4, EvaluationPeriodDto Q3, int TemplateId, Dictionary<string, EmployeeDto> People, Dictionary<string, int> Q4Cards)
    {
        public int Id(string who) => People[who].Id;

        public string Code(string who) => People[who].EmployeeCode;
    }

    /// <summary>
    /// Q4 2026 (Active): Anna Finalized 90, Ben (reports to Anna) Finalized 80, Cara (reports to Anna) Finalized 90, Dan (reports to Anna)
    /// Submitted 55, Eve Rejected 40, Finn Draft. Q3 2026 (Completed): Ben Finalized 70. One KPI each (template S-01, weight 100) whose
    /// achievement is the score; Finn's has no result. All in the seeded department.
    /// </summary>
    private async Task<World> SeedAsync()
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var template = await SeedKpiTemplateAsync(category.Id);
        var q3 = await SeedActivePeriodAsync(org.Company.Id, "2026 Q3", start: "2026-07-01", end: "2026-09-30");
        var q4 = await SeedActivePeriodAsync(org.Company.Id);

        var people = new Dictionary<string, EmployeeDto>();
        people["Anna"] = await EmployeeAsync(org, "1", "anna");
        foreach (var (who, number, managed) in new (string, string, bool)[] { ("Ben", "2", true), ("Cara", "3", true), ("Dan", "4", true), ("Eve", "5", false), ("Finn", "6", false) })
        {
            people[who] = await EmployeeAsync(org, number, who.ToLowerInvariant(), managed ? people["Anna"].Id : null);
        }

        var world = new World(org, q4, q3, template.Id, people, []);
        world.Q4Cards["Anna"] = await CardAsync(world, "Anna", q4, "Finalized", 90m, "Exceeds");
        world.Q4Cards["Ben"] = await CardAsync(world, "Ben", q4, "Finalized", 80m, "Meets");
        world.Q4Cards["Cara"] = await CardAsync(world, "Cara", q4, "Finalized", 90m, "Exceeds");
        world.Q4Cards["Dan"] = await CardAsync(world, "Dan", q4, "Submitted", 55m, "Low");
        world.Q4Cards["Eve"] = await CardAsync(world, "Eve", q4, "Rejected", 40m, "Low");
        world.Q4Cards["Finn"] = await CardAsync(world, "Finn", q4, "Draft", null, null);
        await CardAsync(world, "Ben", q3, "Finalized", 70m, "Meets");

        var completed = Succeeded(await RunCommand<CompletePeriodCommand, EvaluationPeriodDto>(new CompletePeriodCommand(q3.Id, Requests.Version(q3.RowVersion))));
        return world with { Q3 = completed };
    }

    private async Task<EmployeeDto> EmployeeAsync(Org org, string suffix, string local, int? managerId = null) =>
        Succeeded(await Create(
            Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, suffix, local) with { ManagerId = managerId }));

    /// <summary>Plans and finalizes a one-KPI scorecard through the handlers, then moves it to <paramref name="status"/> with plain SQL.</summary>
    private async Task<int> CardAsync(World world, string who, EvaluationPeriodDto period, string status, decimal? score, string? label)
    {
        var card = Succeeded(await FinalizePlan(await SeedScorecardAsync(world.Id(who), period.Id, ItemOf(world.TemplateId, weight: 100m))));
        if (status == "Draft")
        {
            return card.Id;
        }

        await Sql.ExecuteAsync(
            """
            UPDATE kpi.Scorecard SET Status = @status, SubmittedOn = '2026-11-09', OverallScore = @score, RatingLabel = @label,
                CalculationVersion = 1,
                ReviewedOn = CASE WHEN @status IN ('Approved', 'Finalized', 'Rejected') THEN CAST('2026-11-09' AS date) END,
                FinalizedOn = CASE WHEN @status = 'Finalized' THEN CAST('2026-11-09' AS date) END
            WHERE Id = @id;
            UPDATE kpi.ScorecardItem SET ActualValue = @score, AchievementPct = @score, WeightedScore = @score WHERE ScorecardId = @id;
            """,
            ("@status", status),
            ("@score", score),
            ("@label", label is null ? null : Name(label)),
            ("@id", card.Id));
        return card.Id;
    }

    // ---- Dashboards ----

    [IntegrationFact]
    public async Task The_hr_dashboard_computes_the_figures_of_the_period_in_the_database()
    {
        var world = await SeedAsync();
        var threshold = await Sql.ScalarAsync<decimal>("SELECT ISNULL(MIN(MinScore), 60) FROM kpi.RatingBand WHERE Status = 'Active' AND MinScore >= 60;");

        var hr = await OkAsync(admin.GetAsync($"/api/kpi/dashboard/hr?periodId={world.Q4.Id}"));
        var ofCompany = await OkAsync(admin.GetAsync($"/api/kpi/dashboard/hr?companyId={world.Org.Company.Id}"));

        Assert.Equal(world.Q4.Id, Int(hr.GetProperty("period"), "id"));
        Assert.Equal((6, 6, 3, 1, 1), (Int(hr, "employeesWithKpis"), Int(hr, "totalAssignments"), Int(hr, "completedEvaluations"), Int(hr, "pendingSubmissions"), Int(hr, "pendingApprovals")));
        Assert.Equal(86.67m, Dec(hr, "averageScore"));      // (90 + 80 + 90) / 3; Eve's Rejected 40 and Dan's Submitted 55 do not count
        Assert.Equal(50m, Dec(hr, "completionRatePct"));    // 3 Finalized / 6 past Planning
        var department = Assert.Single(Items(hr, "departments"));
        Assert.Equal((world.Org.Department.Id, 6, 3, 86.67m), (Int(department, "departmentId"), Int(department, "scorecards"), Int(department, "finalized"), Dec(department, "averageScore")));
        Assert.Equal([world.Code("Anna"), world.Code("Cara"), world.Code("Ben")], Items(hr, "topPerformers").Select(row => Str(row, "employeeCode")));
        Assert.Equal(threshold, Dec(hr, "attentionThreshold"));
        Assert.Equal(FinalizedQ4Scores.Count(score => score < threshold), Int(hr, "requiringAttentionCount"));
        Assert.Equal(
            new[] { (Name("Exceeds"), 2), (Name("Meets"), 1) }.Order(),
            Items(hr, "ratingDistribution").Select(row => (Str(row, "ratingLabel"), Int(row, "count"))).Order());
        Assert.Equal(1, Items(hr, "byStatus").Single(row => Str(row, "status") == "Rejected").GetProperty("count").GetInt32());
        Assert.Equal(world.Q4.Id, Int(ofCompany.GetProperty("period"), "id")); // the Active period of the company
    }

    [IntegrationFact]
    public async Task The_manager_and_employee_dashboards_show_only_their_own_set_and_the_hr_dashboard_refuses_a_manager()
    {
        var world = await SeedAsync();
        var anna = await SignedInAsync("anna", world.Id("Anna"), ManagerGrants);
        var ben = await SignedInAsync("ben", world.Id("Ben"), EmployeeGrants);

        var manager = await OkAsync(anna.GetAsync("/api/kpi/dashboard/manager"));
        var employee = await OkAsync(ben.GetAsync("/api/kpi/dashboard/employee"));
        var refused = await anna.GetAsync("/api/kpi/dashboard/hr");
        var notManager = await ben.GetAsync("/api/kpi/dashboard/manager");

        Assert.Equal(world.Q4.Id, Int(manager.GetProperty("period"), "id"));
        Assert.Equal((3, 1, 2, 85m), (Int(manager, "teamSize"), Int(manager, "pendingReviews"), Int(manager, "approvedOrFinalized"), Dec(manager, "averageScore")));
        Assert.Equal(new[] { world.Code("Cara"), world.Code("Ben"), world.Code("Dan") }.Order(StringComparer.Ordinal), Items(manager, "employees").Select(row => Str(row, "employeeCode")));
        Assert.Equal((3, false), (Int(manager, "employeeCount"), manager.GetProperty("employeesTruncated").GetBoolean()));

        Assert.Equal(world.Q4Cards["Ben"], Int(employee.GetProperty("scorecard"), "id"));
        Assert.Equal(("Finalized", 80m), (Str(employee.GetProperty("scorecard"), "status"), Dec(employee.GetProperty("scorecard"), "overallScore")));
        Assert.Equal(Name("Meets"), Str(employee.GetProperty("scorecard"), "ratingLabel"));
        Assert.Equal(80m, Dec(Assert.Single(Items(employee)), "achievementPct"));
        Assert.False(employee.GetProperty("pending").GetProperty("isPending").GetBoolean());
        Assert.Equal(70m, Dec(Assert.Single(Items(employee, "history")), "overallScore"));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("The HR dashboard needs HR rights.", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, notManager.StatusCode);
    }

    // ---- Reports ----

    [IntegrationFact]
    public async Task The_ranking_gives_equal_scores_the_same_rank_also_across_pages()
    {
        var world = await SeedAsync();

        var ranking = await OkAsync(admin.GetAsync($"/api/kpi/reports/ranking?periodId={world.Q4.Id}"));
        var secondPage = await OkAsync(admin.GetAsync($"/api/kpi/reports/ranking?periodId={world.Q4.Id}&pageIndex=1&pageSize=1"));
        var badSort = await admin.GetAsync($"/api/kpi/reports/ranking?periodId={world.Q4.Id}&sort=employeeCode:asc");

        Assert.Equal([world.Code("Anna"), world.Code("Cara"), world.Code("Ben")], Items(ranking).Select(row => Str(row, "employeeCode")));
        Assert.Equal([1, 1, 3], Items(ranking).Select(row => Int(row, "rank")));
        Assert.Equal(3, Int(ranking, "total"));
        var cara = Assert.Single(Items(secondPage));
        Assert.Equal((world.Code("Cara"), 1), (Str(cara, "employeeCode"), Int(cara, "rank")));
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
    }

    [IntegrationFact]
    public async Task The_department_report_keeps_the_department_copied_onto_the_scorecard_after_a_transfer()
    {
        var world = await SeedAsync();
        var newDepartment = await SeedDepartmentAsync(world.Org.Company.Id, world.Org.Branch.Id, "B-OPS", "Operations");
        var newPosition = await SeedPositionAsync(newDepartment.Id, world.Org.Grade.Id, "B-OPR");
        var ben = Succeeded(await Get<EmployeeDto>(world.Id("Ben")));
        Succeeded(await Update(ben.Id, Requests.EmployeeUpdate(ben, positionId: newPosition.Id) with { DepartmentId = newDepartment.Id }));

        var before = await OkAsync(admin.GetAsync($"/api/kpi/reports/department/{world.Org.Department.Id}?periodId={world.Q4.Id}&sort=overallScore:desc"));
        var moved = await OkAsync(admin.GetAsync($"/api/kpi/reports/department/{newDepartment.Id}?periodId={world.Q4.Id}"));
        var unknown = await admin.GetAsync("/api/kpi/reports/department/2147483000");

        Assert.Contains(Items(before.GetProperty("rows")), row => Str(row, "employeeCode") == world.Code("Ben"));
        Assert.Equal(6, Int(before, "scorecards"));
        Assert.Equal((3, 86.67m), (Int(before, "finalized"), Dec(before, "averageScore")));
        Assert.Equal(90m, Dec(Items(before.GetProperty("rows")).First(), "overallScore"));
        Assert.Equal(0, Int(moved, "scorecards"));
        Assert.Empty(Items(moved.GetProperty("rows")));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [IntegrationFact]
    public async Task A_department_without_a_visible_scorecard_is_the_same_404_as_an_unknown_one_unless_the_caller_may_read_departments()
    {
        var world = await SeedAsync();
        var empty = await SeedDepartmentAsync(world.Org.Company.Id, world.Org.Branch.Id, "B-FIN", "Finance");
        var ben = await SignedInAsync("Ben", world.Id("Ben"), EmployeeGrants);
        var finnWithOrganization = await SignedInAsync("FinnOrg", world.Id("Finn"), [.. EmployeeGrants, "organization:View"]);

        var own = await ben.GetAsync($"/api/kpi/reports/department/{world.Org.Department.Id}");
        var hidden = await ben.GetAsync($"/api/kpi/reports/department/{empty.Id}");
        var unknown = await ben.GetAsync("/api/kpi/reports/department/2147483000");
        var allowed = await finnWithOrganization.GetAsync($"/api/kpi/reports/department/{empty.Id}");
        var hr = await admin.GetAsync($"/api/kpi/reports/department/{empty.Id}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal($"Department #{empty.Id} was not found.", (await hidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.DoesNotContain("Finance", await hidden.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Department #2147483000 was not found.", (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hr.StatusCode);
    }

    [IntegrationFact]
    public async Task The_employee_dashboard_shows_the_stored_manager_feedback_only_on_a_final_scorecard()
    {
        var world = await SeedAsync();
        await Sql.ExecuteAsync(
            "UPDATE kpi.Scorecard SET ManagerRating = 4, ManagerComment = N'Kept for the manager' WHERE Id IN (@ben, @dan, @eve);",
            ("@ben", world.Q4Cards["Ben"]),
            ("@dan", world.Q4Cards["Dan"]),
            ("@eve", world.Q4Cards["Eve"]));
        var ben = await SignedInAsync("Ben", world.Id("Ben"), EmployeeGrants);
        var dan = await SignedInAsync("Dan", world.Id("Dan"), EmployeeGrants);
        var eve = await SignedInAsync("Eve", world.Id("Eve"), EmployeeGrants);

        var finalized = (await OkAsync(ben.GetAsync("/api/kpi/dashboard/employee"))).GetProperty("feedback");
        var submitted = (await OkAsync(dan.GetAsync("/api/kpi/dashboard/employee"))).GetProperty("feedback");
        var rejected = (await OkAsync(eve.GetAsync("/api/kpi/dashboard/employee"))).GetProperty("feedback");

        Assert.Equal((4m, "Kept for the manager"), (Dec(finalized, "managerRating"), Str(finalized, "managerComment")));
        Assert.Equal((4m, "Kept for the manager"), (Dec(rejected, "managerRating"), Str(rejected, "managerComment")));
        Assert.Equal(JsonValueKind.Null, submitted.GetProperty("managerRating").ValueKind);
        Assert.Equal(JsonValueKind.Null, submitted.GetProperty("managerComment").ValueKind);
    }

    [IntegrationFact]
    public async Task The_summary_achievement_target_vs_actual_and_employee_reports_run_on_the_server()
    {
        var world = await SeedAsync();
        var company = $"companyId={world.Org.Company.Id}";

        var summary = await OkAsync(admin.GetAsync($"/api/kpi/reports/summary?{company}&sort=periodName:asc"));
        var quarterly = await OkAsync(admin.GetAsync($"/api/kpi/reports/summary?{company}&periodType=Quarterly&year=2026"));
        var achievement = await OkAsync(admin.GetAsync($"/api/kpi/reports/kpi-achievement?{company}&sort=averageAchievement:desc"));
        var rows = await OkAsync(admin.GetAsync($"/api/kpi/reports/target-vs-actual?{company}&sort=employeeName:asc&pageSize=3"));
        var rejected = await OkAsync(admin.GetAsync($"/api/kpi/reports/target-vs-actual?{company}&status=Rejected"));
        var history = await OkAsync(admin.GetAsync($"/api/kpi/reports/employee/{world.Id("Ben")}?sort=periodStart:desc"));
        var badStatus = await admin.GetAsync($"/api/kpi/reports/target-vs-actual?{company}&status=Done");

        var q4 = Items(summary).Single(row => Int(row, "periodId") == world.Q4.Id);
        Assert.Equal((6, 0, 3, 1), (Int(q4, "scorecards"), Int(q4, "inPlanning"), Int(q4, "finalized"), Int(q4, "rejected")));
        Assert.Equal((86.67m, 80m, 90m, 50m), (Dec(q4, "averageScore"), Dec(q4, "minScore"), Dec(q4, "maxScore"), Dec(q4, "completionRatePct")));
        Assert.Equal(2, Int(quarterly, "total"));

        var template = Assert.Single(Items(achievement));
        Assert.Equal((7, 4, 82.5m), (Int(template, "assigned"), Int(template, "counted"), Dec(template, "averageAchievementPct"))); // (90 + 80 + 90 + 70) / 4

        Assert.Equal(7, Int(rows, "total"));
        Assert.Equal(3, Items(rows).Count);
        Assert.Equal(world.Code("Eve"), Str(Assert.Single(Items(rejected)), "employeeCode"));
        Assert.Equal("Rejected", Str(Items(rejected).Single(), "status"));

        Assert.Equal((2, 75m), (Int(history, "finalized"), Dec(history, "averageScore")));
        Assert.Equal([world.Q4.Id, world.Q3.Id], Items(history.GetProperty("scorecards")).Select(row => Int(row, "periodId")));
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
    }

    [IntegrationFact]
    public async Task An_employee_reads_reports_of_their_own_scorecards_only()
    {
        var world = await SeedAsync();
        var ben = await SignedInAsync("ben", world.Id("Ben"), EmployeeGrants);

        var ranking = await OkAsync(ben.GetAsync("/api/kpi/reports/ranking"));
        var rows = await OkAsync(ben.GetAsync("/api/kpi/reports/target-vs-actual"));
        var colleague = await ben.GetAsync($"/api/kpi/reports/employee/{world.Id("Cara")}");

        Assert.Equal([1, 2], Items(ranking).Select(row => Int(row, "rank")));
        Assert.All(Items(ranking), row => Assert.Equal(world.Code("Ben"), Str(row, "employeeCode")));
        Assert.All(Items(rows), row => Assert.Equal(world.Code("Ben"), Str(row, "employeeCode")));
        Assert.Equal(HttpStatusCode.NotFound, colleague.StatusCode);
    }

    // ---- Excel ----

    private static List<(string Code, int Rank)> RankingRows(byte[] workbook)
    {
        using var book = new XLWorkbook(new MemoryStream(workbook));
        var sheet = book.Worksheets.First();
        var headers = Enumerable.Range(1, sheet.LastColumnUsed()!.ColumnNumber()).Select(c => sheet.Cell(1, c).GetString()).ToList();
        var code = headers.IndexOf("Employee Code") + 1;
        var rank = headers.IndexOf("Rank") + 1;
        var last = sheet.LastRowUsed()!.RowNumber();
        return [.. Enumerable.Range(2, Math.Max(0, last - 1)).Select(r => (sheet.Cell(r, code).GetString(), (int)sheet.Cell(r, rank).GetDouble()))];
    }

    [IntegrationFact]
    public async Task The_kpi_ranking_export_honours_the_callers_scope_and_the_filters()
    {
        var world = await SeedAsync();
        var ben = await SignedInAsync("ben", world.Id("Ben"), EmployeeGrants);
        var noKpi = await SignedInAsync("nokpi", world.Id("Eve"), "excel:View", "excel:Export");

        var own = await ben.PostAsJsonAsync("/api/excel/exports/kpi-ranking", new { scope = "all" });
        var filtered = await admin.PostAsJsonAsync("/api/excel/exports/kpi-ranking", new { scope = "filtered", filters = new { periodId = world.Q4.Id, companyId = world.Org.Company.Id } });
        var searched = await admin.PostAsJsonAsync("/api/excel/exports/kpi-ranking", new { scope = "filtered", search = world.Code("Cara"), filters = new { companyId = world.Org.Company.Id } });
        var refused = await noKpi.PostAsJsonAsync("/api/excel/exports/kpi-ranking", new { scope = "all" });

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal([(world.Code("Ben"), 1), (world.Code("Ben"), 2)], RankingRows(await own.Content.ReadAsByteArrayAsync()));
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal([(world.Code("Anna"), 1), (world.Code("Cara"), 1), (world.Code("Ben"), 3)], RankingRows(await filtered.Content.ReadAsByteArrayAsync()));
        Assert.Equal([(world.Code("Cara"), 1)], RankingRows(await searched.Content.ReadAsByteArrayAsync()));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [IntegrationFact]
    public async Task The_scorecard_and_target_vs_actual_exports_run_on_the_server_and_carry_no_comment()
    {
        var world = await SeedAsync();
        await Sql.ExecuteAsync("UPDATE kpi.ScorecardItem SET EmployeeComment = N'Private remark' WHERE ScorecardId = @id;", ("@id", world.Q4Cards["Ben"]));
        var filters = new { companyId = world.Org.Company.Id };

        var cards = await admin.PostAsJsonAsync("/api/excel/exports/kpi-scorecards", new { scope = "filtered", filters });
        var items = await admin.PostAsJsonAsync("/api/excel/exports/kpi-target-vs-actual", new { scope = "filtered", filters, dateFrom = "2026-10-01", dateTo = "2026-12-31" });

        Assert.Equal(HttpStatusCode.OK, cards.StatusCode);
        Assert.Equal(HttpStatusCode.OK, items.StatusCode);
        using var cardBook = new XLWorkbook(new MemoryStream(await cards.Content.ReadAsByteArrayAsync()));
        using var itemBook = new XLWorkbook(new MemoryStream(await items.Content.ReadAsByteArrayAsync()));
        Assert.Equal(8, cardBook.Worksheets.First().LastRowUsed()!.RowNumber()); // header + 7 scorecards
        Assert.Equal(7, itemBook.Worksheets.First().LastRowUsed()!.RowNumber()); // header + 6 Q4 KPIs (the date range leaves Q3 out)
        Assert.DoesNotContain(itemBook.Worksheets.First().CellsUsed(), cell => cell.GetString().Contains("Private remark", StringComparison.Ordinal));
    }

    // ---- The contract additions of this phase ----

    [IntegrationFact]
    public async Task Lists_and_the_period_picker_carry_the_submission_due_date_and_history_entries_the_actor_user_id()
    {
        var world = await SeedAsync();
        var ben = await SignedInAsync("ben", world.Id("Ben"), EmployeeGrants);

        var mine = await OkAsync(ben.GetAsync("/api/kpi/my-scorecards"));
        var options = await OkAsync(ben.GetAsync($"/api/kpi/periods/options?companyId={world.Org.Company.Id}"));
        var detail = await OkAsync(ben.GetAsync($"/api/kpi/scorecards/{world.Q4Cards["Ben"]}"));

        Assert.Equal("2027-01-10", Str(Items(mine).Single(row => Int(row, "periodId") == world.Q4.Id), "submissionDueDate"));
        Assert.Equal("2027-01-10", Str(options.EnumerateArray().Single(row => Int(row, "id") == world.Q4.Id), "submissionDueDate"));
        var plan = Items(detail, "history").Single(entry => Str(entry, "action") == "Finalize Plan");
        Assert.Equal(TestAdministrator.AccountId, Int(plan, "actorUserId"));
    }
}
