using Microsoft.Data.SqlClient;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// The payroll part of the cleanup. Components and structures carry the tag in their code and salary assignments and payslips belong to
/// tagged employees, like the other rows. A payroll run has no code: the tests create theirs in the reserved test year
/// <see cref="PayrollTestYear"/>, long before any real period, and that year is what the cleanup and the leftover check look for.
/// The pay triggers lock an approved run, its payslips, lines and warnings, and make a paid run final, so a test that approved or paid
/// a run could never be cleaned up through the API. When such a run exists, the cleanup disables the four pay triggers for the time
/// of its own tagged deletes (in this throwaway test database only, and always enabling them again, even when a delete fails).
/// </summary>
internal static partial class TestRows
{
    /// <summary>The year of every payroll run the tests create (months 1 to 12). No real period is anywhere near it.</summary>
    public const int PayrollTestYear = 1999;

    private static readonly string[] PayTriggers =
    [
        "pay.TR_PayrollRun_PaidIsFinal ON pay.PayrollRun",
        "pay.TR_Payslip_PaidRunIsFinal ON pay.Payslip",
        "pay.TR_PayslipLine_PaidRunIsFinal ON pay.PayslipLine",
        "pay.TR_PayrollRunWarning_PaidRunIsFinal ON pay.PayrollRunWarning",
    ];

    private const string TaggedRuns =
        "SELECT Id FROM pay.PayrollRun WHERE [Year] = @year OR Id IN (SELECT PayrollRunId FROM pay.Payslip WHERE EmployeeId IN (SELECT Id FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@pattern)))";

    private const string TaggedPayslips =
        $"SELECT Id FROM pay.Payslip WHERE EmployeeId IN ({TaggedEmployees}) OR PayrollRunId IN ({TaggedRuns})";

    /// <summary>Child to parent, before the employees, structures and components they point at.</summary>
    private static async Task DeletePayrollAsync(SqlConnection connection, string pattern)
    {
        var locked = await ScalarAsync(
            connection,
            pattern,
            $"SELECT COUNT(*) FROM pay.PayrollRun WHERE Status IN ('Approved', 'Paid') AND Id IN ({TaggedRuns})");
        try
        {
            if (locked > 0)
            {
                await SetPayTriggersAsync(connection, enable: false);
            }

            await ExecuteWithYearAsync(connection, pattern, $"DELETE FROM pay.PayslipLine WHERE PayslipId IN ({TaggedPayslips})");
            await ExecuteWithYearAsync(connection, pattern, $"DELETE FROM pay.Payslip WHERE Id IN ({TaggedPayslips})");
            await ExecuteWithYearAsync(connection, pattern, $"DELETE FROM pay.PayrollRunWarning WHERE PayrollRunId IN ({TaggedRuns})");
            await ExecuteWithYearAsync(connection, pattern, $"DELETE FROM pay.PayrollRun WHERE Id IN ({TaggedRuns})");
        }
        finally
        {
            if (locked > 0)
            {
                await SetPayTriggersAsync(connection, enable: true);
            }
        }

        await ExecuteAsync(
            connection,
            pattern,
            $"DELETE FROM pay.SalaryAssignment WHERE EmployeeId IN ({TaggedEmployees}) OR SalaryStructureId IN (SELECT Id FROM pay.SalaryStructure WHERE Code LIKE @pattern)");
        await ExecuteAsync(
            connection,
            pattern,
            "DELETE FROM pay.SalaryStructureComponent WHERE SalaryStructureId IN (SELECT Id FROM pay.SalaryStructure WHERE Code LIKE @pattern) OR SalaryComponentId IN (SELECT Id FROM pay.SalaryComponent WHERE Code LIKE @pattern)");
        await ExecuteAsync(connection, pattern, "DELETE FROM pay.SalaryStructure WHERE Code LIKE @pattern");
        await ExecuteAsync(connection, pattern, "DELETE FROM pay.SalaryComponent WHERE Code LIKE @pattern");
    }

    private static async Task CountPayrollInto(Dictionary<string, int> counts, SqlConnection connection, string pattern)
    {
        await CountWithYearInto(counts, connection, "pay.PayrollRun", "[Year] = @year", pattern);
        await CountWithYearInto(counts, connection, "pay.Payslip", $"EmployeeId IN ({TaggedEmployees}) OR PayrollRunId IN ({TaggedRuns})", pattern);
        await CountInto(counts, connection, "pay.SalaryAssignment", $"EmployeeId IN ({TaggedEmployees})", pattern);
        await CountInto(counts, connection, "pay.SalaryStructure", "Code LIKE @pattern", pattern);
        await CountInto(counts, connection, "pay.SalaryComponent", "Code LIKE @pattern", pattern);
    }

    /// <summary>How many of the four pay triggers are disabled; the fixture requires 0 before and after a run.</summary>
    public static async Task<int> DisabledPayTriggersAsync()
    {
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.triggers WHERE is_disabled = 1 AND OBJECT_SCHEMA_NAME(parent_id) = 'pay'",
            connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task SetPayTriggersAsync(SqlConnection connection, bool enable)
    {
        foreach (var trigger in PayTriggers)
        {
            await using var command = new SqlCommand($"{(enable ? "ENABLE" : "DISABLE")} TRIGGER {trigger}", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string pattern, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@year", PayrollTestYear);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteWithYearAsync(SqlConnection connection, string pattern, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@year", PayrollTestYear);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CountWithYearInto(Dictionary<string, int> counts, SqlConnection connection, string table, string condition, string pattern)
    {
        await using var command = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE {condition}", connection);
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@year", PayrollTestYear);
        var count = (int)(await command.ExecuteScalarAsync())!;
        if (count != 0)
        {
            counts[table] = count;
        }
    }
}
