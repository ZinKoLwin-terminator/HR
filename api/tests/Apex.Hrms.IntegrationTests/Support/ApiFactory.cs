using System.Net.Http.Json;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// The real API host with its connection string pointed at the test database. The appsettings default names the
/// developer database <c>HR</c>, so the override is set three ways (environment, host setting, in-memory source) and
/// the host refuses to start when the resulting setting is anything but the test database.
/// The host runs in Development; its mail goes to a private temporary folder (never <c>App_Data</c>) and its sign-in
/// rate limit is raised so the many sign-ins of the tests do not trip it.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string Key = "ConnectionStrings:Hrms";

    private readonly int loginPermitsPerMinute;

    public ApiFactory(int loginPermitsPerMinute = 10000)
    {
        this.loginPermitsPerMinute = loginPermitsPerMinute;
        Environment.SetEnvironmentVariable(Key.Replace(":", "__", StringComparison.Ordinal), SqlServerEnvironment.ConnectionString);
    }

    /// <summary>Where the development mail sink of this host writes. Removed with the host.</summary>
    public string MailFolder { get; } = Path.Combine(Path.GetTempPath(), "hrms-it-mail-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(Key, SqlServerEnvironment.ConnectionString);

        // Host settings, not ConfigureAppConfiguration: the API reads these while it registers its services, before
        // the app configuration of a test host is applied.
        builder.UseSetting("Email:DevelopmentFolder", MailFolder);
        builder.UseSetting("Auth:RateLimit:PermitsPerMinute", loginPermitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Auth:PasswordResetUrl", "https://app.example.test/reset-password");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Key] = SqlServerEnvironment.ConnectionString }));
    }

    /// <summary>A client that keeps cookies and sends the antiforgery header like the Angular frontend. Not signed in.</summary>
    public HttpClient CreateBrowserClient(out BrowserHandler browser, bool sendXsrfHeader = true) =>
        BrowserHandler.CreateClient(Server.CreateHandler(), out browser, sendXsrfHeader);

    /// <summary>Builds the host and checks which database it is configured for. Call before the first request.</summary>
    public void EnsurePointsAtTestDatabase()
    {
        var configured = Services.GetRequiredService<IConfiguration>()[Key];
        if (configured is null || new SqlConnectionStringBuilder(configured).InitialCatalog != SqlServerEnvironment.DatabaseName)
        {
            throw new InvalidOperationException($"The API host is not configured for {SqlServerEnvironment.DatabaseName}; refusing to send requests.");
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(MailFolder))
        {
            Directory.Delete(MailFolder, recursive: true);
        }
    }
}

internal static class SignInExtensions
{
    /// <summary>Signs a browser-like client in through <c>POST /api/auth/login</c> and fails the test when that is refused.</summary>
    public static async Task SignInAsync(this HttpClient client, TestAccount account, bool rememberMe = false)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = account.Email, password = account.Password, rememberMe });
        Assert.True(response.IsSuccessStatusCode, $"Sign-in of the test account failed with {(int)response.StatusCode}.");
    }
}
