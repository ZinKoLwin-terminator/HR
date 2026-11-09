using Microsoft.Data.SqlClient;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Where the integration tests run: the throwaway LocalDB database <c>HR_IntegrationTests</c> and nothing else. The
/// database name is a constant on purpose (it is never read from configuration), so a stray setting can never point
/// the tests, which write and roll back data, at the developer database <c>HR</c>.
/// </summary>
internal static class SqlServerEnvironment
{
    public const string DatabaseName = "HR_IntegrationTests";

    public const string Server = @"(localdb)\MSSQLLocalDB";

    /// <summary>Overrides the location of the database project's dacpac.</summary>
    public const string DacpacVariable = "HRMS_DACPAC";

    private const string DefaultDacpacPath = @"D:\New folder\HR DBPJ\HR DBPJ\bin\Debug\HR DBPJ.dacpac";

    private static readonly Lazy<string?> UnavailableReasonValue = new(DetectUnavailableReason);

    public static string DacpacPath =>
        Environment.GetEnvironmentVariable(DacpacVariable) is { Length: > 0 } path ? path : DefaultDacpacPath;

    /// <summary>The server without a database, used to deploy the schema (which creates the database when needed).</summary>
    public static string ServerConnectionString => Builder(initialCatalog: null).ConnectionString;

    public static string ConnectionString => Builder(DatabaseName).ConnectionString;

    /// <summary>Why the tests cannot run here (for example no LocalDB on Linux, or no dacpac), or null when they can.</summary>
    public static string? UnavailableReason => UnavailableReasonValue.Value;

    private static SqlConnectionStringBuilder Builder(string? initialCatalog)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            ConnectTimeout = 30,
        };
        if (initialCatalog is not null)
        {
            builder.InitialCatalog = initialCatalog;
        }

        return builder;
    }

    private static string? DetectUnavailableReason()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "SQL Server integration tests need LocalDB, which only exists on Windows.";
        }

        if (!File.Exists(DacpacPath))
        {
            return $"Dacpac not found at '{DacpacPath}'. Build the database project or set {DacpacVariable}.";
        }

        try
        {
            using var connection = new SqlConnection(ServerConnectionString);
            connection.Open();
            return null;
        }
        catch (SqlException exception)
        {
            return $"LocalDB instance {Server} is not available: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            return $"LocalDB instance {Server} is not available: {exception.Message}";
        }
    }
}
