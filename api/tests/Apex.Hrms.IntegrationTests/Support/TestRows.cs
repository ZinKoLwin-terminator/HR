using Microsoft.Data.SqlClient;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Finds and removes the rows the tests created. Every row a test creates carries a code that starts with the tag of
/// that test (<c>TST-</c> plus 8 hex characters), so each statement here is filtered by a tag and nothing else:
/// there is no TRUNCATE and no unfiltered DELETE.
/// </summary>
internal static class TestRows
{
    /// <summary>The start of every tag; what the fixture looks for to find rows a test left behind.</summary>
    public const string TagFamily = "TST-";

    /// <summary>Child tables first, so no delete is stopped by a foreign key from a row that is about to go.</summary>
    private static readonly string[] TablesChildToParent =
    [
        "org.Position", "org.Section", "org.Department", "org.Branch", "org.Company", "org.JobGrade",
    ];

    public static string NewTag() => TagFamily + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Deletes the rows whose code starts with <paramref name="tag"/>, followed by a dash.</summary>
    public static async Task DeleteAsync(string tag)
    {
        var pattern = tag + "-%";
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        foreach (var table in TablesChildToParent)
        {
            await using var command = new SqlCommand($"DELETE FROM {table} WHERE Code LIKE @pattern", connection);
            command.Parameters.AddWithValue("@pattern", pattern);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>How many rows per table have a code that starts with <paramref name="prefix"/>.</summary>
    public static async Task<IReadOnlyDictionary<string, int>> CountAsync(string prefix)
    {
        var pattern = prefix + "%";
        var counts = new Dictionary<string, int>();
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        foreach (var table in TablesChildToParent)
        {
            await using var command = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE Code LIKE @pattern", connection);
            command.Parameters.AddWithValue("@pattern", pattern);
            var count = (int)(await command.ExecuteScalarAsync())!;
            if (count != 0)
            {
                counts[table] = count;
            }
        }

        return counts;
    }
}
