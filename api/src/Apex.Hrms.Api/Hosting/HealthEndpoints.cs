using Apex.Hrms.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Apex.Hrms.Api.Hosting;

/// <summary>
/// Liveness and readiness probes for the host, a load balancer or an uptime monitor. Both are anonymous and answer only
/// <c>Healthy</c> or <c>Unhealthy</c> as plain text (no check names, timings or exception details).
/// They live outside <c>/api/</c> on purpose: every <c>/api/</c> endpoint except sign-in requires a signed-in user.
/// </summary>
internal static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string ReadyTag = "ready";

    private static readonly string[] ProbeMethods = [HttpMethods.Get, HttpMethods.Head];

    /// <summary>How long the readiness check waits for the database before it answers Unhealthy.</summary>
    public static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddHrmsHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag], DatabaseTimeout);
        return services;
    }

    /// <summary>
    /// <c>GET /health/live</c>: the process is up and serving requests (runs no checks).
    /// <c>GET /health/ready</c>: the database can be reached; <c>503</c> when it cannot.
    /// </summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false })
            .WithMetadata(new HttpMethodMetadata(ProbeMethods))
            .AllowAnonymous();
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) })
            .WithMetadata(new HttpMethodMetadata(ProbeMethods))
            .AllowAnonymous();
        return app;
    }
}

/// <summary>Readiness: can a connection to the HRMS database be opened. Reads no data.</summary>
internal sealed class DatabaseHealthCheck(IDatabaseProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await probe.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
}
