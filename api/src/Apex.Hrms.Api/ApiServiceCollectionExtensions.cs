using Apex.Hrms.Api.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Apex.Hrms.Api;

internal static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "Frontend";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();

        // Make malformed requests flow through the exception handler so they get the same problem shape.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            problem.Extensions.TryAdd(ApiProblems.CodeKey, ApiProblems.TypeFor(problem.Status ?? context.HttpContext.Response.StatusCode).ToString());
            problem.Extensions.Remove("traceId");
        });

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(
            CorsPolicyName,
            policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        return services;
    }
}
