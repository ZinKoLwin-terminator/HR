using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The employee routes over HTTP: which response holds which field (security review H1/H2), the sub-resources, the
/// commands with their bodies, and the absence of an unpaged list.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class EmployeeHttpTests : DatabaseTest, IDisposable
{
    private const string NationalId = "NRC-TEST-000001";
    private const string PersonalEmail = "mia.personal@example.test";

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public EmployeeHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        client = factory.CreateClient();
    }

    protected override void Cleanup()
    {
        client.Dispose();
        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private async Task<EmployeeDto> CreateOverHttpAsync(string? codeSuffix = "1")
    {
        var org = await SeedOrganizationAsync();
        var response = await client.PostAsJsonAsync(
            "/api/employees",
            Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, codeSuffix));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<EmployeeDto>())!;
        Assert.Equal($"/api/employees/{created.Id}", response.Headers.Location?.OriginalString);
        return created;
    }

    private static async Task<string> BodyAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    private static void AssertNoPersonalData(string json)
    {
        var lower = json.ToLowerInvariant();
        foreach (var name in new[] { "nationalid", "dateofbirth", "personalemail", "alternatephone", "\"phone\"", "address", "emergencycontact", "maritalstatus", "nationality", "accountnumber\":\"1" })
        {
            Assert.DoesNotContain(name, lower, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(NationalId.ToLowerInvariant(), lower, StringComparison.Ordinal);
        Assert.DoesNotContain(PersonalEmail, lower, StringComparison.Ordinal);
        Assert.DoesNotContain("555 0101", lower, StringComparison.Ordinal);
        Assert.DoesNotContain("12 test street", lower, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task Create_list_and_detail_responses_carry_no_personal_data()
    {
        var created = await CreateOverHttpAsync();

        var detail = await BodyAsync(await client.GetAsync($"/api/employees/{created.Id}"));
        var list = await BodyAsync(await client.GetAsync($"/api/employees/query?pageSize=200&search={Tag}"));
        var createdJson = JsonSerializer.Serialize(created);

        Assert.Contains("\"employeeCode\"", detail, StringComparison.Ordinal);
        Assert.Contains("\"workEmail\"", list, StringComparison.Ordinal);
        AssertNoPersonalData(detail);
        AssertNoPersonalData(list);
        AssertNoPersonalData(createdJson);
    }

    [IntegrationFact]
    public async Task Personal_data_is_only_on_the_personal_endpoint()
    {
        var created = await CreateOverHttpAsync();

        var response = await client.GetAsync($"/api/employees/{created.Id}/personal");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var personal = (await response.Content.ReadFromJsonAsync<EmployeePersonalDto>())!;
        Assert.Equal(NationalId, personal.NationalId);
        Assert.Equal(PersonalEmail, personal.PersonalEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/employees/2000000/personal")).StatusCode);
    }

    [IntegrationFact]
    public async Task There_is_no_unpaged_employee_list()
    {
        await CreateOverHttpAsync();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [IntegrationFact]
    public async Task The_query_limits_the_page_size_to_200()
    {
        var response = await client.GetAsync("/api/employees/query?pageSize=100000");

        var page = (await response.Content.ReadFromJsonAsync<PagedResult<EmployeeListItemDto>>())!;
        Assert.Equal(200, page.PageSize);
    }

    [IntegrationFact]
    public async Task The_bank_account_is_a_sub_resource_that_shows_the_full_number_only_there()
    {
        var created = await CreateOverHttpAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/employees/{created.Id}/bank-account")).StatusCode);

        var put = await client.PutAsJsonAsync(
            $"/api/employees/{created.Id}/bank-account",
            new BankAccountUpdateRequest("Sample Bank", null, "Mia Testwell", "1234567890", null));
        var bank = (await put.Content.ReadFromJsonAsync<BankAccountDto>())!;
        var detail = await BodyAsync(await client.GetAsync($"/api/employees/{created.Id}"));
        var read = (await client.GetFromJsonAsync<BankAccountDto>($"/api/employees/{created.Id}/bank-account"))!;

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("1234567890", bank.AccountNumber);
        Assert.Equal("1234567890", read.AccountNumber);
        Assert.Contains("\"bankAccountMasked\":\"******7890\"", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567890", detail, StringComparison.Ordinal);

        var missingVersion = await client.PutAsJsonAsync(
            $"/api/employees/{created.Id}/bank-account",
            new BankAccountUpdateRequest("Sample Bank", null, "Mia Testwell", "9876543210", null));
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
        Assert.Contains("rowVersion", await BodyAsync(missingVersion), StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task Exit_is_a_command_with_its_own_body_and_status_never_changes_through_put()
    {
        var created = await CreateOverHttpAsync();
        var version = Convert.ToBase64String(created.RowVersion);

        var noBody = await client.PostAsync($"/api/employees/{created.Id}/exit", null);
        var wrongStatus = await client.PostAsJsonAsync($"/api/employees/{created.Id}/exit", new { rowVersion = version, exitDate = "2026-10-31", status = "Active" });
        var exit = await client.PostAsJsonAsync($"/api/employees/{created.Id}/exit", new { rowVersion = version, exitDate = "2026-10-31", status = "Resigned" });

        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongStatus.StatusCode);
        Assert.Contains("status", await BodyAsync(wrongStatus), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, exit.StatusCode);
        var exited = (await exit.Content.ReadFromJsonAsync<EmployeeDto>())!;
        Assert.Equal("Resigned", exited.EmploymentStatus);
        Assert.Equal(new DateOnly(2026, 10, 31), exited.ExitDate);

        // A status in a PUT body is ignored.
        var putBody = new Dictionary<string, object?>
        {
            ["employeeCode"] = exited.EmployeeCode,
            ["firstName"] = exited.FirstName,
            ["lastName"] = exited.LastName,
            ["gender"] = exited.Gender,
            ["workEmail"] = exited.WorkEmail,
            ["companyId"] = exited.CompanyId,
            ["branchId"] = exited.BranchId,
            ["departmentId"] = exited.DepartmentId,
            ["positionId"] = exited.PositionId,
            ["jobGradeId"] = exited.JobGradeId,
            ["employmentType"] = exited.EmploymentType,
            ["joiningDate"] = exited.JoiningDate,
            ["employmentStatus"] = "Active",
            ["rowVersion"] = Convert.ToBase64String(exited.RowVersion),
        };
        var put = await client.PutAsJsonAsync($"/api/employees/{exited.Id}", putBody);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var afterPut = (await put.Content.ReadFromJsonAsync<EmployeeDto>())!;
        Assert.Equal("Resigned", afterPut.EmploymentStatus);

        var reinstate = await client.PostAsJsonAsync(
            $"/api/employees/{exited.Id}/reinstate",
            new { rowVersion = Convert.ToBase64String(afterPut.RowVersion) });
        Assert.Equal(HttpStatusCode.OK, reinstate.StatusCode);
        Assert.Equal("Active", (await reinstate.Content.ReadFromJsonAsync<EmployeeDto>())!.EmploymentStatus);
    }

    [IntegrationFact]
    public async Task A_duplicate_work_email_is_a_400_on_workEmail_and_delete_takes_the_row_version_in_the_body()
    {
        var created = await CreateOverHttpAsync();
        var duplicate = await client.PostAsJsonAsync(
            "/api/employees",
            Requests.Employee(created.CompanyId, created.BranchId, created.DepartmentId, created.PositionId, created.JobGradeId, "2", "mia"));
        var problem = JsonDocument.Parse(await BodyAsync(duplicate)).RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal("Work email must be unique.", problem.GetProperty("errors").GetProperty("workEmail")[0].GetString());

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/employees/{created.Id}")
        {
            Content = JsonContent.Create(new { rowVersion = Convert.ToBase64String(created.RowVersion) }),
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/employees/{created.Id}")).StatusCode);
    }
}
