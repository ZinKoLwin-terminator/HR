using System.Data;
using Microsoft.Data.SqlClient;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Plain SQL against the test database, on its own connection. There is no ambient transaction: what a test writes here is committed
/// and visible to the handlers, and the test removes it again through its tag (see <see cref="TestRows"/>).
/// </summary>
internal static class Sql
{
    public static async Task<int> ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await using var command = Command(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<T> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await using var command = Command(connection, sql, parameters);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>The number of the SQL Server error a statement raises, or 0 when it succeeds.</summary>
    public static async Task<int> ErrorNumberOfAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        try
        {
            await ExecuteAsync(sql, parameters);
        }
        catch (SqlException exception)
        {
            return exception.Number;
        }

        return 0;
    }

    private static SqlCommand Command(SqlConnection connection, string sql, (string Name, object? Value)[] parameters)
    {
        var command = new SqlCommand(sql, connection) { CommandType = CommandType.Text };
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }
}
