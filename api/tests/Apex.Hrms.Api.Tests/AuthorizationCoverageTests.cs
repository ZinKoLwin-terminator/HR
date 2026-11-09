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

    [Fact]
    public void Six_organization_resources_map_eight_endpoints_each()
    {
        var endpoints = ApiEndpoints();

        Assert.Equal(48, endpoints.Count);
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
            ["organization:Create", "organization:Delete", "organization:Edit", "organization:View"],
            policies.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_policy_of_each_endpoint_matches_its_http_method()
    {
        foreach (var endpoint in ApiEndpoints())
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
