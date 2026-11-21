using Apex.Hrms.Infrastructure;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Infrastructure.Identity;
using Apex.Hrms.Mobile.Infrastructure.Persistence;
using Apex.Hrms.Mobile.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Mobile.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the adapters of the mobile ports: the shared HRMS persistence and Identity (UserManager, account directory,
    /// clock; connection string <c>ConnectionStrings:Hrms</c>), the mobile DbContext on the same database, the session store,
    /// the employee directory, the credential gateway, and the token issuer with its <c>MobileAuth</c> settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">The connection string or the signing key is missing, or a setting is out of range.</exception>
    public static IServiceCollection AddMobileInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(Apex.Hrms.Infrastructure.DependencyInjection.ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{Apex.Hrms.Infrastructure.DependencyInjection.ConnectionStringName}' is not configured.");
        var settings = MobileTokenSettings.From(configuration);

        services.AddPersistenceAndIdentity(configuration);
        services.AddDbContext<MobileDbContext>(options => options.UseSqlServer(connectionString));

        services.AddSingleton(settings);
        services.AddSingleton(settings.Lifetimes);
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IMobileSessionStore, EfMobileSessionStore>();
        services.AddScoped<IMobileEmployeeDirectory, EfMobileEmployeeDirectory>();
        services.AddScoped<IIdentityGateway, IdentityGateway>();
        return services;
    }
}
