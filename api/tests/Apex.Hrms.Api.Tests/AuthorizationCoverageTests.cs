using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// Architecture rule: no endpoint of the API is mapped without an authorization policy, and the policy matches what
/// the endpoint does. Today the policies are permissive placeholders; this test keeps the seams complete so the real
/// permission check can be added in one place without finding forgotten endpoints.
/// </summary>
public sealed class AuthorizationCoverageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public AuthorizationCoverageTests(WebApplicationFactory<Program> factory) =>
        this.factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    private List<RouteEndpoint> ApiEndpoints()
    {
        using var client = factory.CreateClient();
        return [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal))];
    }

    [Fact]
    public void Every_api_endpoint_requires_a_named_policy()
    {
        var endpoints = ApiEndpoints();

        Assert.NotEmpty(endpoints);
        var unprotected = endpoints
            .Where(endpoint => !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => !string.IsNullOrWhiteSpace(data.Policy)))
            .Select(endpoint => endpoint.DisplayName)
            .ToList();
        Assert.Empty(unprotected);
        Assert.DoesNotContain(endpoints, endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null);
    }

    private static bool IsEmployeeRoute(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/api/employees", StringComparison.Ordinal);

    [Fact]
    public void Six_organization_resources_map_eight_endpoints_each()
    {
        var endpoints = ApiEndpoints().Where(endpoint => !IsEmployeeRoute(endpoint)).ToList();

        Assert.Equal(48, endpoints.Count);
    }

    [Fact]
    public void Employees_map_fourteen_endpoints_and_no_unpaged_list()
    {
        var endpoints = ApiEndpoints().Where(IsEmployeeRoute).ToList();

        Assert.Equal(14, endpoints.Count);
        Assert.DoesNotContain(
            endpoints,
            endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(HttpMethods.Get)
                && endpoint.RoutePattern.RawText!.TrimEnd('/') == "/api/employees");
    }

    [Fact]
    public void Employee_personal_and_bank_endpoints_have_their_own_permissions()
    {
        var policies = ApiEndpoints().Where(IsEmployeeRoute)
            .ToDictionary(
                endpoint => $"{endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single()} {endpoint.RoutePattern.RawText}",
                endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy);

        Assert.Equal("employees:ViewSensitive", policies["GET /api/employees/{id:int}/personal"]);
        Assert.Equal("employees:EditSensitive", policies["PUT /api/employees/{id:int}/personal"]);
        Assert.Equal("employees:ViewBank", policies["GET /api/employees/{id:int}/bank-account"]);
        Assert.Equal("employees:EditBank", policies["PUT /api/employees/{id:int}/bank-account"]);
        Assert.Equal("employees:View", policies["GET /api/employees/query"]);
        Assert.Equal("employees:View", policies["GET /api/employees/{id:int}"]);
        Assert.Equal("employees:View", policies["GET /api/employees/{id:int}/history"]);
        Assert.Equal("employees:Create", policies["GET /api/employees/next-code"]);
        Assert.Equal("employees:Create", policies["POST /api/employees/"]);
        Assert.Equal("employees:Edit", policies["PUT /api/employees/{id:int}"]);
        Assert.Equal("employees:Delete", policies["DELETE /api/employees/{id:int}"]);
        Assert.Equal("employees:Edit", policies["POST /api/employees/{id:int}/exit"]);
        Assert.Equal("employees:Edit", policies["POST /api/employees/{id:int}/reinstate"]);
        Assert.Equal("employees:Edit", policies["POST /api/employees/{id:int}/status"]);
    }

    [Fact]
    public async Task Every_policy_in_use_is_registered()
    {
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policies = ApiEndpoints()
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>())
            .Select(data => data.Policy!)
            .Distinct()
            .ToList();

        foreach (var policy in policies)
        {
            Assert.NotNull(await provider.GetPolicyAsync(policy));
        }

        Assert.Equal(
            [
                "employees:Create", "employees:Delete", "employees:Edit", "employees:EditBank", "employees:EditSensitive",
                "employees:View", "employees:ViewBank", "employees:ViewSensitive",
                "organization:Create", "organization:Delete", "organization:Edit", "organization:View",
            ],
            policies.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_policy_of_each_endpoint_matches_its_http_method()
    {
        foreach (var endpoint in ApiEndpoints().Where(endpoint => !IsEmployeeRoute(endpoint)))
        {
            var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single();
            var isCommand = method == HttpMethods.Post && endpoint.RoutePattern.RawText!.Contains("/{id:int}/", StringComparison.Ordinal);
            var expected = method switch
            {
                "GET" => "organization:View",
                "POST" when isCommand => "organization:Edit",
                "POST" => "organization:Create",
                "PUT" => "organization:Edit",
                "DELETE" => "organization:Delete",
                _ => throw new InvalidOperationException($"Unexpected method {method}."),
            };

            var policy = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single(data => data.Policy is not null).Policy;
            Assert.True(expected == policy, $"{endpoint.DisplayName}: expected {expected} but found {policy}");
        }
    }
}
