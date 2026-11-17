using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The scorecard routes over HTTP against the test database: the 201/200 convention of an assignment, the JSON shapes, the plan
/// commands with their row versions, the error mapping, and the row-level scope of the reads (an HR administrator sees everyone,
/// a manager sees themselves and their direct reports, an employee only themselves; anything else is a 404).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ScorecardHttpTests : DatabaseTest, IDisposable
{
    private static readonly string[] ManagerGrants = ["kpi:View", "kpi:Approve"];
    private static readonly string[] EmployeeGrants = ["kpi:View", "kpi:Create"];
    private static readonly string[] SetupOnlyGrants = ["kpi-setup:View", "kpi-setup:Create", "kpi-setup:Edit", "kpi-setup:Delete"];

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;
    private readonly List<HttpClient> others = [];

    public ScorecardHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        client = factory.CreateBrowserClient(out _);
    }

    public override Task InitializeAsync() => client.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        client.Dispose();
        foreach (var other in others)
        {
            other.Dispose();
        }

        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static Task<HttpResponseMessage> Send(HttpClient http, HttpMethod method, string url, object body) =>
        http.SendAsync(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) });

    private async Task<HttpClient> SignedInAsRoleAsync(string suffix, int? employeeId, params string[] grants)
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

    private sealed record Seed(
        Apex.Hrms.Application.Organization.Companies.CompanyDto Company,
        int DepartmentId,
        int Category,
        int First,
        int Second,
        EvaluationPeriodDto Period,
        (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position) Org);

    private async Task<Seed> SeedAsync()
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var first = await SeedKpiTemplateAsync(category.Id, "S-01", "Monthly sales");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02", "Quality score");
        var period = await SeedActivePeriodAsync(org.Company.Id);
        return new Seed(org.Company, org.Department.Id, category.Id, first.Id, second.Id, period, org);
    }

    private sealed record ItemBody(int TemplateId, decimal? Weight);

    private static ItemBody[] Items(params (int TemplateId, decimal? Weight)[] items) =>
        [.. items.Select(item => new ItemBody(item.TemplateId, item.Weight))];

    // ---- Assigning ----

    [IntegrationFact]
    public async Task A_first_assignment_is_201_with_a_location_and_the_scorecard_and_a_second_is_200_on_the_same_scorecard()
    {
        var seed = await SeedAsync();
        var employee = await SeedEmployeeAsync(seed.Org);

        var created = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.First, 40m)) });
        var json = await ReadAsync(created);
        var id = json.GetProperty("id").GetInt32();
        var added = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.Second, 60m)) });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/kpi/scorecards/{id}", created.Headers.Location?.OriginalString);
        Assert.Equal("Planning", json.GetProperty("status").GetString());
        Assert.Equal(employee.EmployeeCode, json.GetProperty("employeeCode").GetString());
        Assert.Equal(40m, json.GetProperty("totalWeight").GetDecimal());
        Assert.Equal(1, json.GetProperty("itemCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("reviewerEmployeeId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("planFinalizedOn").ValueKind);
        Assert.Equal(JsonValueKind.Array, json.GetProperty("history").ValueKind);
        var item = json.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Higher Is Better", item.GetProperty("direction").GetString());
        Assert.Equal("Direct Percentage", item.GetProperty("scoringMethod").GetString());
        Assert.Equal("2026-10-01", item.GetProperty("startDate").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("actualValue").ValueKind);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Null(added.Headers.Location);
        var after = await ReadAsync(added);
        Assert.Equal(id, after.GetProperty("id").GetInt32());
        Assert.Equal(100m, after.GetProperty("totalWeight").GetDecimal());
        Assert.Equal(2, after.GetProperty("items").GetArrayLength());
    }

    [IntegrationFact]
    public async Task A_bad_assignment_is_a_400_with_camel_case_field_errors_and_an_unavailable_period_or_a_duplicate_is_a_409()
    {
        var seed = await SeedAsync();
        var employee = await SeedEmployeeAsync(seed.Org);
        await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.First, 40m)) });

        var missingTemplate = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((2_000_000_000, null)) });
        var missingEmployee = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = 2_000_000_000, periodId = seed.Period.Id, items = Items((seed.Second, null)) });
        var noItems = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Array.Empty<object>() });
        var heavy = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.Second, 60.01m)) });
        var badWeight = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.Second, 0m)) });
        var zeroTarget = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = new[] { new { templateId = seed.Second, target = 0m } } });
        var duplicate = await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.First, 10m)) });

        Assert.Equal(HttpStatusCode.BadRequest, missingTemplate.StatusCode);
        Assert.True((await ReadAsync(missingTemplate)).GetProperty("errors").TryGetProperty("items[0].templateId", out _));
        Assert.True((await ReadAsync(missingEmployee)).GetProperty("errors").TryGetProperty("employeeId", out _));
        Assert.True((await ReadAsync(noItems)).GetProperty("errors").TryGetProperty("items", out _));
        Assert.True((await ReadAsync(heavy)).GetProperty("errors").TryGetProperty("items", out _));
        Assert.True((await ReadAsync(badWeight)).GetProperty("errors").TryGetProperty("items[0].weight", out _));
        Assert.True((await ReadAsync(zeroTarget)).GetProperty("errors").TryGetProperty("items[0].target", out _));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(Code("S-01"), (await ReadAsync(duplicate)).GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task A_bulk_assignment_is_200_with_the_assigned_and_skipped_lists_and_bad_selectors_are_400s()
    {
        var seed = await SeedAsync();
        var one = await SeedEmployeeAsync(seed.Org, "1", "ria");
        var two = await SeedEmployeeAsync(seed.Org, "2", "rex");

        var response = await client.PostAsJsonAsync("/api/kpi/assignments/bulk", new { periodId = seed.Period.Id, employeeIds = new[] { one.Id, two.Id, 2_000_000_000 }, items = Items((seed.First, 50m)) });
        var again = await client.PostAsJsonAsync("/api/kpi/assignments/bulk", new { periodId = seed.Period.Id, employeeIds = new[] { one.Id, two.Id }, items = Items((seed.First, 50m)) });
        var byFilter = await client.PostAsJsonAsync("/api/kpi/assignments/bulk", new { periodId = seed.Period.Id, departmentId = seed.DepartmentId, items = Items((seed.Second, 50m)) });
        var both = await client.PostAsJsonAsync("/api/kpi/assignments/bulk", new { periodId = seed.Period.Id, employeeIds = new[] { one.Id }, departmentId = seed.DepartmentId, items = Items((seed.First, 50m)) });
        var neither = await client.PostAsJsonAsync("/api/kpi/assignments/bulk", new { periodId = seed.Period.Id, items = Items((seed.First, 50m)) });
        var tooMany = await client.PostAsJsonAsync("/api/kpi/assignments/bulk", new { periodId = seed.Period.Id, employeeIds = Enumerable.Range(1, 501).ToArray(), items = Items((seed.First, 50m)) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadAsync(response);
        var assigned = json.GetProperty("assigned").EnumerateArray().ToList();
        Assert.Equal([one.Id, two.Id], assigned.Select(entry => entry.GetProperty("employeeId").GetInt32()));
        Assert.Equal(1, assigned[0].GetProperty("itemsAdded").GetInt32());
        Assert.Equal(one.EmployeeCode, assigned[0].GetProperty("employeeCode").GetString());
        Assert.True(assigned[0].GetProperty("scorecardId").GetInt32() > 0);
        var skipped = Assert.Single(json.GetProperty("skipped").EnumerateArray());
        Assert.Equal(2_000_000_000, skipped.GetProperty("employeeId").GetInt32());
        Assert.Equal(JsonValueKind.Null, skipped.GetProperty("employeeCode").ValueKind);
        var againJson = await ReadAsync(again);
        Assert.Equal(0, againJson.GetProperty("assigned").GetArrayLength());
        Assert.Equal(2, againJson.GetProperty("skipped").GetArrayLength());
        Assert.Equal(2, (await ReadAsync(byFilter)).GetProperty("assigned").GetArrayLength());
        Assert.True((await ReadAsync(both)).GetProperty("errors").TryGetProperty("employeeIds", out _));
        Assert.True((await ReadAsync(neither)).GetProperty("errors").TryGetProperty("employeeIds", out _));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    // ---- The plan ----

    [IntegrationFact]
    public async Task The_plan_is_changed_removed_finalized_and_deleted_with_row_versions_in_the_body()
    {
        var seed = await SeedAsync();
        var manager = await SeedEmployeeAsync(seed.Org, "1", "maya");
        var employee = Succeeded(await Create(Requests.Employee(seed.Company.Id, seed.Org.Branch.Id, seed.DepartmentId, seed.Org.Position.Id, seed.Org.Grade.Id, "2", "ria") with { ManagerId = manager.Id }));
        var created = await ReadAsync(await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.First, 60m), (seed.Second, 40m)) }));
        var scorecardId = created.GetProperty("id").GetInt32();
        var first = created.GetProperty("items")[0];
        var itemId = first.GetProperty("id").GetInt32();
        var itemVersion = first.GetProperty("rowVersion").GetString();

        var update = await client.PutAsJsonAsync($"/api/kpi/scorecard-items/{itemId}", new { target = 250m, weight = 60m, startDate = "2026-10-01", endDate = "2026-12-31", sortOrder = 3, rowVersion = itemVersion });
        var stale = await client.PutAsJsonAsync($"/api/kpi/scorecard-items/{itemId}", new { target = 250m, weight = 60m, startDate = "2026-10-01", endDate = "2026-12-31", sortOrder = 3, rowVersion = itemVersion });
        var noVersion = await client.PutAsJsonAsync($"/api/kpi/scorecard-items/{itemId}", new { target = 250m, weight = 60m, startDate = "2026-10-01", endDate = "2026-12-31" });
        var updated = await ReadAsync(update);
        var heavy = await client.PutAsJsonAsync($"/api/kpi/scorecard-items/{itemId}", new { target = 250m, weight = 61m, startDate = "2026-10-01", endDate = "2026-12-31", rowVersion = updated.GetProperty("rowVersion").GetString() });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(250m, updated.GetProperty("target").GetDecimal());
        Assert.Equal(3, updated.GetProperty("sortOrder").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.True((await ReadAsync(noVersion)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        Assert.True((await ReadAsync(heavy)).GetProperty("errors").TryGetProperty("weight", out _));

        var current = await ReadAsync(await client.GetAsync($"/api/kpi/scorecards/{scorecardId}"));
        var finalize = await client.PostAsJsonAsync($"/api/kpi/scorecards/{scorecardId}/finalize-plan", new { rowVersion = current.GetProperty("rowVersion").GetString() });
        var finalized = await ReadAsync(finalize);
        var again = await client.PostAsJsonAsync($"/api/kpi/scorecards/{scorecardId}/finalize-plan", new { rowVersion = finalized.GetProperty("rowVersion").GetString() });
        var noBody = await client.PostAsync($"/api/kpi/scorecards/{scorecardId}/finalize-plan", content: null);

        Assert.Equal(HttpStatusCode.OK, finalize.StatusCode);
        Assert.Equal("Draft", finalized.GetProperty("status").GetString());
        Assert.Equal(manager.Id, finalized.GetProperty("reviewerEmployeeId").GetInt32());
        Assert.Equal(JsonValueKind.String, finalized.GetProperty("planFinalizedOn").ValueKind);
        var step = finalized.GetProperty("history").EnumerateArray().Single();
        Assert.Equal("Finalize Plan", step.GetProperty("action").GetString());
        Assert.Equal("HR", step.GetProperty("step").GetString());
        Assert.Equal("Planning", step.GetProperty("fromStatus").GetString());
        Assert.Equal("Draft", step.GetProperty("toStatus").GetString());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);

        var deleteDraft = await Send(client, HttpMethod.Delete, $"/api/kpi/scorecards/{scorecardId}", new { rowVersion = finalized.GetProperty("rowVersion").GetString() });
        var deleteItem = await Send(client, HttpMethod.Delete, $"/api/kpi/scorecard-items/{itemId}", new { rowVersion = (await ReadAsync(await client.GetAsync($"/api/kpi/scorecards/{scorecardId}"))).GetProperty("items")[0].GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, deleteDraft.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteItem.StatusCode);
    }

    [IntegrationFact]
    public async Task A_kpi_and_a_planning_scorecard_are_deleted_with_204_and_then_are_404()
    {
        var seed = await SeedAsync();
        var employee = await SeedEmployeeAsync(seed.Org);
        var created = await ReadAsync(await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.First, 60m), (seed.Second, 40m)) }));
        var scorecardId = created.GetProperty("id").GetInt32();
        var item = created.GetProperty("items")[0];

        var withoutBody = await client.DeleteAsync($"/api/kpi/scorecard-items/{item.GetProperty("id").GetInt32()}");
        var deletedItem = await Send(client, HttpMethod.Delete, $"/api/kpi/scorecard-items/{item.GetProperty("id").GetInt32()}", new { rowVersion = item.GetProperty("rowVersion").GetString() });
        var missingItem = await Send(client, HttpMethod.Delete, $"/api/kpi/scorecard-items/{item.GetProperty("id").GetInt32()}", new { rowVersion = item.GetProperty("rowVersion").GetString() });
        var current = await ReadAsync(await client.GetAsync($"/api/kpi/scorecards/{scorecardId}"));
        var deleted = await Send(client, HttpMethod.Delete, $"/api/kpi/scorecards/{scorecardId}", new { rowVersion = current.GetProperty("rowVersion").GetString() });
        var gone = await client.GetAsync($"/api/kpi/scorecards/{scorecardId}");

        Assert.Equal(HttpStatusCode.BadRequest, withoutBody.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deletedItem.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingItem.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    // ---- Reads and the scope ----

    [IntegrationFact]
    public async Task A_manager_sees_their_own_scorecard_and_their_direct_reports_and_another_employees_is_a_404()
    {
        var seed = await SeedAsync();
        var maya = await SeedEmployeeAsync(seed.Org, "1", "maya");
        Task<EmployeeDto> Report(string suffix, string local) => Task.Run(async () => Succeeded(await Create(
            Requests.Employee(seed.Company.Id, seed.Org.Branch.Id, seed.DepartmentId, seed.Org.Position.Id, seed.Org.Grade.Id, suffix, local) with { ManagerId = maya.Id })));
        var ria = await Report("2", "ria");
        var rex = await Report("3", "rex");
        var nora = await SeedEmployeeAsync(seed.Org, "4", "nora");
        var cards = new Dictionary<int, int>();
        foreach (var employee in new[] { maya, ria, rex, nora })
        {
            // Finalized, because an employee does not see their own scorecard while it is in Planning (a test of its own below).
            cards[employee.Id] = Succeeded(await FinalizePlan(await SeedScorecardAsync(employee.Id, seed.Period.Id, ItemOf(seed.First, weight: 100m)))).Id;
        }

        var manager = await SignedInAsRoleAsync("Manager", maya.Id, ManagerGrants);
        var staff = await SignedInAsRoleAsync("Employee", ria.Id, EmployeeGrants);

        var team = await EmployeeIdsAsync(manager, $"/api/kpi/scorecards/query?periodId={seed.Period.Id}&pageSize=200");
        var everyone = await EmployeeIdsAsync(client, $"/api/kpi/scorecards/query?periodId={seed.Period.Id}&pageSize=200");
        var own = await EmployeeIdsAsync(staff, $"/api/kpi/scorecards/query?periodId={seed.Period.Id}&pageSize=200");
        var getReport = await manager.GetAsync($"/api/kpi/scorecards/{cards[rex.Id]}");
        var getSelf = await manager.GetAsync($"/api/kpi/scorecards/{cards[maya.Id]}");
        var getStranger = await manager.GetAsync($"/api/kpi/scorecards/{cards[nora.Id]}");
        var strangerFilter = await manager.GetAsync($"/api/kpi/scorecards/query?employeeId={nora.Id}");
        var staffGetManager = await staff.GetAsync($"/api/kpi/scorecards/{cards[maya.Id]}");
        var staffGetSelf = await staff.GetAsync($"/api/kpi/scorecards/{cards[ria.Id]}");
        var hrGetStranger = await client.GetAsync($"/api/kpi/scorecards/{cards[nora.Id]}");

        Assert.Equal([maya.Id, ria.Id, rex.Id], team.Order());
        Assert.Equal(4, everyone.Count);
        Assert.Equal([ria.Id], own);
        Assert.Equal(HttpStatusCode.OK, getReport.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getSelf.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getStranger.StatusCode);
        Assert.Equal(0, (await ReadAsync(strangerFilter)).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, staffGetManager.StatusCode);
        Assert.Equal(HttpStatusCode.OK, staffGetSelf.StatusCode);
        Assert.Equal(HttpStatusCode.OK, hrGetStranger.StatusCode);
    }

    private static async Task<List<int>> EmployeeIdsAsync(HttpClient http, string url) =>
        [.. (await ReadAsync(await http.GetAsync(url))).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("employeeId").GetInt32())];

    [IntegrationFact]
    public async Task An_employee_does_not_see_their_own_scorecard_while_it_is_in_planning_but_their_manager_and_hr_do()
    {
        var seed = await SeedAsync();
        var maya = await SeedEmployeeAsync(seed.Org, "1", "maya");
        var ria = Succeeded(await Create(
            Requests.Employee(seed.Company.Id, seed.Org.Branch.Id, seed.DepartmentId, seed.Org.Position.Id, seed.Org.Grade.Id, "2", "ria") with { ManagerId = maya.Id }));
        var planning = await SeedScorecardAsync(ria.Id, seed.Period.Id, ItemOf(seed.First, weight: 100m));
        var manager = await SignedInAsRoleAsync("Manager", maya.Id, ManagerGrants);
        var staff = await SignedInAsRoleAsync("Employee", ria.Id, EmployeeGrants);
        var url = $"/api/kpi/scorecards/{planning.Id}";

        var hidden = await staff.GetAsync(url);
        var ownList = await ReadAsync(await staff.GetAsync($"/api/kpi/scorecards/query?status=Planning&pageSize=200"));
        var dashboard = await ReadAsync(await staff.GetAsync("/api/kpi/dashboard/employee"));

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal($"Scorecard #{planning.Id} was not found.", (await ReadAsync(hidden)).GetProperty("detail").GetString());
        Assert.Equal(0, ownList.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("scorecard").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);

        Succeeded(await FinalizePlan(Succeeded(await Get<ScorecardDetailDto>(planning.Id))));
        Assert.Equal("Draft", (await ReadAsync(await staff.GetAsync(url))).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Hr_never_plans_their_own_scorecard_and_the_built_in_administrator_still_does()
    {
        var seed = await SeedAsync();
        var hana = await SeedEmployeeAsync(seed.Org, "1", "hana");
        var other = await SeedEmployeeAsync(seed.Org, "2", "omar");
        var own = await ReadAsync(await client.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = hana.Id, periodId = seed.Period.Id, items = Items((seed.First, 60m), (seed.Second, 40m)) }));
        var ownId = own.GetProperty("id").GetInt32();
        var item = own.GetProperty("items")[0];
        string[] hrGrants = [.. SetupOnlyGrants, "kpi:View", "kpi:Approve", "employees:View", "employees:Edit"];
        var hr = await SignedInAsRoleAsync("Hr", hana.Id, hrGrants);
        const string OwnPlan = "You cannot plan your own scorecard.";

        var assign = await hr.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = hana.Id, periodId = seed.Period.Id, items = Items((seed.First, 10m)) });
        var update = await hr.PutAsJsonAsync($"/api/kpi/scorecard-items/{item.GetProperty("id").GetInt32()}", new { target = 250m, weight = 60m, startDate = "2026-10-01", endDate = "2026-12-31", rowVersion = item.GetProperty("rowVersion").GetString() });
        var removeItem = await Send(hr, HttpMethod.Delete, $"/api/kpi/scorecard-items/{item.GetProperty("id").GetInt32()}", new { rowVersion = item.GetProperty("rowVersion").GetString() });
        var deleteCard = await Send(hr, HttpMethod.Delete, $"/api/kpi/scorecards/{ownId}", new { rowVersion = own.GetProperty("rowVersion").GetString() });
        var finalize = await hr.PostAsJsonAsync($"/api/kpi/scorecards/{ownId}/finalize-plan", new { rowVersion = own.GetProperty("rowVersion").GetString() });
        var bulk = await ReadAsync(await hr.PostAsJsonAsync("/api/kpi/assignments/bulk", new { periodId = seed.Period.Id, employeeIds = new[] { hana.Id, other.Id }, items = Items((seed.First, 100m)) }));
        var otherAssigned = bulk.GetProperty("assigned").EnumerateArray().Select(entry => entry.GetProperty("employeeId").GetInt32()).ToList();
        var skipped = bulk.GetProperty("skipped").EnumerateArray().Single();

        foreach (var refused in new[] { assign, update, removeItem, deleteCard, finalize })
        {
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal(OwnPlan, (await ReadAsync(refused)).GetProperty("detail").GetString());
        }

        Assert.Equal([other.Id], otherAssigned);
        Assert.Equal((hana.Id, OwnPlan), (skipped.GetProperty("employeeId").GetInt32(), skipped.GetProperty("reason").GetString()));
        Assert.Equal(2, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.ScorecardItem WHERE ScorecardId = @id AND Weight IN (60, 40)", ("@id", ownId)));
        Assert.Equal(HttpStatusCode.OK, (await hr.GetAsync($"/api/kpi/scorecards/{ownId}")).StatusCode);

        var byAdministrator = await client.PostAsJsonAsync($"/api/kpi/scorecards/{ownId}/finalize-plan", new { rowVersion = own.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, byAdministrator.StatusCode);
        Assert.Equal("Draft", (await ReadAsync(byAdministrator)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task The_list_query_has_the_paging_shape_the_sort_whitelist_and_a_search_over_http()
    {
        var seed = await SeedAsync();
        var one = await SeedEmployeeAsync(seed.Org, "1", "ria");
        var two = await SeedEmployeeAsync(seed.Org, "2", "rex");
        await SeedScorecardAsync(one.Id, seed.Period.Id, ItemOf(seed.First, weight: 100m));
        await SeedScorecardAsync(two.Id, seed.Period.Id, ItemOf(seed.First, weight: 100m));

        var page = await ReadAsync(await client.GetAsync($"/api/kpi/scorecards/query?periodId={seed.Period.Id}&sort=employeeCode:desc&pageSize=1"));
        var search = await ReadAsync(await client.GetAsync($"/api/kpi/scorecards/query?periodId={seed.Period.Id}&search={two.EmployeeCode}"));
        var badSort = await client.GetAsync("/api/kpi/scorecards/query?sort=rowVersion:asc");
        var unknownStatus = await ReadAsync(await client.GetAsync($"/api/kpi/scorecards/query?periodId={seed.Period.Id}&status=Nonsense"));
        var longSearch = await client.GetAsync($"/api/kpi/scorecards/query?search={new string('x', 101)}");

        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(two.Id, page.GetProperty("items")[0].GetProperty("employeeId").GetInt32());
        Assert.Equal(two.Id, search.GetProperty("items")[0].GetProperty("employeeId").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
        Assert.Equal(0, unknownStatus.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, longSearch.StatusCode);
    }

    // ---- Permissions ----

    [IntegrationFact]
    public async Task An_employee_cannot_assign_or_plan_and_hr_setup_alone_cannot_read_scorecards_and_an_anonymous_caller_gets_401()
    {
        var seed = await SeedAsync();
        var employee = await SeedEmployeeAsync(seed.Org);
        var card = await SeedScorecardAsync(employee.Id, seed.Period.Id, ItemOf(seed.First, weight: 100m));
        var staff = await SignedInAsRoleAsync("Employee", employee.Id, EmployeeGrants);
        var hr = await SignedInAsRoleAsync("Setup", null, SetupOnlyGrants);
        using var anonymous = factory.CreateBrowserClient(out _);
        var body = new { employeeId = employee.Id, periodId = seed.Period.Id, items = Items((seed.Second, 10m)) };

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/kpi/assignments", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/kpi/assignments/bulk", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(staff, HttpMethod.Put, $"/api/kpi/scorecard-items/{card.Items[0].Id}", new { target = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/kpi/scorecards/{card.Id}/finalize-plan", new { rowVersion = Requests.Version(card.RowVersion) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(staff, HttpMethod.Delete, $"/api/kpi/scorecards/{card.Id}", new { rowVersion = Requests.Version(card.RowVersion) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync("/api/kpi/scorecards/query")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await hr.GetAsync($"/api/kpi/scorecards/{card.Id}")).StatusCode);
        var other = await SeedEmployeeAsync(seed.Org, "9", "zoe");
        Assert.Equal(HttpStatusCode.Created, (await hr.PostAsJsonAsync("/api/kpi/assignments", new { employeeId = other.Id, periodId = seed.Period.Id, items = Items((seed.Second, 10m)) })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/kpi/scorecards/query")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/kpi/assignments", body)).StatusCode);
    }
}
