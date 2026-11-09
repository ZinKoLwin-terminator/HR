using Microsoft.SqlServer.Dac;

namespace Apex.Hrms.IntegrationTests.Support;

[CollectionDefinition(Name)]
public sealed class SqlServerFixtureDefinition : ICollectionFixture<DatabaseFixture>
{
    /// <summary>One collection for every integration test: they share one database, so they run one at a time.</summary>
    public const string Name = "SqlServer";
}

/// <summary>
/// Deploys the schema of the SSDT database project (its dacpac) into <c>HR_IntegrationTests</c> once per test run.
/// The deployment is an upgrade: it creates the database when it is missing, never drops objects that are not in the
/// dacpac and refuses changes that could lose data. Tests commit their rows and delete them again (see <see cref="TestRows"/>); the
/// fixture checks at the start and at the end of the run that no row with a test code is left.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        if (SqlServerEnvironment.UnavailableReason is not null)
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            Deploy();
            await AssertNoTestRowsAsync("A previous run left test data behind (start of run).");
        });
    }

    public async Task DisposeAsync()
    {
        if (SqlServerEnvironment.UnavailableReason is null)
        {
            await AssertNoTestRowsAsync("A test did not clean up its rows (end of run).");
        }
    }

    private static void Deploy()
    {
        var services = new DacServices(SqlServerEnvironment.ServerConnectionString);
        using var package = DacPackage.Load(SqlServerEnvironment.DacpacPath);
        var options = new DacDeployOptions
        {
            CreateNewDatabase = false,
            BlockOnPossibleDataLoss = true,
            DropObjectsNotInSource = false,
        };
        services.Deploy(package, SqlServerEnvironment.DatabaseName, upgradeExisting: true, options);
    }

    private static async Task AssertNoTestRowsAsync(string problem)
    {
        var leftovers = await TestRows.CountAsync(TestRows.TagFamily);
        if (leftovers.Count != 0)
        {
            var detail = string.Join(", ", leftovers.Select(pair => $"{pair.Key}: {pair.Value}"));
            throw new InvalidOperationException(
                $"{problem} {leftovers.Values.Sum()} row(s) with a code starting with '{TestRows.TagFamily}' remain in {SqlServerEnvironment.DatabaseName} ({detail}). " +
                $"Delete them with DELETE ... WHERE Code LIKE '{TestRows.TagFamily}%' (child tables first) in that database only.");
        }
    }
}
