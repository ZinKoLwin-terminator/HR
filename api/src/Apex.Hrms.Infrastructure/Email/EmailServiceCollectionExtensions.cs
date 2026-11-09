using Apex.Hrms.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    public const string DevelopmentFolderKey = "Email:DevelopmentFolder";

    /// <summary>
    /// Development: <see cref="DevelopmentMailSink"/> writing under <c>{contentRoot}/App_Data/dev-mail</c> (or the folder in
    /// <c>Email:DevelopmentFolder</c>); it never sends. Any other environment: <see cref="SmtpEmailSender"/> configured
    /// through <c>Email:Smtp:*</c>, which refuses to send while unconfigured.
    /// </summary>
    public static IServiceCollection AddEmailSender(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (isDevelopment)
        {
            var folder = configuration[DevelopmentFolderKey] is { Length: > 0 } configured
                ? configured
                : Path.Combine(contentRootPath, "App_Data", "dev-mail");
            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(
                provider => new DevelopmentMailSink(folder, provider.GetRequiredService<ILogger<DevelopmentMailSink>>())));
        }
        else
        {
            var settings = SmtpSettings.From(configuration);
            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(new SmtpEmailSender(settings)));
        }

        return services;
    }
}
