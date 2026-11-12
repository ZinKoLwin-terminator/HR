using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace Apex.Hrms.Api.Hosting;

/// <summary>
/// The <c>Hosting:ForwardedHeaders</c> section: honour <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> from trusted reverse
/// proxies only. Off by default, so local development and a direct deployment are unchanged.
/// </summary>
internal sealed record ForwardedHeadersSettings(bool Enabled, IReadOnlyList<IPAddress> KnownProxies, IReadOnlyList<IPNetwork> KnownNetworks)
{
    public const string SectionName = "Hosting:ForwardedHeaders";

    /// <summary>One proxy hop is trusted: the address in front of the last proxy is not believed, so a caller cannot forge it.</summary>
    public const int ForwardLimit = 1;

    /// <exception cref="InvalidOperationException">Enabled without a trusted proxy or network, or an unparsable address or network.</exception>
    public static ForwardedHeadersSettings From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var enabled = section.GetValue<bool>("Enabled");
        var proxies = (section.GetSection("KnownProxies").Get<string[]>() ?? [])
            .Select(value => IPAddress.TryParse(value?.Trim(), out var address)
                ? address
                : throw new InvalidOperationException($"{SectionName}:KnownProxies contains an invalid IP address."))
            .ToList();
        var networks = (section.GetSection("KnownNetworks").Get<string[]>() ?? [])
            .Select(value => IPNetwork.TryParse(value?.Trim(), out var network)
                ? network
                : throw new InvalidOperationException($"{SectionName}:KnownNetworks contains an invalid CIDR network (for example 10.0.0.0/8)."))
            .ToList();

        if (enabled && proxies.Count == 0 && networks.Count == 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:Enabled needs at least one address in KnownProxies or one network in KnownNetworks: forwarded headers are never trusted from unknown callers.");
        }

        return new ForwardedHeadersSettings(enabled, proxies, networks);
    }

    public ForwardedHeadersOptions ToOptions()
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = ForwardLimit,
        };

        // Replace the framework's loopback defaults: only the configured proxies are trusted.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var proxy in KnownProxies)
        {
            options.KnownProxies.Add(proxy);
        }

        foreach (var network in KnownNetworks)
        {
            options.KnownIPNetworks.Add(network);
        }

        return options;
    }
}

/// <summary>The <c>DataProtection:KeysPath</c> setting: where the data protection key ring is stored.</summary>
internal static class DataProtectionSettings
{
    public const string KeysPathKey = "DataProtection:KeysPath";
    public const string DefaultKeysPath = "App_Data/keys";
    public const string ApplicationName = "Apex.Hrms";

    /// <summary>The configured directory (a relative path starts at the content root), or <c>App_Data/keys</c> under it.</summary>
    public static string ResolveKeysPath(IConfiguration configuration, string contentRoot)
    {
        var configured = configuration[KeysPathKey];
        var path = string.IsNullOrWhiteSpace(configured) ? DefaultKeysPath : configured.Trim();
        return Path.GetFullPath(path, contentRoot);
    }
}

internal static class HostingSetup
{
    /// <summary>
    /// Persists the data protection keys (session cookie, antiforgery tokens, password reset tokens) so they survive a restart,
    /// under one application name so that instances sharing the directory share the keys. The directory holds secrets: see the README.
    /// </summary>
    public static IServiceCollection AddHrmsDataProtection(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.AddDataProtection()
            .SetApplicationName(DataProtectionSettings.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(DataProtectionSettings.ResolveKeysPath(configuration, contentRoot)));
        return services;
    }

    public static IServiceCollection AddHrmsForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = ForwardedHeadersSettings.From(configuration);
        if (settings.Enabled)
        {
            services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>>(new ConfigureOptions<ForwardedHeadersOptions>(options =>
            {
                var configured = settings.ToOptions();
                options.ForwardedHeaders = configured.ForwardedHeaders;
                options.ForwardLimit = configured.ForwardLimit;
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (var proxy in configured.KnownProxies)
                {
                    options.KnownProxies.Add(proxy);
                }

                foreach (var network in configured.KnownIPNetworks)
                {
                    options.KnownIPNetworks.Add(network);
                }
            }));
        }

        return services;
    }

    /// <summary>Runs first in the pipeline when enabled, before anything reads the client address or the scheme.</summary>
    public static WebApplication UseHrmsForwardedHeaders(this WebApplication app)
    {
        if (ForwardedHeadersSettings.From(app.Configuration).Enabled)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }
}
