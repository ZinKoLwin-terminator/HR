using System.Net;
using Apex.Hrms.Api.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Apex.Hrms.Api.Tests;

/// <summary>The <c>Hosting:ForwardedHeaders</c> and <c>DataProtection:KeysPath</c> settings.</summary>
public sealed class HostingSettingsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void Forwarded_headers_are_off_by_default()
    {
        var settings = ForwardedHeadersSettings.From(Config());

        Assert.False(settings.Enabled);
        Assert.Empty(settings.KnownProxies);
        Assert.Empty(settings.KnownNetworks);
    }

    [Fact]
    public void Enabled_forwarded_headers_trust_only_the_listed_proxies_and_networks_with_one_hop()
    {
        var settings = ForwardedHeadersSettings.From(Config(
            ("Hosting:ForwardedHeaders:Enabled", "true"),
            ("Hosting:ForwardedHeaders:KnownProxies:0", "10.0.0.5"),
            ("Hosting:ForwardedHeaders:KnownProxies:1", "2001:db8::1"),
            ("Hosting:ForwardedHeaders:KnownNetworks:0", "192.168.10.0/24")));

        var options = settings.ToOptions();

        Assert.True(settings.Enabled);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal([IPAddress.Parse("10.0.0.5"), IPAddress.Parse("2001:db8::1")], options.KnownProxies);
        Assert.Single(options.KnownIPNetworks);
        Assert.Equal("192.168.10.0/24", options.KnownIPNetworks[0].ToString());
    }

    [Fact]
    public void Enabled_without_any_proxy_or_network_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => ForwardedHeadersSettings.From(Config(("Hosting:ForwardedHeaders:Enabled", "true"))));
    }

    [Theory]
    [InlineData("Hosting:ForwardedHeaders:KnownProxies:0", "not-an-ip")]
    [InlineData("Hosting:ForwardedHeaders:KnownNetworks:0", "10.0.0.0/99")]
    public void An_unparsable_proxy_or_network_stops_startup(string key, string value)
    {
        Assert.Throws<InvalidOperationException>(() => ForwardedHeadersSettings.From(Config(("Hosting:ForwardedHeaders:Enabled", "true"), (key, value))));
    }

    [Fact]
    public void The_data_protection_keys_default_to_App_Data_keys_under_the_content_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "apex-root");

        Assert.Equal(Path.Combine(root, "App_Data", "keys"), DataProtectionSettings.ResolveKeysPath(Config(), root));
        Assert.Equal(Path.Combine(root, "App_Data", "keys"), DataProtectionSettings.ResolveKeysPath(Config(("DataProtection:KeysPath", "  ")), root));
    }

    [Fact]
    public void A_configured_keys_path_is_used_relative_paths_start_at_the_content_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "apex-root");
        var absolute = Path.Combine(Path.GetTempPath(), "apex-keys-elsewhere");

        Assert.Equal(absolute, DataProtectionSettings.ResolveKeysPath(Config(("DataProtection:KeysPath", absolute)), root));
        Assert.Equal(Path.Combine(root, "state", "keys"), DataProtectionSettings.ResolveKeysPath(Config(("DataProtection:KeysPath", "state/keys")), root));
    }

    [Fact]
    public async Task The_host_stores_its_data_protection_keys_in_the_configured_directory()
    {
        var keys = Path.Combine(Path.GetTempPath(), "apex-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("DataProtection:KeysPath", keys);
            });
            using var client = factory.CreateClient();

            // The antiforgery token is protected with a data protection key, which creates the key ring.
            var response = await client.GetAsync("/api/auth/antiforgery");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.NotEmpty(Directory.GetFiles(keys, "key-*.xml"));
        }
        finally
        {
            if (Directory.Exists(keys))
            {
                Directory.Delete(keys, recursive: true);
            }
        }
    }
}
