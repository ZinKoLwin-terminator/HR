using System.Net;
using Apex.Hrms.Api.Hosting;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The liveness and readiness probes: anonymous, in the Production pipeline (host filtering, HTTPS), and minimal output.
/// The database is replaced by a probe whose answer each test chooses; no connection is ever opened.
/// </summary>
public sealed class HealthEndpointTests
{
    private const string Host = "api.example.test";

    private sealed class FakeProbe(Func<CancellationToken, Task<bool>> answer) : IDatabaseProbe
    {
        public int Calls { get; private set; }

        public Task<bool> CanConnectAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return answer(cancellationToken);
        }
    }

    private static WebApplicationFactory<Program> Production(FakeProbe probe) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Hrms", "Server=.;Database=Placeholder;Integrated Security=True");
            builder.UseSetting("https_port", "443");
            builder.UseSetting("AllowedHosts", Host);
            builder.UseSetting(GrievancePseudonymSettings.KeyName, TestSecrets.NewPseudonymKey());
            builder.ConfigureTestServices(services => services.AddScoped<IDatabaseProbe>(_ => probe));
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"https://{Host}"),
            AllowAutoRedirect = false,
        });

    [Fact]
    public async Task Liveness_is_200_without_a_session_and_never_asks_the_database()
    {
        var probe = new FakeProbe(_ => Task.FromResult(false));
        using var factory = Production(probe);
        using var client = Client(factory);

        var response = await client.GetAsync(HealthEndpoints.LivePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task Readiness_is_200_when_the_database_answers()
    {
        var probe = new FakeProbe(_ => Task.FromResult(true));
        using var factory = Production(probe);
        using var client = Client(factory);

        var response = await client.GetAsync(HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, probe.Calls);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Readiness_is_503_when_the_database_cannot_be_reached()
    {
        using var factory = Production(new FakeProbe(_ => Task.FromResult(false)));
        using var client = Client(factory);

        var response = await client.GetAsync(HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_hides_the_exception_when_the_probe_fails()
    {
        const string secret = "Login failed for user hrms_api on sql.internal.example";
        using var factory = Production(new FakeProbe(_ => throw new InvalidOperationException(secret)));
        using var client = Client(factory);

        var response = await client.GetAsync(HealthEndpoints.ReadyPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body);
        Assert.DoesNotContain("hrms_api", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Readiness_gives_up_on_a_database_that_hangs()
    {
        using var factory = Production(new FakeProbe(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return true;
        }));
        using var client = Client(factory);
        var started = TimeProvider.System.GetTimestamp();

        var response = await client.GetAsync(HealthEndpoints.ReadyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(TimeProvider.System.GetElapsedTime(started) < HealthEndpoints.DatabaseTimeout + TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Probes_obey_the_host_filter()
    {
        using var factory = Production(new FakeProbe(_ => Task.FromResult(true)));
        using var client = Client(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, HealthEndpoints.LivePath);
        request.Headers.Host = "evil.example";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // The list of anonymous endpoints is pinned in AuthEndpointSecurityTests; here: the probes are GET/HEAD only.
    [Theory]
    [InlineData(HealthEndpoints.LivePath)]
    [InlineData(HealthEndpoints.ReadyPath)]
    public void Probes_are_anonymous_and_get_or_head_only(string path)
    {
        using var factory = Production(new FakeProbe(_ => Task.FromResult(true)));
        using var client = Client(factory);
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == path);

        Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Equal(["GET", "HEAD"], endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods);
    }

    [Fact]
    public async Task An_unsafe_method_is_refused_without_running_the_check()
    {
        var probe = new FakeProbe(_ => Task.FromResult(true));
        using var factory = Production(probe);
        using var client = Client(factory);

        var response = await client.PostAsync(HealthEndpoints.ReadyPath, content: null);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(0, probe.Calls);
    }
}
