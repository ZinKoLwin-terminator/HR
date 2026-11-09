using Apex.Hrms.Api.Http;

namespace Apex.Hrms.Api;

internal static class ApiApplicationExtensions
{
    /// <summary>
    /// The request pipeline in order: response headers, error handling, transport security (outside Development),
    /// CORS, authorization. TODO(Identity): add <c>UseAuthentication()</c> before <c>UseAuthorization()</c>.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<ApiResponseHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCors(ApiServiceCollectionExtensions.CorsPolicyName);
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
