using Microsoft.Data.SqlClient;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Finds and removes the rows the tests created. Every row a test creates carries a code that starts with the tag of
/// that test (<c>TST-</c> plus 8 hex characters), so each statement here is filtered by a tag and nothing else:
/// there is no TRUNCATE and no unfiltered DELETE. Employees are the exception to "the code starts with the tag": an
/// employee code holds at most 12 characters, so the tag is the start of the <em>work email</em> instead, and the
/// employee rows (with the rows that hang on them) are removed first, child to parent.
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

    /// <summary>The rows owned by an employee, removed before the employee (the database would cascade; this is explicit).</summary>
    private static readonly string[] EmployeeChildTables =
    [
        "hr.EmployeeDocument", "hr.EmploymentHistory", "hr.EmployeeBankAccount",
    ];

    private const string TaggedEmployees = "SELECT Id FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@pattern)";

    public static string NewTag() => TagFamily + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Deletes the rows whose code starts with <paramref name="tag"/>, followed by a dash.</summary>
    public static async Task DeleteAsync(string tag)
    {
        var pattern = tag + "-%";
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await DeleteEmployeesAsync(connection, pattern);
        foreach (var table in TablesChildToParent)
        {
            await using var command = new SqlCommand($"DELETE FROM {table} WHERE Code LIKE @pattern", connection);
            command.Parameters.AddWithValue("@pattern", pattern);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>How many rows per table have a code that starts with <paramref name="prefix"/> (employees: a work email that does).</summary>
    public static async Task<IReadOnlyDictionary<string, int>> CountAsync(string prefix)
    {
        var pattern = prefix + "%";
        var counts = new Dictionary<string, int>();
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await CountInto(counts, connection, "hr.Employee", "LOWER(WorkEmail) LIKE LOWER(@pattern)", pattern);
        foreach (var table in TablesChildToParent)
        {
            await CountInto(counts, connection, table, "Code LIKE @pattern", pattern);
        }

        return counts;
    }

    private static async Task DeleteEmployeesAsync(SqlConnection connection, string pattern)
    {
        // A tagged employee may manage another tagged one: release the self reference before deleting either.
        await ExecuteAsync(connection, pattern, $"UPDATE hr.Employee SET ManagerEmployeeId = NULL WHERE Id IN ({TaggedEmployees}) AND ManagerEmployeeId IS NOT NULL");
        foreach (var table in EmployeeChildTables)
        {
            await ExecuteAsync(connection, pattern, $"DELETE FROM {table} WHERE EmployeeId IN ({TaggedEmployees})");
        }

        await ExecuteAsync(connection, pattern, "DELETE FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@pattern)");
    }

    private static async Task ExecuteAsync(SqlConnection connection, string pattern, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pattern", pattern);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CountInto(Dictionary<string, int> counts, SqlConnection connection, string table, string condition, string pattern)
    {
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE {condition}", connection);
        command.Parameters.AddWithValue("@pattern", pattern);
        var count = (int)(await command.ExecuteScalarAsync())!;
        if (count != 0)
        {
            counts[table] = count;
        }
    }
}
