using System.Net;
using Apex.Hrms.Mobile.Infrastructure.Tokens;
using Apex.Hrms.MobileApi.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.MobileApi.Tests;

/// <summary>The security shape of the host: what is anonymous, the signing key rules, the envelope on unknown routes.</summary>
public sealed class MobileHostingTests
{
    [Fact]
    public void Only_login_refresh_the_health_probes_and_the_development_openapi_document_allow_anonymous_callers()
    {
        using var host = new MobileHost();
        using var client = host.CreateClient();

        var anonymous = host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["/api/v1/auth/login", "/api/v1/auth/refresh", "/health/live", "/health/ready", "/openapi/{documentName}.json"], anonymous);
    }

    [Fact]
    public async Task An_unknown_route_is_a_404_in_the_envelope_shape_for_a_signed_out_caller_too()
    {
        using var host = new MobileHost();
        var login = await host.LoginAsync();
        using var client = host.CreateClient(login.GetProperty("accessToken").GetString());

        using var response = await client.GetAsync("/api/v1/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False((await MobileHost.Json(response)).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task The_liveness_probe_answers_without_a_token()
    {
        using var host = new MobileHost();
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !")]
    [InlineData("c2hvcnQga2V5")]
    public void The_host_does_not_start_without_a_usable_signing_key(string key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [MobileTokenSettings.SigningKeyKey] = key })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() => MobileTokenSettings.From(configuration));

        Assert.Contains(MobileTokenSettings.SigningKeyKey, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_session_shorter_than_the_refresh_token_is_refused()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MobileTokenSettings.SigningKeyKey] = Convert.ToBase64String(new byte[32]),
                ["MobileAuth:RefreshTokenDays"] = "30",
                ["MobileAuth:SessionMaxDays"] = "7",
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => MobileTokenSettings.From(configuration));
    }

    [Fact]
    public void A_production_host_refuses_to_start_with_a_wildcard_host_filter()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting("ConnectionStrings:Hrms", "Server=.;Database=Placeholder;Integrated Security=True");
            builder.UseSetting(MobileTokenSettings.SigningKeyKey, Convert.ToBase64String(new byte[48]));
        });

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public void A_production_host_without_a_signing_key_refuses_to_start()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AllowedHosts", "mobile-api.example.test");
            builder.UseSetting("ConnectionStrings:Hrms", "Server=.;Database=Placeholder;Integrated Security=True");
        });

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains(MobileTokenSettings.SigningKeyKey, error.Message, StringComparison.Ordinal);
    }
}
