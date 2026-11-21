using System.Net;
using Apex.Hrms.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using IPNetwork = System.Net.IPNetwork;

namespace Apex.Hrms.MobileApi.Hosting;

/// <summary>
/// Liveness and readiness probes (same contract as the web API): anonymous, outside <c>/api/</c>, answering only
/// <c>Healthy</c> or <c>Unhealthy</c> as plain text.
/// </summary>
internal static class MobileHealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    private const string ReadyTag = "ready";

    private static readonly string[] ProbeMethods = [HttpMethods.Get, HttpMethods.Head];

    public static IServiceCollection AddMobileHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag], TimeSpan.FromSeconds(5));
        return services;
    }

    public static WebApplication MapMobileHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false })
            .WithMetadata(new HttpMethodMetadata(ProbeMethods))
            .AllowAnonymous();
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .WithMetadata(new HttpMethodMetadata(ProbeMethods))
            .AllowAnonymous();
        return app;
    }

    private sealed class DatabaseHealthCheck(IDatabaseProbe probe) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            await probe.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
    }
}

/// <summary>
/// The <c>Hosting:ForwardedHeaders</c> section, as in the web API: honour <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c>
/// from the listed proxies only. Off by default. Needed behind a load balancer, or the sign-in rate limit is shared by everyone.
/// </summary>
internal static class MobileForwardedHeaders
{
    public const string SectionName = "Hosting:ForwardedHeaders";

    public static IServiceCollection AddMobileForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        if (!section.GetValue<bool>("Enabled"))
        {
            return services;
        }

        var proxies = (section.GetSection("KnownProxies").Get<string[]>() ?? [])
            .Select(value => IPAddress.TryParse(value?.Trim(), out var address)
                ? address
                : throw new InvalidOperationException($"{SectionName}:KnownProxies contains an invalid IP address."))
            .ToList();
        var networks = (section.GetSection("KnownNetworks").Get<string[]>() ?? [])
            .Select(value => IPNetwork.TryParse(value?.Trim(), out var network)
                ? network
                : throw new InvalidOperationException($"{SectionName}:KnownNetworks contains an invalid CIDR network (for example 10.0.0.0/8)."))
            .ToList();
        if (proxies.Count == 0 && networks.Count == 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Enabled needs at least one address in KnownProxies or one network in KnownNetworks.");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            proxies.ForEach(options.KnownProxies.Add);
            networks.ForEach(options.KnownIPNetworks.Add);
        });
        services.AddSingleton(new ForwardedHeadersMarker());
        return services;
    }

    public static WebApplication UseMobileForwardedHeaders(this WebApplication app)
    {
        if (app.Services.GetService<ForwardedHeadersMarker>() is not null)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    private sealed class ForwardedHeadersMarker;
}
