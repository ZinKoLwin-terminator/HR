namespace Apex.Hrms.Api.Hosting;

/// <summary>
/// The OpenAPI document (<c>/openapi/v1.json</c>) and Swagger UI (<c>/swagger</c>). Always on in Development; in any other
/// environment only when <c>OpenApi:Enabled</c> is <see langword="true"/> (default off: the document lists every route and
/// field of the API). Both are anonymous, but every API call made from Swagger UI still needs a signed-in session.
/// </summary>
internal static class ApiDocumentation
{
    public const string EnabledKey = "OpenApi:Enabled";
    public const string DocumentPath = "/openapi/v1.json";
    public const string SwaggerRoutePrefix = "swagger";

    /// <summary>
    /// Copies the readable <c>XSRF-TOKEN</c> cookie into the <c>X-XSRF-TOKEN</c> header, as the Angular client does, so POST, PUT
    /// and DELETE calls from Swagger UI pass the antiforgery check. Call <c>GET /api/auth/antiforgery</c> first to get the cookie.
    /// </summary>
    private const string AntiforgeryRequestInterceptor =
        "(request) => { const match = document.cookie.match(/(?:^|; )XSRF-TOKEN=([^;]*)/); " +
        "if (match) { request.headers['X-XSRF-TOKEN'] = decodeURIComponent(match[1]); } return request; }";

    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() || configuration.GetValue<bool>(EnabledKey);

    /// <summary>
    /// Serves the Swagger UI files. Runs before the API pipeline: the UI is static files with no endpoint, and the
    /// deny-by-default authorization fallback would otherwise refuse them.
    /// </summary>
    public static WebApplication UseHrmsSwaggerUi(this WebApplication app)
    {
        if (IsEnabled(app.Configuration, app.Environment))
        {
            app.UseSwaggerUI(options =>
            {
                options.RoutePrefix = SwaggerRoutePrefix;
                options.SwaggerEndpoint(DocumentPath, "APEX HRMS API v1");
                options.DocumentTitle = "APEX HRMS API";
                options.UseRequestInterceptor(AntiforgeryRequestInterceptor);
            });
        }

        return app;
    }

    /// <summary>Maps the OpenAPI document as an anonymous endpoint when enabled.</summary>
    public static WebApplication MapHrmsOpenApi(this WebApplication app)
    {
        if (IsEnabled(app.Configuration, app.Environment))
        {
            app.MapOpenApi().AllowAnonymous();
        }

        return app;
    }
}
