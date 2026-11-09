using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Infrastructure.Identity;
using Apex.Hrms.Infrastructure.Persistence;
using Apex.Hrms.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Hrms";

    /// <summary>
    /// Registers persistence, the clock and the placeholder current user. The connection string comes from
    /// configuration key <c>ConnectionStrings:Hrms</c> (user-secrets or environment variables for anything with a password).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

        services.AddScoped<AuditStampInterceptor>();
        services.AddDbContext<HrmsDbContext>((provider, options) => options
            .UseSqlServer(connectionString)
            .AddInterceptors(provider.GetRequiredService<AuditStampInterceptor>()));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IReferenceUsageReader, EfReferenceUsageReader>();
        return services;
    }
}
