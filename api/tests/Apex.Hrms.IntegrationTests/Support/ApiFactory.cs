using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// The real API host with its connection string pointed at the test database. The appsettings default names the
/// developer database <c>HR</c>, so the override is set three ways (environment, host setting, in-memory source) and
/// the host refuses to start when the resulting setting is anything but the test database.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string Key = "ConnectionStrings:Hrms";

    public ApiFactory() =>
        Environment.SetEnvironmentVariable(Key.Replace(":", "__", StringComparison.Ordinal), SqlServerEnvironment.ConnectionString);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting(Key, SqlServerEnvironment.ConnectionString);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { [Key] = SqlServerEnvironment.ConnectionString }));
    }

    /// <summary>Builds the host and checks which database it is configured for. Call before the first request.</summary>
    public void EnsurePointsAtTestDatabase()
    {
        var configured = Services.GetRequiredService<IConfiguration>()[Key];
        if (configured is null || new SqlConnectionStringBuilder(configured).InitialCatalog != SqlServerEnvironment.DatabaseName)
        {
            throw new InvalidOperationException($"The API host is not configured for {SqlServerEnvironment.DatabaseName}; refusing to send requests.");
        }
    }
}
