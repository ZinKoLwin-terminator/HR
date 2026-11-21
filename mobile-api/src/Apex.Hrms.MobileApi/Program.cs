using Apex.Hrms.Mobile.Application;
using Apex.Hrms.Mobile.Infrastructure;
using Apex.Hrms.MobileApi.Endpoints;
using Apex.Hrms.MobileApi.Hosting;

namespace Apex.Hrms.MobileApi;

/// <summary>
/// The host of the employee mobile app's API. A class in a namespace rather than top-level statements, so its name does not
/// clash with the web API's global <c>Program</c> where a test project references both.
/// </summary>
public sealed partial class Program
{
    private Program()
    {
    }

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        MobileApiExtensions.EnsureHostFilteringIsConfigured(builder.Configuration, builder.Environment);

        // Clean Architecture: Mobile.Domain (session rules) <- Mobile.Application (use cases, ports) <- Mobile.Infrastructure
        // (EF, Identity, JWT adapters) <- this host (HTTP only). The infrastructure brings only the shared persistence and
        // Identity of api/src; add the shared module of a feature (AddAttendance, AddLeave ...) when its mobile endpoints come.
        builder.Services
            .AddMobileApplication()
            .AddMobileInfrastructure(builder.Configuration)
            .AddMobileApi(builder.Configuration);

        var app = builder.Build();

        app.UseMobileApiPipeline();
        app.MapMobileOpenApi();
        app.MapMobileHealthEndpoints();
        app.MapMobileAuthEndpoints();

        await app.RunAsync();
    }
}
