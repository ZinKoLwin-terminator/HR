using Apex.Hrms.Domain.Common;
using Apex.Hrms.MobileApi.Auth;
using Apex.Hrms.MobileApi.Hosting;
using Apex.Hrms.MobileApi.Http;

namespace Apex.Hrms.MobileApi;

internal static class MobileApiExtensions
{
    public static IServiceCollection AddMobileApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();

        // Malformed requests throw, so the exception handler answers them in the envelope shape.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddExceptionHandler<MobileExceptionHandler>();
        services.AddProblemDetails();

        services.AddMobileAuthentication(configuration);
        services.AddMobileForwardedHeaders(configuration);
        services.AddMobileHealthChecks();
        return services;
    }

    /// <summary>
    /// The request pipeline in order: forwarded headers (only when enabled), response headers, error handling (every error in
    /// the envelope shape), transport security (outside Development), authentication (bearer token), rate limiting, authorization.
    /// </summary>
    public static WebApplication UseMobileApiPipeline(this WebApplication app)
    {
        app.UseMobileForwardedHeaders();
        app.UseMiddleware<MobileResponseHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(context => MobileResults.WriteErrorAsync(
            context.HttpContext,
            context.HttpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? AppError.Unknown(MobileResults.MessageFor(context.HttpContext.Response.StatusCode))
                : AppError.Validation(MobileResults.MessageFor(context.HttpContext.Response.StatusCode)),
            context.HttpContext.Response.StatusCode));

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }

    /// <summary>The OpenAPI document, in Development only (it describes the endpoints to anyone who can reach it).</summary>
    public static WebApplication MapMobileOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        return app;
    }

    /// <summary>
    /// Outside Development the host filter must be configured explicitly: a missing or wildcard <c>AllowedHosts</c> would
    /// accept any Host header. Fails at startup instead of running open.
    /// </summary>
    /// <exception cref="InvalidOperationException">AllowedHosts is empty or contains a wildcard outside Development.</exception>
    public static void EnsureHostFilteringIsConfigured(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var allowedHosts = configuration["AllowedHosts"]?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (allowedHosts.Length == 0 || allowedHosts.Any(host => host.Contains('*', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "AllowedHosts must list the exact host names of this API (separated by ';') outside Development; wildcards are not allowed.");
        }
    }
}
