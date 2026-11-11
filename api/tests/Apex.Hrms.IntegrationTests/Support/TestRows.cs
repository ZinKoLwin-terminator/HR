using Microsoft.Data.SqlClient;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>
/// Finds and removes the rows the tests created. Every row a test creates carries a code that starts with the tag of
/// that test (<c>TST-</c> plus 8 hex characters), so each statement here is filtered by a tag and nothing else:
/// there is no TRUNCATE and no unfiltered DELETE. Employees are the exception to "the code starts with the tag": an
/// employee code holds at most 12 characters, so the tag is the start of the <em>work email</em> instead, and the
/// employee rows (with the rows that hang on them) are removed first, child to parent. Sign-in accounts follow the same
/// rule through their <em>email</em> (auth.User and sec.AppUser; removed before the employees they may link to) and
/// test roles through their name (sec.Role).
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

    /// <summary>The rows owned by an identity user, removed before the user (the database would cascade; this is explicit).</summary>
    private static readonly string[] IdentityChildTables =
    [
        "auth.UserToken", "auth.UserLogin", "auth.UserClaim",
    ];

    private const string TaggedEmployees = "SELECT Id FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@pattern)";

    private const string TaggedAppUsers = "SELECT Id FROM sec.AppUser WHERE LOWER(Email) LIKE LOWER(@pattern)";

    private const string TaggedIdentityUsers = "SELECT Id FROM auth.[User] WHERE LOWER(Email) LIKE LOWER(@pattern)";

    public static string NewTag() => TagFamily + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Deletes the rows whose code starts with <paramref name="tag"/>, followed by a dash.</summary>
    public static async Task DeleteAsync(string tag)
    {
        var pattern = tag + "-%";
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await DeleteActivityAndNotificationsAsync(connection, tag, pattern);
        await DeleteAccountsAsync(connection, pattern);
        await DeleteLeaveAsync(connection, pattern);
        await DeleteAttendanceOfEmployeesAsync(connection, pattern);
        await DeleteRecruitmentAsync(connection, pattern);
        await DeleteEmployeesAsync(connection, pattern);
        await DeleteShiftsAsync(connection, pattern);
        foreach (var table in TablesChildToParent)
        {
            await using var command = new SqlCommand($"DELETE FROM {table} WHERE Code LIKE @pattern", connection);
            command.Parameters.AddWithValue("@pattern", pattern);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>How many rows per table have a code that starts with <paramref name="prefix"/> (employees and accounts: an email that does; roles: a name).</summary>
    public static async Task<IReadOnlyDictionary<string, int>> CountAsync(string prefix)
    {
        var pattern = prefix + "%";
        var counts = new Dictionary<string, int>();
        await using var connection = new SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await CountInto(counts, connection, "auth.[User]", "LOWER(Email) LIKE LOWER(@pattern)", pattern);
        await CountInto(counts, connection, "sec.AppUser", "LOWER(Email) LIKE LOWER(@pattern)", pattern);
        await CountInto(counts, connection, "sec.Role", "Name LIKE @pattern", pattern);
        await CountInto(counts, connection, "app.ActivityLog", $"ActorName LIKE @pattern OR Message LIKE '%' + @pattern OR ActorUserId IN ({TaggedAppUsers})", pattern);
        await CountInto(counts, connection, "app.Notification", $"Title LIKE @pattern OR RecipientUserId IN ({TaggedAppUsers})", pattern);
        await CountInto(counts, connection, "hr.Employee", "LOWER(WorkEmail) LIKE LOWER(@pattern)", pattern);
        await CountInto(counts, connection, "lve.LeaveApplication", $"EmployeeId IN ({TaggedEmployees})", pattern);
        await CountInto(counts, connection, "lve.LeavePolicy", "Name LIKE @pattern", pattern);
        await CountInto(counts, connection, "lve.LeaveType", "Code LIKE @pattern", pattern);
        await CountInto(counts, connection, "att.Holiday", "Name LIKE @pattern", pattern);
        await CountInto(counts, connection, "att.Shift", "Code LIKE @pattern", pattern);
        await CountInto(counts, connection, "att.AttendanceRecord", $"EmployeeId IN ({TaggedEmployees}) OR ShiftId IN ({TaggedShifts})", pattern);
        await CountInto(counts, connection, "att.AttendanceAdjustment", $"EmployeeId IN ({TaggedEmployees})", pattern);
        await CountInto(counts, connection, "att.OvertimeRecord", $"EmployeeId IN ({TaggedEmployees})", pattern);
        await CountInto(counts, connection, "rec.Interview", $"ApplicationId IN ({TaggedApplications}) OR InterviewerEmployeeId IN ({TaggedEmployees})", pattern);
        await CountInto(counts, connection, "rec.Application", $"Id IN ({TaggedApplications})", pattern);
        await CountInto(counts, connection, "rec.JobOpening", $"Id IN ({TaggedJobs})", pattern);
        await CountInto(counts, connection, "rec.Candidate", "LOWER(Email) LIKE LOWER(@pattern)", pattern);
        foreach (var table in TablesChildToParent)
        {
            await CountInto(counts, connection, table, "Code LIKE @pattern", pattern);
        }

        return counts;
    }

    /// <summary>
    /// The activity entries and notifications a test caused, before the accounts they point at. An entry is a test's when its
    /// actor name starts with the tag (calls through the handlers), its message holds the tag or the tag's code form (names and
    /// codes of the rows it created) or it was written by a tagged account (calls through HTTP). A notification is a test's when its
    /// title starts with the tag or it is for a tagged account.
    /// </summary>
    private static async Task DeleteActivityAndNotificationsAsync(SqlConnection connection, string tag, string pattern)
    {
        var code = tag[TagFamily.Length..];
        await using var notifications = new SqlCommand(
            $"DELETE FROM app.Notification WHERE Title LIKE @titlePattern OR RecipientUserId IN ({TaggedAppUsers})",
            connection);
        notifications.Parameters.AddWithValue("@titlePattern", tag + "%");
        notifications.Parameters.AddWithValue("@pattern", pattern);
        await notifications.ExecuteNonQueryAsync();

        await using var activity = new SqlCommand(
            $"DELETE FROM app.ActivityLog WHERE ActorName LIKE @actor OR Message LIKE @message OR Message LIKE @codeMessage OR ActorUserId IN ({TaggedAppUsers})",
            connection);
        activity.Parameters.AddWithValue("@actor", tag + "%");
        activity.Parameters.AddWithValue("@message", "%" + tag + "%");
        activity.Parameters.AddWithValue("@codeMessage", "%\"" + code + "-%");
        activity.Parameters.AddWithValue("@pattern", pattern);
        await activity.ExecuteNonQueryAsync();
    }

    /// <summary>Child to parent: identity children, the business account (it points at the identity user), the identity user, then test roles.</summary>
    private static async Task DeleteAccountsAsync(SqlConnection connection, string pattern)
    {
        foreach (var table in IdentityChildTables)
        {
            await ExecuteAsync(connection, pattern, $"DELETE FROM {table} WHERE UserId IN ({TaggedIdentityUsers})");
        }

        await ExecuteAsync(connection, pattern, "DELETE FROM sec.AppUser WHERE LOWER(Email) LIKE LOWER(@pattern)");
        await ExecuteAsync(connection, pattern, "DELETE FROM auth.[User] WHERE LOWER(Email) LIKE LOWER(@pattern)");
        await ExecuteAsync(connection, pattern, "DELETE FROM sec.Role WHERE Name LIKE @pattern");
    }

    /// <summary>
    /// Child to parent: the applications of tagged employees or tagged leave types, the policies of tagged leave types or
    /// job grades, the tagged leave types, and the tagged holidays. Before the employees and the organization rows they point at.
    /// </summary>
    private static async Task DeleteLeaveAsync(SqlConnection connection, string pattern)
    {
        const string TaggedTypes = "SELECT Id FROM lve.LeaveType WHERE Code LIKE @pattern";
        await ExecuteAsync(
            connection,
            pattern,
            $"DELETE FROM lve.LeaveApplication WHERE EmployeeId IN ({TaggedEmployees}) OR ApproverEmployeeId IN ({TaggedEmployees}) OR LeaveTypeId IN ({TaggedTypes})");
        await ExecuteAsync(
            connection,
            pattern,
            $"DELETE FROM lve.LeavePolicy WHERE LeaveTypeId IN ({TaggedTypes}) OR JobGradeId IN (SELECT Id FROM org.JobGrade WHERE Code LIKE @pattern)");
        await ExecuteAsync(connection, pattern, "DELETE FROM lve.LeaveType WHERE Code LIKE @pattern");
        await ExecuteAsync(connection, pattern, "DELETE FROM att.Holiday WHERE Name LIKE @pattern");
    }

    // Job openings carry a server-made code, so a test tags them through the title (or the manager or department it made);
    // candidates through the email, like employees.
    private const string TaggedJobs =
        "SELECT Id FROM rec.JobOpening WHERE Title LIKE @pattern OR HiringManagerEmployeeId IN (SELECT Id FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@pattern)) OR DepartmentId IN (SELECT Id FROM org.Department WHERE Code LIKE @pattern)";

    private const string TaggedCandidates = "SELECT Id FROM rec.Candidate WHERE LOWER(Email) LIKE LOWER(@pattern)";

    private const string TaggedApplications =
        $"SELECT Id FROM rec.Application WHERE JobOpeningId IN ({TaggedJobs}) OR CandidateId IN ({TaggedCandidates})";

    /// <summary>
    /// Child to parent, before the employees and departments they point at: interviews (of tagged applications or tagged
    /// interviewers), applications (of tagged openings or candidates), then the openings and the candidates.
    /// </summary>
    private static async Task DeleteRecruitmentAsync(SqlConnection connection, string pattern)
    {
        await ExecuteAsync(
            connection,
            pattern,
            $"DELETE FROM rec.Interview WHERE ApplicationId IN ({TaggedApplications}) OR InterviewerEmployeeId IN ({TaggedEmployees})");
        await ExecuteAsync(connection, pattern, $"DELETE FROM rec.Application WHERE Id IN ({TaggedApplications})");
        await ExecuteAsync(connection, pattern, $"DELETE FROM rec.JobOpening WHERE Id IN ({TaggedJobs})");
        await ExecuteAsync(connection, pattern, "DELETE FROM rec.Candidate WHERE LOWER(Email) LIKE LOWER(@pattern)");
    }

    private const string TaggedShifts = "SELECT Id FROM att.Shift WHERE Code LIKE @pattern";

    /// <summary>
    /// Child to parent, before the employees (and the shifts) they point at: adjustment requests (they reference the record of
    /// their employee and day), then the day records of tagged employees or tagged shifts, then overtime entries.
    /// </summary>
    private static async Task DeleteAttendanceOfEmployeesAsync(SqlConnection connection, string pattern)
    {
        await ExecuteAsync(connection, pattern, $"DELETE FROM att.AttendanceAdjustment WHERE EmployeeId IN ({TaggedEmployees})");
        await ExecuteAsync(connection, pattern, $"DELETE FROM att.AttendanceRecord WHERE EmployeeId IN ({TaggedEmployees}) OR ShiftId IN ({TaggedShifts})");
        await ExecuteAsync(connection, pattern, $"DELETE FROM att.OvertimeRecord WHERE EmployeeId IN ({TaggedEmployees})");
    }

    /// <summary>The tagged shifts, after the employees and day records that point at them (the work days go with the shift).</summary>
    private static async Task DeleteShiftsAsync(SqlConnection connection, string pattern)
    {
        await ExecuteAsync(connection, pattern, $"DELETE FROM att.ShiftWorkDay WHERE ShiftId IN ({TaggedShifts})");
        await ExecuteAsync(connection, pattern, "DELETE FROM att.Shift WHERE Code LIKE @pattern");
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
