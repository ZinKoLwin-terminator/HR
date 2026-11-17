using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.RatingBands;
using Apex.Hrms.Application.Performance.Templates;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The KPI master data routes over HTTP: routing, JSON bodies and shapes, 201 with Location, the command and delete bodies,
/// the error mapping (400 with field errors, 404, 409), the sort whitelist and the permissions of an HR administrator and of
/// an employee who only holds <c>kpi:View</c> and <c>kpi:Create</c>.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class KpiHttpTests : DatabaseTest, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient client;
    private readonly List<HttpClient> others = [];

    public KpiHttpTests()
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

    private async Task<T> CreatedAsync<T>(string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string url, object? body) => http.PostAsJsonAsync(url, body);

    private Task<HttpResponseMessage> DeleteAsync(string url, string? rowVersion) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(new { rowVersion }) });

    private async Task<T> MoveAsync<T>(EvaluationPeriodDto period, string action)
    {
        var response = await PostAsync(client, $"/api/kpi/periods/{period.Id}/{action}", new { rowVersion = Requests.Version(period.RowVersion) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    // ---- Categories, templates and rating bands ----

    [IntegrationFact]
    public async Task A_category_has_the_organization_routes_and_shapes_and_201_with_a_location()
    {
        var created = await client.PostAsJsonAsync("/api/kpi/categories", Requests.KpiCategory());
        var category = (await created.Content.ReadFromJsonAsync<KpiCategoryDto>())!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/kpi/categories/{category.Id}", created.Headers.Location?.OriginalString);
        Assert.Equal(8, category.RowVersion.Length);
        var json = await ReadAsync(await client.GetAsync($"/api/kpi/categories/{category.Id}"));
        Assert.Equal(Code("PROD"), json.GetProperty("code").GetString());
        Assert.Equal(string.Empty, json.GetProperty("description").GetString());
        Assert.Equal("Active", json.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/kpi/categories")).StatusCode);

        var query = await client.GetFromJsonAsync<PagedResult<KpiCategoryDto>>($"/api/kpi/categories/query?search={Tag}&sort=code:desc&status=Active&pageSize=5");
        Assert.Equal(category.Id, Assert.Single(query!.Items).Id);

        var inactive = await PostAsync(client, $"/api/kpi/categories/{category.Id}/deactivate", new { rowVersion = Requests.Version(category.RowVersion) });
        Assert.Equal("Inactive", (await ReadAsync(inactive)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task A_bad_category_is_a_400_with_field_errors_a_missing_one_a_404_and_a_stale_update_a_409()
    {
        var category = await CreatedAsync<KpiCategoryDto>("/api/kpi/categories", Requests.KpiCategory());

        var badCode = await client.PostAsJsonAsync("/api/kpi/categories", Requests.KpiCategory("bad code", "Other"));
        var duplicate = await client.PostAsJsonAsync("/api/kpi/categories", Requests.KpiCategory("PROD", "Another"));
        var missing = await client.GetAsync("/api/kpi/categories/999999999");
        var noVersion = await client.PutAsJsonAsync($"/api/kpi/categories/{category.Id}", new { code = category.Code, name = category.Name, description = "x" });
        var first = await client.PutAsJsonAsync($"/api/kpi/categories/{category.Id}", Requests.KpiCategoryUpdate(category, "first"));
        var stale = await client.PutAsJsonAsync($"/api/kpi/categories/{category.Id}", Requests.KpiCategoryUpdate(category, "second"));

        Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);
        var problem = await ReadAsync(badCode);
        Assert.Equal("Validation", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("code", out _));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.True((await ReadAsync(duplicate)).GetProperty("errors").TryGetProperty("code", out _));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        Assert.True((await ReadAsync(noVersion)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("Conflict", (await ReadAsync(stale)).GetProperty("code").GetString());
    }

    [IntegrationFact]
    public async Task A_template_travels_as_json_with_the_texts_of_the_checks_and_a_bad_rule_is_a_400_on_the_request_field()
    {
        var category = await CreatedAsync<KpiCategoryDto>("/api/kpi/categories", Requests.KpiCategory());
        var template = await CreatedAsync<KpiTemplateDto>(
            "/api/kpi/templates",
            Requests.KpiTemplate(category.Id, "LOW-1", "Defects", direction: "Lower Is Better", scoring: "Capped Percentage", target: 5m, cap: 120m));

        var json = await ReadAsync(await client.GetAsync($"/api/kpi/templates/{template.Id}"));
        Assert.Equal("Lower Is Better", json.GetProperty("direction").GetString());
        Assert.Equal("Capped Percentage", json.GetProperty("scoringMethod").GetString());
        Assert.Equal("Monthly", json.GetProperty("frequency").GetString());
        Assert.Equal(120m, json.GetProperty("maxAchievementPct").GetDecimal());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("departmentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("rangeMin").ValueKind);

        var bad = await client.PostAsJsonAsync(
            "/api/kpi/templates",
            Requests.KpiTemplate(category.Id, "LOW-2", direction: "Lower Is Better", scoring: "Capped Percentage", target: 5m, cap: null));
        var badTarget = await client.PostAsJsonAsync("/api/kpi/templates", Requests.KpiTemplate(category.Id, "ZERO", target: 0m));
        var badText = await client.PostAsJsonAsync("/api/kpi/templates", Requests.KpiTemplate(category.Id, "TXT", direction: "Up"));

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await ReadAsync(bad)).GetProperty("errors").TryGetProperty("maxAchievementPct", out _));
        Assert.True((await ReadAsync(badTarget)).GetProperty("errors").TryGetProperty("defaultTarget", out _));
        Assert.True((await ReadAsync(badText)).GetProperty("errors").TryGetProperty("direction", out _));

        var byCategory = await client.GetFromJsonAsync<PagedResult<KpiTemplateDto>>($"/api/kpi/templates/query?categoryId={category.Id}&scoringMethod=Capped%20Percentage");
        Assert.Equal(template.Id, Assert.Single(byCategory!.Items).Id);
    }

    [IntegrationFact]
    public async Task Deleting_needs_the_row_version_in_the_body_and_a_used_category_is_a_409_with_the_count()
    {
        var category = await CreatedAsync<KpiCategoryDto>("/api/kpi/categories", Requests.KpiCategory());
        var free = await CreatedAsync<KpiCategoryDto>("/api/kpi/categories", Requests.KpiCategory("FREE", "Free"));
        await CreatedAsync<KpiTemplateDto>("/api/kpi/templates", Requests.KpiTemplate(category.Id));

        var withoutBody = await client.DeleteAsync($"/api/kpi/categories/{free.Id}");
        var blocked = await DeleteAsync($"/api/kpi/categories/{category.Id}", Requests.Version(category.RowVersion));
        var deleted = await DeleteAsync($"/api/kpi/categories/{free.Id}", Requests.Version(free.RowVersion));

        Assert.Equal(HttpStatusCode.BadRequest, withoutBody.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("1 template(s)", (await ReadAsync(blocked)).GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/kpi/categories/{free.Id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task A_rating_band_has_the_same_routes_and_a_sort_outside_the_whitelist_is_a_400()
    {
        var band = await CreatedAsync<RatingBandDto>("/api/kpi/rating-bands", Requests.RatingBand("Top", 455.5m));

        var query = await client.GetFromJsonAsync<PagedResult<RatingBandDto>>($"/api/kpi/rating-bands/query?search={Tag}&sort=minScore:desc");
        var badSort = await client.GetAsync("/api/kpi/rating-bands/query?sort=rowVersion:asc");
        var duplicate = await client.PostAsJsonAsync("/api/kpi/rating-bands", Requests.RatingBand("Other", 455.5m));
        var outOfRange = await client.PostAsJsonAsync("/api/kpi/rating-bands", Requests.RatingBand("Too high", 501m));
        var inactive = await PostAsync(client, $"/api/kpi/rating-bands/{band.Id}/deactivate", new { rowVersion = Requests.Version(band.RowVersion) });

        Assert.Equal(band.Id, Assert.Single(query!.Items).Id);
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
        Assert.True((await ReadAsync(badSort)).GetProperty("errors").TryGetProperty("sort", out _));
        Assert.True((await ReadAsync(duplicate)).GetProperty("errors").TryGetProperty("minScore", out _));
        Assert.True((await ReadAsync(outOfRange)).GetProperty("errors").TryGetProperty("minScore", out _));
        Assert.Equal("Inactive", (await ReadAsync(inactive)).GetProperty("status").GetString());
    }

    // ---- Periods ----

    [IntegrationFact]
    public async Task A_period_goes_through_its_commands_over_http_and_an_out_of_order_command_is_a_409()
    {
        var org = await SeedOrganizationAsync();
        var created = await client.PostAsJsonAsync("/api/kpi/periods", Requests.Period(org.Company.Id));
        var draft = (await created.Content.ReadFromJsonAsync<EvaluationPeriodDto>())!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/kpi/periods/{draft.Id}", created.Headers.Location?.OriginalString);
        Assert.Equal("Draft", draft.Status);

        var early = await PostAsync(client, $"/api/kpi/periods/{draft.Id}/complete", new { rowVersion = Requests.Version(draft.RowVersion) });
        var noBody = await client.PostAsync($"/api/kpi/periods/{draft.Id}/activate", content: null);
        var active = await MoveAsync<EvaluationPeriodDto>(draft, "activate");
        var completed = await MoveAsync<EvaluationPeriodDto>(active, "complete");
        var closed = await MoveAsync<EvaluationPeriodDto>(completed, "close");

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);
        Assert.Equal(["Active", "Completed", "Closed"], [active.Status, completed.Status, closed.Status]);
        var json = await ReadAsync(await client.GetAsync($"/api/kpi/periods/{draft.Id}"));
        Assert.Equal("Closed", json.GetProperty("status").GetString());
        Assert.Equal("2026-10-01", json.GetProperty("startDate").GetString());
        Assert.Equal("Quarterly", json.GetProperty("periodType").GetString());
        Assert.False(json.GetProperty("requiresHrReview").GetBoolean());
    }

    [IntegrationFact]
    public async Task An_overlapping_period_is_a_409_naming_the_clash_and_bad_dates_are_400s_with_field_errors()
    {
        var org = await SeedOrganizationAsync();
        await CreatedAsync<EvaluationPeriodDto>("/api/kpi/periods", Requests.Period(org.Company.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31"));

        var touching = await client.PostAsJsonAsync("/api/kpi/periods", Requests.Period(org.Company.Id, "Touching", "Quarterly", "2026-12-31", "2027-03-31"));
        var adjacent = await client.PostAsJsonAsync("/api/kpi/periods", Requests.Period(org.Company.Id, "2027 Q1", "Quarterly", "2027-01-01", "2027-03-31"));
        var inverted = await client.PostAsJsonAsync("/api/kpi/periods", Requests.Period(org.Company.Id, "Inverted", "Custom", "2026-05-02", "2026-05-01"));
        var earlyDue = await client.PostAsJsonAsync("/api/kpi/periods", Requests.Period(org.Company.Id, "Early due", "Custom", "2026-05-01", "2026-05-31", due: "2026-04-30"));
        var noCompany = await client.PostAsJsonAsync("/api/kpi/periods", Requests.Period(999999999));

        Assert.Equal(HttpStatusCode.Conflict, touching.StatusCode);
        Assert.Contains(Name("2026 Q4"), (await ReadAsync(touching)).GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, adjacent.StatusCode);
        Assert.True((await ReadAsync(inverted)).GetProperty("errors").TryGetProperty("endDate", out _));
        Assert.True((await ReadAsync(earlyDue)).GetProperty("errors").TryGetProperty("submissionDueDate", out _));
        Assert.True((await ReadAsync(noCompany)).GetProperty("errors").TryGetProperty("companyId", out _));
    }

    [IntegrationFact]
    public async Task An_active_period_refuses_to_change_its_dates_and_a_non_draft_period_cannot_be_deleted_over_http()
    {
        var org = await SeedOrganizationAsync();
        var draft = await CreatedAsync<EvaluationPeriodDto>("/api/kpi/periods", Requests.Period(org.Company.Id));
        var active = await MoveAsync<EvaluationPeriodDto>(draft, "activate");

        var renamed = await client.PutAsJsonAsync($"/api/kpi/periods/{active.Id}", Requests.PeriodUpdate(active, Name("2026 Q4 final")));
        var renamedDto = (await renamed.Content.ReadFromJsonAsync<EvaluationPeriodDto>())!;
        var locked = await client.PutAsJsonAsync($"/api/kpi/periods/{active.Id}", Requests.PeriodUpdate(renamedDto, end: new DateOnly(2026, 12, 30)));
        var delete = await DeleteAsync($"/api/kpi/periods/{active.Id}", Requests.Version(renamedDto.RowVersion));

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("only its name and submission due date", (await ReadAsync(locked)).GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [IntegrationFact]
    public async Task A_draft_period_can_be_deleted_over_http()
    {
        var org = await SeedOrganizationAsync();
        var draft = await CreatedAsync<EvaluationPeriodDto>("/api/kpi/periods", Requests.Period(org.Company.Id));

        var deleted = await DeleteAsync($"/api/kpi/periods/{draft.Id}", Requests.Version(draft.RowVersion));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/kpi/periods/{draft.Id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task Periods_are_queried_with_the_documented_filters()
    {
        var org = await SeedOrganizationAsync();
        var q4 = await CreatedAsync<EvaluationPeriodDto>("/api/kpi/periods", Requests.Period(org.Company.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31"));
        await CreatedAsync<EvaluationPeriodDto>("/api/kpi/periods", Requests.Period(org.Company.Id, "2027 Q1", "Quarterly", "2027-01-01", "2027-03-31"));
        await MoveAsync<EvaluationPeriodDto>(q4, "activate");

        var all = await client.GetFromJsonAsync<PagedResult<EvaluationPeriodDto>>($"/api/kpi/periods/query?companyId={org.Company.Id}&sort=startDate:asc");
        var byYear = await client.GetFromJsonAsync<PagedResult<EvaluationPeriodDto>>($"/api/kpi/periods/query?companyId={org.Company.Id}&year=2027");
        var byStatus = await client.GetFromJsonAsync<PagedResult<EvaluationPeriodDto>>($"/api/kpi/periods/query?companyId={org.Company.Id}&status=Active&periodType=Quarterly");
        var badSort = await client.GetAsync("/api/kpi/periods/query?sort=submissionDueDate:asc");

        Assert.Equal([Name("2026 Q4"), Name("2027 Q1")], all!.Items.Select(p => p.Name));
        Assert.Equal([Name("2027 Q1")], byYear!.Items.Select(p => p.Name));
        Assert.Equal([Name("2026 Q4")], byStatus!.Items.Select(p => p.Name));
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
    }

    // ---- Permissions ----

    private async Task<HttpClient> EmployeeClientAsync()
    {
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "employee"), roleName: "Employee");
        var employeeClient = factory.CreateBrowserClient(out _);
        others.Add(employeeClient);
        await employeeClient.SignInAsync(account);
        return employeeClient;
    }

    [IntegrationFact]
    public async Task An_employee_can_pick_a_period_through_the_options_route_but_cannot_use_the_setup_routes()
    {
        var org = await SeedOrganizationAsync();
        var draft = await CreatedAsync<EvaluationPeriodDto>("/api/kpi/periods", Requests.Period(org.Company.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31"));
        var visible = await MoveAsync<EvaluationPeriodDto>(
            await CreatedAsync<EvaluationPeriodDto>("/api/kpi/periods", Requests.Period(org.Company.Id, "2027 Q1", "Quarterly", "2027-01-01", "2027-03-31", hr: true)),
            "activate");
        var employee = await EmployeeClientAsync();

        var options = await employee.GetAsync($"/api/kpi/periods/options?companyId={org.Company.Id}");
        var query = await employee.GetAsync("/api/kpi/periods/query");
        var templates = await employee.GetAsync("/api/kpi/templates/query");
        var bands = await employee.GetAsync("/api/kpi/rating-bands");
        var create = await employee.PostAsJsonAsync("/api/kpi/periods", Requests.Period(org.Company.Id, "Mine", "Custom", "2028-01-01", "2028-01-31"));
        var activate = await PostAsync(employee, $"/api/kpi/periods/{draft.Id}/activate", new { rowVersion = Requests.Version(draft.RowVersion) });

        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var items = (await ReadAsync(options)).EnumerateArray().ToList();
        var only = Assert.Single(items);
        Assert.Equal(visible.Id, only.GetProperty("id").GetInt32());
        Assert.Equal(Name("2027 Q1"), only.GetProperty("name").GetString());
        Assert.Equal("Active", only.GetProperty("status").GetString());
        Assert.True(only.GetProperty("requiresHrReview").GetBoolean());
        Assert.Equal("2027-01-01", only.GetProperty("startDate").GetString());
        Assert.False(only.TryGetProperty("rowVersion", out _));
        Assert.Equal(HttpStatusCode.Forbidden, query.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, templates.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bands.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, activate.StatusCode);
    }

    [IntegrationFact]
    public async Task An_anonymous_caller_gets_401_on_every_kpi_route()
    {
        using var anonymous = factory.CreateBrowserClient(out _);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/kpi/periods/options")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/kpi/categories/query")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/kpi/templates")).StatusCode);
    }
}
