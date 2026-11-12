using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Dashboard;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Infrastructure.Excel;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Infrastructure.Email;
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
    /// password rules), the account directory, the clock and the anonymous placeholder current user (the Api layer replaces it). The connection string
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
        services.AddScoped<EfActivityRecorder>();
        services.AddScoped<IActivityRecorder>(provider => provider.GetRequiredService<EfActivityRecorder>());
        services.AddScoped<IActivityLogStore, EfActivityLogStore>();
        services.AddScoped<INotificationStore, EfNotificationStore>();
        services.AddScoped<IDashboardSource, EfDashboardSource>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IPayrollRunOutput, EfPayrollRunOutput>();
        services.AddScoped<IActivityMute>(provider => provider.GetRequiredService<EfActivityRecorder>());
        services.AddScoped<IImportBatchStore, EfImportBatchStore>();
        services.AddScoped<IBatchTransactionFactory, EfBatchTransactionFactory>();
        services.AddSingleton<IWorkbookReader, ClosedXmlWorkbookReader>();
        services.AddSingleton<IWorkbookWriter, ClosedXmlWorkbookWriter>();
        services.AddSingleton(configuration.GetSection("Excel").Get<ExcelLimits>() ?? new ExcelLimits());
        services.TryAddSingleton<IFileStorage>(provider => new LocalFileStorage(
            configuration["Excel:StorageFolder"] is { Length: > 0 } folder ? folder : Path.Combine(AppContext.BaseDirectory, "App_Data", "excel-uploads"),
            provider.GetRequiredService<TimeProvider>()));
        services.AddScoped<IEmployeeCodeSequence, EfEmployeeCodeSequence>();
        services.AddScoped<ILeaveApplicationNumberSequence, EfLeaveApplicationNumberSequence>();
        services.AddScoped<IJobOpeningCodeSequence, EfJobOpeningCodeSequence>();
        services.AddSingleton<IBusinessWeek>(ConfiguredBusinessWeek.From(
            configuration.GetSection(ConfiguredBusinessWeek.ConfigurationKey).Get<string[]>()));

        services.AddScoped<IAccountDirectory, EfAccountDirectory>();
        services.AddScoped<IAdministratorStore, EfAdministratorStore>();
        services.AddScoped<IAccountLogins, EfAccountLogins>();
        services.AddScoped<IPermissionStore, EfPermissionStore>();
        services.AddScoped<ITeamDirectory, EfTeamDirectory>();
        services.AddScoped<IRecruitmentDirectory, EfRecruitmentDirectory>();

        // Fails closed (refuses to send) until the host registers a real sender with AddEmailSender.
        services.TryAddSingleton<IEmailSender>(new SmtpEmailSender(SmtpSettings.From(configuration)));
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
