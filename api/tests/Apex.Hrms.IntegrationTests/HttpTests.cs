using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The whole stack through HTTP: routing, JSON, handlers, EF and SQL Server. The in-process host writes through its own DbContext and commits; the tag of the test removes those rows afterwards.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class HttpTests : DatabaseTest, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public HttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        client = factory.CreateBrowserClient(out _);
    }

    public override Task InitializeAsync() => client.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        client.Dispose();
        factory.Dispose();
    }

    /// <summary>Also run by xunit after <see cref="DatabaseTest.DisposeAsync"/>; disposing twice is harmless.</summary>
    public void Dispose() => Cleanup();

    [IntegrationFact]
    public async Task Create_returns_201_with_a_location_and_the_row_version()
    {
        var response = await client.PostAsJsonAsync("/api/companies", Requests.Company());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var company = (await response.Content.ReadFromJsonAsync<CompanyDto>())!;
        Assert.Equal($"/api/companies/{company.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(8, company.RowVersion.Length);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Company WHERE Code = @code", ("@code", Code("A"))));
    }

    [IntegrationFact]
    public async Task A_duplicate_code_is_a_400_with_an_error_on_code()
    {
        await client.PostAsJsonAsync("/api/companies", Requests.Company("A", "Test Company A"));

        var response = await client.PostAsJsonAsync("/api/companies", Requests.Company("A", "Another Test Company"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("Validation", problem.GetProperty("code").GetString());
        Assert.Equal("Company code must be unique.", problem.GetProperty("errors").GetProperty("code")[0].GetString());
    }

    [IntegrationFact]
    public async Task A_branch_moves_to_another_company_over_http()
    {
        var companyA = await CreateAsync<CompanyDto>("/api/companies", Requests.Company("A", "Test Company A"));
        var companyB = await CreateAsync<CompanyDto>("/api/companies", Requests.Company("B", "Test Company B"));
        var branch = await CreateAsync<BranchDto>("/api/branches", Requests.Branch(companyA.Id));

        var response = await client.PutAsJsonAsync($"/api/branches/{branch.Id}", Requests.BranchUpdate(branch, companyId: companyB.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = (await response.Content.ReadFromJsonAsync<BranchDto>())!;
        Assert.Equal(companyB.Id, moved.CompanyId);
        Assert.NotEqual(branch.RowVersion, moved.RowVersion);
    }

    [IntegrationFact]
    public async Task Moving_a_branch_that_has_a_department_is_a_400_on_companyId()
    {
        var companyA = await CreateAsync<CompanyDto>("/api/companies", Requests.Company("A", "Test Company A"));
        var companyB = await CreateAsync<CompanyDto>("/api/companies", Requests.Company("B", "Test Company B"));
        var branch = await CreateAsync<BranchDto>("/api/branches", Requests.Branch(companyA.Id));
        await client.PostAsJsonAsync("/api/departments", Requests.Department(companyA.Id, branch.Id));

        var response = await client.PutAsJsonAsync($"/api/branches/{branch.Id}", Requests.BranchUpdate(branch, companyId: companyB.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadAsync(response)).GetProperty("errors");
        Assert.Equal("Remove or move its 1 department(s) first, or keep the current company.", errors.GetProperty("companyId")[0].GetString());
    }

    [IntegrationFact]
    public async Task A_stale_row_version_on_put_is_a_409()
    {
        var company = await CreateAsync<CompanyDto>("/api/companies", Requests.Company());
        (await client.PutAsJsonAsync($"/api/companies/{company.Id}", Requests.CompanyUpdate(company, city: "Mandalay"))).EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync($"/api/companies/{company.Id}", Requests.CompanyUpdate(company, city: "Bago"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Conflict", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [IntegrationFact]
    public async Task Delete_without_a_body_is_a_400_on_rowVersion_and_a_stale_one_is_a_409()
    {
        var company = await CreateAsync<CompanyDto>("/api/companies", Requests.Company());

        var missing = await client.DeleteAsync($"/api/companies/{company.Id}");
        var stale = await DeleteAsync($"/api/companies/{company.Id}", Convert.ToBase64String(new byte[8]));

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.True((await ReadAsync(missing)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [IntegrationFact]
    public async Task Deleting_a_company_with_a_branch_is_a_409_with_the_wording_of_the_mock()
    {
        var company = await CreateAsync<CompanyDto>("/api/companies", Requests.Company());
        await client.PostAsJsonAsync("/api/branches", Requests.Branch(company.Id));

        var response = await DeleteAsync($"/api/companies/{company.Id}", Requests.Version(company.RowVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            $"\"{Name("Test Company A")}\" cannot be deleted because it is used by 1 branch(es). Deactivate it instead.",
            (await ReadAsync(response)).GetProperty("detail").GetString());
    }

    [IntegrationFact]
    public async Task Delete_with_the_current_row_version_is_a_204_and_the_record_is_gone()
    {
        var company = await CreateAsync<CompanyDto>("/api/companies", Requests.Company());

        var response = await DeleteAsync($"/api/companies/{company.Id}", Requests.Version(company.RowVersion));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/companies/{company.Id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task Deactivate_and_activate_over_http_return_the_new_state()
    {
        var company = await CreateAsync<CompanyDto>("/api/companies", Requests.Company());

        var deactivated = await client.PostAsJsonAsync($"/api/companies/{company.Id}/deactivate", new { rowVersion = Requests.Version(company.RowVersion) });
        var inactive = (await deactivated.Content.ReadFromJsonAsync<CompanyDto>())!;
        var activated = await client.PostAsJsonAsync($"/api/companies/{company.Id}/activate", new { rowVersion = Requests.Version(inactive.RowVersion) });

        Assert.Equal("Inactive", inactive.Status);
        Assert.Equal("Active", (await activated.Content.ReadFromJsonAsync<CompanyDto>())!.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/companies/{company.Id}/activate", new { })).StatusCode);
    }

    [IntegrationFact]
    public async Task The_query_route_pages_sorts_filters_and_searches_in_sql()
    {
        for (var i = 1; i <= 5; i++)
        {
            await client.PostAsJsonAsync("/api/companies", Requests.Company($"C{i}", $"Test Company {i}"));
        }

        await client.PostAsJsonAsync("/api/companies", Requests.Company("PCT", "Gamma 100% Test", status: "Inactive"));

        var page = (await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/companies/query?pageIndex=1&pageSize=2&sort=name:desc&status=Active&search=company"))!;
        var percent = (await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/companies/query?search=%25"))!;

        Assert.Equal(5, page.Total);
        Assert.Equal([Name("Test Company 3"), Name("Test Company 2")], page.Items.Select(c => c.Name));
        Assert.Equal([Code("PCT")], percent.Items.Select(c => c.Code));
    }

    private async Task<T> CreateAsync<T>(string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private Task<HttpResponseMessage> DeleteAsync(string url, string rowVersion) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(new { rowVersion }) });

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;
}
