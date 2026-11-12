using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Components;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The payroll routes over HTTP: routing, JSON bodies and shapes, the command bodies, the error mapping (400 with field errors,
/// 404, 409) and the separation of duties between two signed-in users. All figures are fictional DEMONSTRATION figures.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class PayrollHttpTests : PayrollTestBase, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient processor;
    private readonly HttpClient approver;

    public PayrollHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        processor = factory.CreateBrowserClient(out _);
        approver = factory.CreateBrowserClient(out _);
    }

    public override async Task InitializeAsync()
    {
        await processor.SignInAsync(TestAdministrator.Account);
        await approver.SignInAsync(await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "approver")));
    }

    protected override void Cleanup()
    {
        processor.Dispose();
        approver.Dispose();
        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string url, object body) => http.PostAsJsonAsync(url, body);

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient http, string url, string rowVersion) =>
        http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(new { rowVersion }) });

    private async Task<PayrollRunDto> ProcessedOverHttpAsync(int month = 1)
    {
        var created = await processor.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var run = (await created.Content.ReadFromJsonAsync<PayrollRunDto>())!;
        var processed = await PostAsync(processor, $"/api/payroll-runs/{run.Id}/process", new { rowVersion = Requests.Version(run.RowVersion) });
        Assert.Equal(HttpStatusCode.OK, processed.StatusCode);
        return (await processed.Content.ReadFromJsonAsync<PayrollRunDto>())!;
    }

    [IntegrationFact]
    public async Task A_run_goes_through_process_approve_and_mark_paid_with_the_command_bodies_and_two_users()
    {
        await SeedPayrollAsync();

        var created = await processor.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month = 1 });
        var run = (await created.Content.ReadFromJsonAsync<PayrollRunDto>())!;
        var createdJson = await ReadAsync(await processor.GetAsync($"/api/payroll-runs/{run.Id}"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/payroll-runs/{run.Id}", created.Headers.Location?.OriginalString);
        Assert.Equal("Draft", createdJson.GetProperty("status").GetString());
        Assert.Equal("January 1999", createdJson.GetProperty("periodName").GetString());
        Assert.Equal("1999-01-31", createdJson.GetProperty("payDate").GetString());
        Assert.Equal(JsonValueKind.Array, createdJson.GetProperty("warnings").ValueKind);
        Assert.Equal(JsonValueKind.String, createdJson.GetProperty("rowVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, createdJson.GetProperty("processedBy").ValueKind);

        var processed = await PostAsync(processor, $"/api/payroll-runs/{run.Id}/process", new { rowVersion = Requests.Version(run.RowVersion) });
        var processedDto = (await processed.Content.ReadFromJsonAsync<PayrollRunDto>())!;
        Assert.Equal(HttpStatusCode.OK, processed.StatusCode);
        Assert.Equal("Pending Approval", processedDto.Status);
        Assert.Equal(1069.5m, processedDto.TotalNet);

        // The user who processed the run cannot approve it.
        var selfApproval = await PostAsync(processor, $"/api/payroll-runs/{run.Id}/approve", new { rowVersion = Requests.Version(processedDto.RowVersion) });
        Assert.Equal(HttpStatusCode.Conflict, selfApproval.StatusCode);
        var problem = await ReadAsync(selfApproval);
        Assert.Equal("Conflict", problem.GetProperty("code").GetString());
        Assert.Contains("cannot approve it", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);

        var approved = await PostAsync(approver, $"/api/payroll-runs/{run.Id}/approve", new { rowVersion = Requests.Version(processedDto.RowVersion) });
        var approvedDto = (await approved.Content.ReadFromJsonAsync<PayrollRunDto>())!;
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Approved", approvedDto.Status);
        Assert.Equal("TST- Test Person", approvedDto.ApprovedBy);

        var paid = await PostAsync(approver, $"/api/payroll-runs/{run.Id}/mark-paid", new { rowVersion = Requests.Version(approvedDto.RowVersion) });
        var paidDto = (await paid.Content.ReadFromJsonAsync<PayrollRunDto>())!;
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("Paid", paidDto.Status);
        Assert.NotNull(paidDto.PaidOn);

        var back = await PostAsync(approver, $"/api/payroll-runs/{run.Id}/return-to-draft", new { rowVersion = Requests.Version(paidDto.RowVersion), reason = "Too late" });
        Assert.Equal(HttpStatusCode.Conflict, back.StatusCode);
    }

    [IntegrationFact]
    public async Task Payslips_are_read_only_paged_and_show_only_the_masked_account()
    {
        var world = await SeedPayrollAsync();
        var run = await ProcessedOverHttpAsync();

        var page = await ReadAsync(await processor.GetAsync($"/api/payslips/query?runId={run.Id}&pageSize=10&sort=netSalary:desc&search=Mia"));
        var item = Assert.Single(page.GetProperty("items").EnumerateArray().ToList());
        var raw = item.GetRawText();

        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("•••• 7890", item.GetProperty("accountMasked").GetString());
        Assert.DoesNotContain(AccountNumber, raw, StringComparison.Ordinal);
        Assert.Equal(1069.5m, item.GetProperty("netSalary").GetDecimal());
        Assert.Equal(2, item.GetProperty("earnings").GetArrayLength());
        Assert.Equal("10% of basic", item.GetProperty("earnings")[0].GetProperty("basis").GetString());
        Assert.Equal(world.Employee.Id, item.GetProperty("employeeId").GetInt32());

        var id = item.GetProperty("id").GetInt32();
        var detail = await ReadAsync(await processor.GetAsync($"/api/payslips/{id}"));
        Assert.Equal("•••• 7890", detail.GetProperty("accountMasked").GetString());
        Assert.DoesNotContain(AccountNumber, detail.GetRawText(), StringComparison.Ordinal);

        // No list of every payslip, and nothing writes one.
        Assert.NotEqual(HttpStatusCode.OK, (await processor.GetAsync("/api/payslips")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await processor.PostAsJsonAsync("/api/payslips", new { })).StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent, (await DeleteAsync(processor, $"/api/payslips/{id}", "AAAAAAAAAAA=")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await processor.GetAsync("/api/payslips/2000000")).StatusCode);

        // A page is never larger than the limit, however large the caller asks.
        var big = await processor.GetFromJsonAsync<PagedResult<PayslipDto>>("/api/payslips/query?pageSize=100000");
        Assert.Equal(200, big!.PageSize);
    }

    [IntegrationFact]
    public async Task Salary_assignments_have_no_unpaged_list_and_a_history_is_a_filtered_page()
    {
        var world = await SeedPayrollAsync();

        var created = await processor.PostAsJsonAsync(
            "/api/salary-assignments",
            new { employeeId = world.Employee.Id, structureId = world.Structure.Id, basicSalary = 1800m, effectiveFrom = "2001-01-01", remarks = "Raise" });
        var assignment = (await created.Content.ReadFromJsonAsync<SalaryAssignmentDto>())!;
        var history = await processor.GetFromJsonAsync<PagedResult<SalaryAssignmentDto>>(
            $"/api/salary-assignments/query?employeeId={world.Employee.Id}&sort=effectiveFrom:desc");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await processor.GetAsync("/api/salary-assignments")).StatusCode);
        Assert.Equal([assignment.Id, world.Assignment.Id], history!.Items.Select(item => item.Id));

        var outside = await processor.PostAsJsonAsync(
            "/api/salary-assignments",
            new { employeeId = world.Employee.Id, structureId = world.Structure.Id, basicSalary = 9000m, effectiveFrom = "2002-01-01" });
        var problem = await ReadAsync(outside);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal("Validation", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("basicSalary", out _));

        var deleted = await DeleteAsync(processor, $"/api/salary-assignments/{assignment.Id}", Requests.Version(assignment.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var last = await DeleteAsync(processor, $"/api/salary-assignments/{world.Assignment.Id}", Requests.Version(world.Assignment.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, last.StatusCode);
    }

    [IntegrationFact]
    public async Task Errors_map_to_validation_not_found_and_conflict_with_field_errors()
    {
        await SeedPayrollAsync();

        var badMonth = await processor.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month = 13 });
        var future = await processor.PostAsJsonAsync("/api/payroll-runs", new { year = 2999, month = 1 });
        var run = (await (await processor.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month = 1 })).Content.ReadFromJsonAsync<PayrollRunDto>())!;
        var duplicate = await processor.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month = 1 });
        var noVersion = await PostAsync(processor, $"/api/payroll-runs/{run.Id}/process", new { });
        var missing = await PostAsync(processor, "/api/payroll-runs/2000000/process", new { rowVersion = Requests.Version(run.RowVersion) });
        var stale = await PostAsync(processor, $"/api/payroll-runs/{run.Id}/process", new { rowVersion = "AAAAAAAAAAA=" });
        var approveDraft = await PostAsync(approver, $"/api/payroll-runs/{run.Id}/approve", new { rowVersion = Requests.Version(run.RowVersion) });

        Assert.Equal(HttpStatusCode.BadRequest, badMonth.StatusCode);
        Assert.True((await ReadAsync(badMonth)).GetProperty("errors").TryGetProperty("month", out _));
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal("A payroll period cannot be in the future.", (await ReadAsync(future)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.True((await ReadAsync(duplicate)).GetProperty("errors").TryGetProperty("month", out _));
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        Assert.True((await ReadAsync(noVersion)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, approveDraft.StatusCode);
    }

    [IntegrationFact]
    public async Task A_run_returns_to_draft_with_a_reason_and_a_draft_can_be_deleted_with_a_body()
    {
        await SeedPayrollAsync();
        var processed = await ProcessedOverHttpAsync();

        var returned = await PostAsync(approver, $"/api/payroll-runs/{processed.Id}/return-to-draft", new { rowVersion = Requests.Version(processed.RowVersion), reason = "Wrong pay date" });
        var draft = (await returned.Content.ReadFromJsonAsync<PayrollRunDto>())!;

        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal("Draft", draft.Status);
        Assert.Empty(draft.Warnings);
        Assert.Equal(0, draft.EmployeeCount);
        var slips = await processor.GetFromJsonAsync<PagedResult<PayslipDto>>($"/api/payslips/query?runId={processed.Id}");
        Assert.Empty(slips!.Items);

        var updated = await processor.PutAsJsonAsync(
            $"/api/payroll-runs/{processed.Id}",
            new { year = Year, month = 1, payDate = "1999-01-28", rowVersion = Requests.Version(draft.RowVersion) });
        var updatedDto = (await updated.Content.ReadFromJsonAsync<PayrollRunDto>())!;
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(new DateOnly(1999, 1, 28), updatedDto.PayDate);

        var deleted = await DeleteAsync(processor, $"/api/payroll-runs/{processed.Id}", Requests.Version(updatedDto.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await processor.GetAsync($"/api/payroll-runs/{processed.Id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task Components_and_structures_have_the_organization_style_routes_and_a_delete_that_needs_no_payroll_delete_permission()
    {
        var world = await SeedPayrollAsync();

        var created = await processor.PostAsJsonAsync(
            "/api/salary-components",
            new { code = Code("BONUS"), name = Name("Bonus"), type = "Earning", calculation = "Fixed", value = 25m, status = "Active" });
        var json = await ReadAsync(created);
        var id = json.GetProperty("id").GetInt32();
        var version = json.GetProperty("rowVersion").GetString()!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Fixed", json.GetProperty("calculation").GetString());
        var deactivated = await PostAsync(processor, $"/api/salary-components/{id}/deactivate", new { rowVersion = version });
        Assert.Equal("Inactive", (await ReadAsync(deactivated)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await processor.GetAsync("/api/salary-components")).StatusCode);
        var query = await processor.GetFromJsonAsync<PagedResult<SalaryComponentDto>>($"/api/salary-components/query?type=Earning&search={Tag}&sort=code:asc");
        Assert.Equal(3, query!.Total);

        var inUse = await DeleteAsync(processor, $"/api/salary-components/{world.Structure.ComponentIds[0]}", "AAAAAAAAAAA=");
        Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
        var current = await ReadAsync(await processor.GetAsync($"/api/salary-components/{id}"));
        var deleted = await DeleteAsync(processor, $"/api/salary-components/{id}", current.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var noEarning = await processor.PostAsJsonAsync(
            "/api/salary-structures",
            new { code = Code("NOEARN"), name = Name("No earning"), componentIds = new[] { world.Structure.ComponentIds[2] }, status = "Active" });
        Assert.Equal(HttpStatusCode.BadRequest, noEarning.StatusCode);
        Assert.True((await ReadAsync(noEarning)).GetProperty("errors").TryGetProperty("componentIds", out _));
    }
}
