using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Employee documents (metadata CRUD, personal data) and the salary summary over HTTP, against the real database: JSON shapes,
/// error mapping, the CHECK-backed type list and the cascade with the employee. Figures are fictional DEMONSTRATION figures.
/// Rows are tagged TST- and removed by the fixture (hr.EmployeeDocument before hr.Employee).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class EmployeeExtrasHttpTests : PayrollTestBase, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient admin;

    public EmployeeExtrasHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        admin = factory.CreateBrowserClient(out _);
    }

    public override async Task InitializeAsync() => await admin.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        admin.Dispose();
        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient http, string url, string rowVersion) =>
        http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(new { rowVersion }) });

    private static object NewBody(int employeeId, string type = "Work Permit", string title = "Work permit", string fileName = "permit.pdf", string? expiryDate = "2099-01-01") =>
        new { employeeId, type, title, fileName, uploadedOn = "2026-01-15", expiryDate };

    [IntegrationFact]
    public async Task A_document_goes_through_create_read_update_and_delete_with_its_shape()
    {
        var world = await SeedPayrollAsync();

        var created = await admin.PostAsJsonAsync("/api/employee-documents", NewBody(world.Employee.Id));
        var json = await ReadAsync(created);
        var id = json.GetProperty("id").GetInt32();
        var rowVersion = json.GetProperty("rowVersion").GetString()!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/employee-documents/{id}", created.Headers.Location?.OriginalString);
        Assert.Equal(world.Employee.Id, json.GetProperty("employeeId").GetInt32());
        Assert.Equal("Work Permit", json.GetProperty("type").GetString());
        Assert.Equal("2026-01-15", json.GetProperty("uploadedOn").GetString());
        Assert.Equal("2099-01-01", json.GetProperty("expiryDate").GetString());

        var read = await ReadAsync(await admin.GetAsync($"/api/employee-documents/{id}"));
        Assert.Equal("permit.pdf", read.GetProperty("fileName").GetString());

        var page = await ReadAsync(await admin.GetAsync($"/api/employee-documents/query?employeeId={world.Employee.Id}&type=Work%20Permit&search=permit&pageIndex=0&pageSize=10"));
        Assert.Equal(1, page.GetProperty("total").GetInt32());

        var updated = await admin.PutAsJsonAsync(
            $"/api/employee-documents/{id}",
            new { employeeId = world.Employee.Id, type = "Passport", title = "Passport", fileName = "passport.pdf", uploadedOn = "2026-01-15", expiryDate = (string?)null, rowVersion });
        var updatedJson = await ReadAsync(updated);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Passport", updatedJson.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, updatedJson.GetProperty("expiryDate").ValueKind);

        var stale = await admin.PutAsJsonAsync(
            $"/api/employee-documents/{id}",
            new { employeeId = world.Employee.Id, type = "Passport", title = "Again", fileName = "passport.pdf", uploadedOn = "2026-01-15", expiryDate = (string?)null, rowVersion });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var deleted = await DeleteAsync(admin, $"/api/employee-documents/{id}", updatedJson.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/employee-documents/{id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task Document_errors_map_to_validation_not_found_and_there_is_no_unpaged_list()
    {
        var world = await SeedPayrollAsync();

        var path = await admin.PostAsJsonAsync("/api/employee-documents", NewBody(world.Employee.Id, fileName: "folder/permit.pdf"));
        var type = await admin.PostAsJsonAsync("/api/employee-documents", NewBody(world.Employee.Id, type: "Secret"));
        var employee = await admin.PostAsJsonAsync("/api/employee-documents", NewBody(employeeId: 2_000_000_000));
        var unversioned = await admin.PutAsJsonAsync("/api/employee-documents/1", NewBody(world.Employee.Id));

        Assert.Equal(HttpStatusCode.BadRequest, path.StatusCode);
        Assert.Contains("fileName", (await ReadAsync(path)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Contains("type", (await ReadAsync(type)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Contains("employeeId", (await ReadAsync(employee)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal(HttpStatusCode.BadRequest, unversioned.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.GetAsync("/api/employee-documents")).StatusCode);
    }

    [IntegrationFact]
    public async Task The_summary_is_the_demonstration_month_at_the_current_salary()
    {
        var world = await SeedPayrollAsync();

        var summary = await ReadAsync(await admin.GetAsync($"/api/employees/{world.Employee.Id}/salary-summary"));

        Assert.Equal(world.Employee.Id, summary.GetProperty("employeeId").GetInt32());
        Assert.Equal(1000m, summary.GetProperty("basic").GetDecimal());
        Assert.Equal(150m, summary.GetProperty("allowances").GetDecimal());
        Assert.Equal(1150m, summary.GetProperty("gross").GetDecimal());
        Assert.Equal(80.5m, summary.GetProperty("deductions").GetDecimal());
        Assert.Equal(1069.5m, summary.GetProperty("net").GetDecimal());
        Assert.EndsWith("-PG", summary.GetProperty("gradeCode").GetString());
        Assert.Equal(500m, summary.GetProperty("bandMin").GetDecimal());
        Assert.Equal(5000m, summary.GetProperty("bandMax").GetDecimal());
        Assert.Equal(11, summary.GetProperty("bandPosition").GetInt32());
        Assert.Equal(6, summary.GetProperty("assumptions").GetArrayLength());

    }

    [IntegrationFact]
    public async Task The_summary_of_a_missing_employee_or_one_without_salary_is_not_found()
    {
        var world = await SeedPayrollAsync();
        var withoutSalary = await SeedSecondEmployeeAsync(world, withSalary: false);

        var missing = await admin.GetAsync("/api/employees/2000000000/salary-summary");
        var none = await admin.GetAsync($"/api/employees/{withoutSalary.Id}/salary-summary");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        Assert.Equal(
            $"{withoutSalary.FirstName} {withoutSalary.LastName} has no salary assignment yet.",
            (await ReadAsync(none)).GetProperty("detail").GetString());
    }
}
