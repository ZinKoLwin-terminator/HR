using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// Exercises routing, binding and the error contract. Every request here is rejected before a database query
/// would run, so no connection to SQL Server is ever opened.
/// </summary>
public sealed class HttpContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public HttpContractTests(WebApplicationFactory<Program> factory) =>
        client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development")).CreateClient();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    [Fact]
    public async Task Unknown_sort_field_on_query_is_a_validation_problem_with_code_and_errors()
    {
        var response = await client.GetAsync("/api/companies/query?pageIndex=0&pageSize=5&sort=salary:asc&status=Active");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await ReadAsync(response);
        Assert.Equal("Validation", body.GetProperty("code").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("Choose a sortable field.", body.GetProperty("errors").GetProperty("sort")[0].GetString());
    }

    [Theory]
    [InlineData("companies")]
    [InlineData("branches")]
    [InlineData("departments")]
    [InlineData("sections")]
    [InlineData("job-grades")]
    [InlineData("positions")]
    public async Task Every_organization_resource_binds_its_query_route(string resource)
    {
        var response = await client.GetAsync($"/api/{resource}/query?sort=nope:desc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_with_invalid_fields_returns_camel_case_field_errors()
    {
        var response = await client.PostAsJsonAsync("/api/job-grades", new { code = "bad code", name = "", level = 0, minSalary = 10, maxSalary = 20, status = "Maybe" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadAsync(response)).GetProperty("errors");
        Assert.Equal("Use upper-case letters, numbers and dashes only.", errors.GetProperty("code")[0].GetString());
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("level", out _));
        Assert.True(errors.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Malformed_json_is_a_validation_problem()
    {
        var response = await client.PostAsync("/api/companies", new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Validation", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_route_is_a_not_found_problem()
    {
        var response = await client.GetAsync("/api/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NotFound", (await ReadAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Cors_allows_the_angular_dev_server_only()
    {
        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/companies");
        allowed.Headers.Add("Origin", "http://localhost:4200");
        allowed.Headers.Add("Access-Control-Request-Method", "POST");
        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/companies");
        denied.Headers.Add("Origin", "http://evil.example");
        denied.Headers.Add("Access-Control-Request-Method", "POST");

        var allowedResponse = await client.SendAsync(allowed);
        var deniedResponse = await client.SendAsync(denied);

        Assert.Equal("http://localhost:4200", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Update_without_a_row_version_is_a_validation_problem_on_rowVersion()
    {
        var body = new { code = "G1", name = "Junior", level = 1, minSalary = 10, maxSalary = 20 };

        var response = await client.PutAsJsonAsync("/api/job-grades/1", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("rowVersion", out _));
        Assert.False(errors.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Update_ignores_a_status_sent_by_an_old_client_because_status_has_its_own_commands()
    {
        var body = new { code = "G1", name = "Junior", level = 1, minSalary = 10, maxSalary = 20, status = "Maybe", rowVersion = "nope" };

        var response = await client.PutAsJsonAsync("/api/job-grades/1", body);

        var errors = (await ReadAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("rowVersion", out _));
        Assert.False(errors.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Delete_requires_a_row_version_in_a_json_body()
    {
        var noBody = await client.DeleteAsync("/api/branches/3");
        using var empty = new HttpRequestMessage(HttpMethod.Delete, "/api/branches/3") { Content = JsonContent.Create(new { }) };
        using var invalid = new HttpRequestMessage(HttpMethod.Delete, "/api/branches/3") { Content = JsonContent.Create(new { rowVersion = "###" }) };

        foreach (var response in new[] { noBody, await client.SendAsync(empty), await client.SendAsync(invalid) })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await ReadAsync(response);
            Assert.Equal("Validation", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("rowVersion", out _));
        }
    }

    [Theory]
    [InlineData("companies")]
    [InlineData("branches")]
    [InlineData("departments")]
    [InlineData("sections")]
    [InlineData("job-grades")]
    [InlineData("positions")]
    [InlineData("companies", "deactivate")]
    public async Task Every_organization_resource_has_activate_and_deactivate_commands_that_require_a_row_version(string resource, string action = "activate")
    {
        var withoutBody = await client.PostAsync($"/api/{resource}/4/{action}", content: null);
        var withEmptyJson = await client.PostAsJsonAsync($"/api/{resource}/4/{action}", new { });

        foreach (var response in new[] { withoutBody, withEmptyJson })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await ReadAsync(response)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        }
    }

    [Fact]
    public async Task Search_longer_than_100_characters_is_rejected()
    {
        var response = await client.GetAsync($"/api/companies/query?search={new string('a', 101)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await ReadAsync(response)).GetProperty("errors").TryGetProperty("search", out _));
    }

    [Fact]
    public async Task Problem_responses_carry_a_trace_id_and_every_response_has_the_security_headers()
    {
        var response = await client.GetAsync("/api/companies/query?sort=salary:asc");

        var body = await ReadAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Not_found_problems_and_the_openapi_document_also_carry_the_security_headers()
    {
        var notFound = await client.GetAsync("/api/nothing-here");
        var openApi = await client.GetAsync("/openapi/v1.json");

        foreach (var response in new[] { notFound, openApi })
        {
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }

        Assert.False(string.IsNullOrWhiteSpace((await ReadAsync(notFound)).GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task OpenApi_document_lists_the_organization_routes()
    {
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await ReadAsync(response)).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/companies/query", out _));
        Assert.True(paths.TryGetProperty("/api/job-grades/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/positions/{id}/activate", out _));
        Assert.True(paths.TryGetProperty("/api/positions/{id}/deactivate", out _));
    }
}
