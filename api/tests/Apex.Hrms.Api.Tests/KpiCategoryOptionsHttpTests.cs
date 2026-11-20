using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// <c>GET /api/kpi/categories/options</c> through the real pipeline without a database: a Department Manager (kpi View, Create,
/// Approve; no kpi-setup) gets every category in the contract's camelCase shape, a caller without <c>kpi:View</c> gets 403, and the
/// category resource itself still needs <c>kpi-setup:View</c> ("options" is never read as an id). The handlers are fakes with
/// fictional rows.
/// </summary>
public sealed class KpiCategoryOptionsHttpTests(WebApplicationFactory<Program> fixture) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/api/kpi/categories/options";

    private static readonly string[] DepartmentManager = ["kpi:View", "kpi:Create", "kpi:Approve", "employees:View", "leave:Approve", "attendance:Approve"];

    /// <summary>One host for the class: the grants of the caller's role travel in a request header, and the category handlers are fakes.</summary>
    private HttpClient ClientWith(params string[] grants)
    {
        var host = SignedInHost.Cached(
            fixture,
            "kpi-category-options",
            factory => SignedInHost.Customize(factory, AccessFakes.Account("Department Manager"), grantsFromHeader: true)
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                {
                    services.AddScoped<IKpiCategoryOptionsHandler, FakeOptions>();
                    services.AddScoped<IGetAllHandler<KpiCategoryDto>, FakeGetAll>();
                    services.AddScoped<IGetByIdHandler<KpiCategoryDto>, FakeGetById>();
                })));
        return SignedInHost.CreateClientWithGrants(host, grants);
    }

    private static string[] PropertiesOf(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];

    [Fact]
    public async Task A_manager_without_the_setup_permission_gets_every_category_in_the_contract_shape()
    {
        using var client = ClientWith(DepartmentManager);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(2, body.GetArrayLength());
        Assert.All(body.EnumerateArray(), row => Assert.Equal(["id", "code", "name", "status"], PropertiesOf(row)));
        var inactive = body[0];
        Assert.Equal(2, inactive.GetProperty("id").GetInt32());
        Assert.Equal("TCS", inactive.GetProperty("code").GetString());
        Assert.Equal("TST- Customer Service", inactive.GetProperty("name").GetString());
        Assert.Equal("Inactive", inactive.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_caller_without_the_performance_view_gets_403_even_with_every_other_grant()
    {
        using var client = ClientWith([.. AccessFakes.AllGrants.Where(grant => grant != "kpi:View")]);

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_category_resource_still_needs_the_setup_view_and_options_is_never_read_as_an_id()
    {
        using var manager = ClientWith(DepartmentManager);
        using var setup = ClientWith("kpi-setup:View");

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/kpi/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/kpi/categories/1")).StatusCode);

        var all = await setup.GetAsync("/api/kpi/categories");
        var one = await setup.GetAsync("/api/kpi/categories/1");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        var category = await one.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, category.GetProperty("id").GetInt32());
        Assert.Equal("TST- Productivity description", category.GetProperty("description").GetString());

        // The setup permission alone does not open the picker: it needs kpi:View.
        Assert.Equal(HttpStatusCode.Forbidden, (await setup.GetAsync(Route)).StatusCode);
    }

    private static KpiCategoryDto Full(int id) =>
        new(id, "TPR", "TST- Productivity", "TST- Productivity description", "Active", [0, 0, 0, 0, 0, 0, 0, 1]);

    /// <summary>An inactive and an active category (fictional), in name order.</summary>
    private sealed class FakeOptions : IKpiCategoryOptionsHandler
    {
        public Task<Result<IReadOnlyList<KpiCategoryOptionDto>>> HandleAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<KpiCategoryOptionDto>>(
            [
                new KpiCategoryOptionDto(2, "TCS", "TST- Customer Service", "Inactive"),
                new KpiCategoryOptionDto(1, "TPR", "TST- Productivity", "Active"),
            ]));
    }

    private sealed class FakeGetAll : IGetAllHandler<KpiCategoryDto>
    {
        public Task<Result<IReadOnlyList<KpiCategoryDto>>> HandleAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success<IReadOnlyList<KpiCategoryDto>>([Full(1)]));
    }

    private sealed class FakeGetById : IGetByIdHandler<KpiCategoryDto>
    {
        public Task<Result<KpiCategoryDto>> HandleAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(Full(id)));
    }
}
