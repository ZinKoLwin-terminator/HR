using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Infrastructure.Identity;
using Apex.Hrms.Infrastructure.Persistence;
using Apex.Hrms.Infrastructure.Security;
using Apex.Hrms.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Hrms";

    /// <summary>
    /// Registers persistence, ASP.NET Core Identity (user and password stores in the auth schema, lockout and
    /// password rules), the account directory, the clock and the placeholder current user. The connection string
    /// comes from configuration key <c>ConnectionStrings:Hrms</c> (user-secrets or environment variables for anything
    /// with a password); the business time zone from <c>Business:TimeZone</c> (default Asia/Yangon).
    /// The current user is registered with TryAdd: the Api layer replaces it with its own <see cref="ICurrentUser"/>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
        var businessTimeZone = BusinessTimeZone.Resolve(configuration[BusinessTimeZone.ConfigurationKey]);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock>(provider => new SystemClock(provider.GetRequiredService<TimeProvider>(), businessTimeZone));
        services.TryAddScoped<ICurrentUser, AnonymousCurrentUser>();

        services.AddScoped<AuditStampInterceptor>();
        services.AddDbContext<HrmsDbContext>((provider, options) => options
            .UseSqlServer(connectionString)
            .AddInterceptors(provider.GetRequiredService<AuditStampInterceptor>()));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IRecordCounter<>), typeof(EfRecordCounter<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IEmployeeCodeSequence, EfEmployeeCodeSequence>();
        services.AddScoped<ILeaveApplicationNumberSequence, EfLeaveApplicationNumberSequence>();
        services.AddScoped<IJobOpeningCodeSequence, EfJobOpeningCodeSequence>();
        services.AddSingleton<IBusinessWeek>(ConfiguredBusinessWeek.From(
            configuration.GetSection(ConfiguredBusinessWeek.ConfigurationKey).Get<string[]>()));

        services.AddScoped<IAccountDirectory, EfAccountDirectory>();
        services.AddScoped<IAdministratorStore, EfAdministratorStore>();
        AddIdentity(services);
        return services;
    }

    /// <summary>
    /// Makes the caller the explicit system identity: the only one allowed to write audited rows without a user id. For
    /// command line tools (the bootstrap command) and deployment seeds; the web host never calls it.
    /// </summary>
    public static IServiceCollection UseSystemIdentity(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, SystemCurrentUser>());
        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<IdentityUser>(options =>
            {
                // Length is the only password rule; no composition rules.
                options.Password.RequiredLength = SecurityPolicy.PasswordMinimumLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.User.RequireUniqueEmail = true;

                // 5 failed sign-ins lock the account for 15 minutes, for new users too.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = SecurityPolicy.MaxFailedSignIns;
                options.Lockout.DefaultLockoutTimeSpan = SecurityPolicy.LockoutDuration;
            })
            .AddEntityFrameworkStores<HrmsDbContext>();
    }
}
