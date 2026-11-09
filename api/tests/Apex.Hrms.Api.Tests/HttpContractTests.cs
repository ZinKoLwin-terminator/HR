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
    public async Task OpenApi_document_lists_the_organization_routes()
    {
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paths = (await ReadAsync(response)).GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/companies/query", out _));
        Assert.True(paths.TryGetProperty("/api/job-grades/{id}", out _));
    }
}
