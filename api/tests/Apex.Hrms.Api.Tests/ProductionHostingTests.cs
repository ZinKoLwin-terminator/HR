using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// Outside Development the API must run with HTTPS redirection, HSTS and an explicit host list. These tests start the
/// pipeline in the Production environment; the connection string is a placeholder and no query is ever run.
/// </summary>
public sealed class ProductionHostingTests
{
    private static WebApplicationFactory<Program> Production(string? allowedHosts) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Hrms", "Server=.;Database=Placeholder;Integrated Security=True");
            builder.UseSetting("https_port", "443");
            if (allowedHosts is not null)
            {
                builder.UseSetting("AllowedHosts", allowedHosts);
            }
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.example.test"),
            AllowAutoRedirect = false,
        });

    [Theory]
    [InlineData("*")]
    [InlineData("api.example.test;*.example.test")]
    [InlineData("")]
    public void Production_refuses_to_start_with_a_wildcard_or_empty_host_list(string allowedHosts)
    {
        using var factory = Production(allowedHosts);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("AllowedHosts", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_sends_hsts_over_https_and_serves_only_the_configured_host()
    {
        using var factory = Production("api.example.test");
        using var client = Client(factory);

        var response = await client.GetAsync("/api/companies/query?sort=nope:asc");
        using var wrongHost = new HttpRequestMessage(HttpMethod.Get, "/api/companies/query?sort=nope:asc");
        wrongHost.Headers.Host = "evil.example";
        var rejected = await client.SendAsync(wrongHost);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("max-age", response.Headers.GetValues("Strict-Transport-Security").Single(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.False(rejected.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Production_redirects_http_to_https()
    {
        using var factory = Production("api.example.test");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://api.example.test"),
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/companies/query?sort=nope:asc");

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.StartsWith("https://api.example.test", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Development_has_no_hsts_and_accepts_any_host()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/companies/query?sort=nope:asc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }
}
