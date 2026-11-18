using Microsoft.Data.SqlClient;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// The grievance part of the cleanup. Every grievance a test creates uses a category whose code carries the tag, so the tagged cases are
/// the cases of tagged categories (or submitted by, or about, a tagged employee). The append-only tables are denied UPDATE and DELETE
/// for <c>hrms_api</c> only; the tests run as the database owner. A submitted case can never be deleted (TR_Case_SubmittedIsFixed) and a
/// Closed one is final (TR_Case_ClosedIsFinal), so, like the pay triggers, the cleanup disables the two grv.Case triggers for the time of
/// its own tagged deletes (in this throwaway test database only, and always enabling them again, even when a delete fails).
/// </summary>
internal static partial class TestRows
{
    private static readonly string[] GrievanceTriggers =
    [
        "grv.TR_Case_ClosedIsFinal ON grv.[Case]",
        "grv.TR_Case_SubmittedIsFixed ON grv.[Case]",
        "grv.TR_Attachment_Fixed ON grv.Attachment",
    ];

    private const string TaggedGrievanceCategories = "SELECT Id FROM grv.Category WHERE Code LIKE @pattern";

    private const string TaggedGrievances =
        $"SELECT Id FROM grv.[Case] WHERE CategoryId IN ({TaggedGrievanceCategories}) OR SubjectEmployeeId IN ({TaggedEmployees}) " +
        $"OR Id IN (SELECT CaseId FROM grv.CaseSubmitter WHERE EmployeeId IN ({TaggedEmployees}))";

    /// <summary>Child to parent, before the employees and the organization the cases point at.</summary>
    private static async Task DeleteGrievancesAsync(SqlConnection connection, string pattern)
    {
        // A database deployed before the grv schema existed has nothing to clean.
        if (await ScalarAsync(connection, pattern, "SELECT CASE WHEN OBJECT_ID(N'grv.[Case]') IS NULL THEN 0 ELSE 1 END") == 0)
        {
            return;
        }

        var cases = await ScalarAsync(connection, pattern, $"SELECT COUNT(*) FROM grv.[Case] WHERE Id IN ({TaggedGrievances})");
        if (cases == 0)
        {
            await ExecuteAsync(connection, pattern, $"DELETE FROM grv.Category WHERE Id IN ({TaggedGrievanceCategories})");
            return;
        }

        // The case ids are read first: deleting grv.CaseSubmitter would otherwise lose the cases found through their submitter.
        var ids = new List<int>();
        await using (var select = new SqlCommand($"SELECT Id FROM grv.[Case] WHERE Id IN ({TaggedGrievances})", connection))
        {
            select.Parameters.AddWithValue("@pattern", pattern);
            await using var reader = await select.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetInt32(0));
            }
        }

        var list = string.Join(',', ids.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        try
        {
            await SetGrievanceTriggersAsync(connection, enable: false);
            await ExecuteAsync(connection, pattern, $"DELETE FROM app.Notification WHERE Title IN (SELECT 'Grievance ' + CaseNo FROM grv.[Case] WHERE Id IN ({list}) AND CaseNo IS NOT NULL)");
            foreach (var table in new[] { "grv.Attachment", "grv.Communication", "grv.InvestigationNote", "grv.StatusHistory", "grv.Assignment", "grv.Escalation", "grv.ResolutionProposal", "grv.CorrectiveAction", "grv.CaseSubmitter", "grv.AuditLog" })
            {
                await ExecuteAsync(connection, pattern, $"DELETE FROM {table} WHERE CaseId IN ({list})");
            }

            await ExecuteAsync(connection, pattern, $"DELETE FROM grv.[Case] WHERE Id IN ({list})");
        }
        finally
        {
            await SetGrievanceTriggersAsync(connection, enable: true);
        }

        await ExecuteAsync(connection, pattern, $"DELETE FROM grv.Category WHERE Id IN ({TaggedGrievanceCategories})");
    }

    private static async Task CountGrievancesInto(Dictionary<string, int> counts, SqlConnection connection, string pattern)
    {
        if (await ScalarAsync(connection, pattern, "SELECT CASE WHEN OBJECT_ID(N'grv.[Case]') IS NULL THEN 0 ELSE 1 END") == 0)
        {
            return;
        }

        await CountInto(counts, connection, "grv.Category", "Code LIKE @pattern", pattern);
        await CountInto(counts, connection, "grv.[Case]", $"Id IN ({TaggedGrievances})", pattern);
    }

    private static async Task SetGrievanceTriggersAsync(SqlConnection connection, bool enable)
    {
        foreach (var trigger in GrievanceTriggers)
        {
            await using var command = new SqlCommand($"{(enable ? "ENABLE" : "DISABLE")} TRIGGER {trigger}", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
