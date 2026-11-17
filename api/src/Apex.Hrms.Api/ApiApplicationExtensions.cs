using Apex.Hrms.Api.Auth;
using Apex.Hrms.Api.Hosting;
using Apex.Hrms.Api.Http;

namespace Apex.Hrms.Api;

internal static class ApiApplicationExtensions
{
    /// <summary>
    /// The request pipeline in order: forwarded headers (only when enabled), response headers, error handling, transport security (outside Development),
    /// CORS, authentication (the session cookie), rate limiting (after authentication, so the evidence upload can be limited per account;
    /// the sign-in limit is per client IP and does not depend on it), antiforgery validation of unsafe methods (it needs the signed-in
    /// user), authorization.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseHrmsForwardedHeaders();
        app.UseMiddleware<ApiResponseHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseMiddleware<AntiforgeryMiddleware>();
        app.UseAuthorization();
        return app;
    }

    /// <summary>
    /// Outside Development the host filter must be configured explicitly: a missing or wildcard
    /// <c>AllowedHosts</c> would accept any Host header. Fails at startup instead of running open.
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
