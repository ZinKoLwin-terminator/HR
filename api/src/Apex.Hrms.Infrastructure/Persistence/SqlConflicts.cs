using System.Globalization;
using System.Text.RegularExpressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Translates SQL Server errors raised while saving into AppError values. The raw SQL message is only read to find
/// the constraint name (it follows the FK_, UQ_ and CK_ naming convention of the database project) and is never
/// copied into the result: it can contain the duplicate value, which may be personal data.
/// </summary>
internal static partial class SqlConflicts
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int ConstraintConflict = 547;

    // THROW numbers raised by the payroll finality triggers in the database project (pay schema).
    private static readonly Dictionary<int, string> FinalityMessages = new Dictionary<int, string>
    {
        [51001] = "A paid payroll run is final and cannot be changed or deleted.",
        [51002] = "Payslips of a paid payroll run are final.",
        [51003] = "Payslip lines of a paid payroll run are final.",
        [51004] = "Warnings of a paid payroll run are final.",
    };

    // Check constraints whose name does not end in the field they guard.
    private static readonly Dictionary<string, string> CheckFields = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CK_JobGrade_SalaryBand"] = "maxSalary",
        ["CK_JobGrade_MinSalary"] = "minSalary",
        ["CK_Position_ReportsToNotSelf"] = "reportsToPositionId",
        ["CK_Employee_ManagerNotSelf"] = "managerId",
        ["CK_Employee_ExitDateRequired"] = "exitDate",
        ["CK_Employee_EmergencyContactRelationship"] = "emergencyContact.relationship",
        ["CK_LeaveApplication_DateOrder"] = "endDate",
        ["CK_LeaveApplication_HalfDay"] = "endDate",
        ["CK_LeaveApplication_RejectionReason"] = "remarks",
    };

    // Unique indexes and checks whose name does not end in a field, or that are conflicts rather than bad input.
    private static readonly Dictionary<string, AppError> NamedConstraints = new Dictionary<string, AppError>(StringComparer.Ordinal)
    {
        ["UQ_LeavePolicy_ActiveGroup"] = AppError.Validation(
            "An active policy already exists for this leave type, grade and employment type.",
            "leaveTypeId",
            "Another active policy covers the same group."),
        ["UQ_LeaveApplication_ApplicationNo"] = AppError.Conflict("That application number is already in use. Try again."),
        ["CK_LeaveApplication_NotOwnApprover"] = AppError.Conflict("You cannot decide your own leave request."),
        ["UQ_AttendanceRecord_EmployeeId_Date"] = AppError.Conflict("Attendance for this employee and day was just recorded. Reload and try again."),
        ["UQ_AttendanceAdjustment_EmployeeId_Date_Pending"] = AppError.Validation(
            "A pending request already exists for this employee and date.",
            "date",
            "A pending request already exists for this date."),
        ["UQ_JobOpening_Code"] = AppError.Conflict("That job opening code is already in use. Try again."),
        ["UQ_Application_JobOpeningId_CandidateId"] = AppError.Validation(
            "This candidate has already applied for this job.",
            "candidateId",
            "Already applied for this opening."),
        ["CK_Application_RejectionReason"] = AppError.Validation(
            "Enter a reason for rejecting this application.",
            "reason",
            "A reason is required."),
        ["CK_Interview_CompletedOutcome"] = AppError.Validation(
            "A completed interview needs a score and a recommendation; other interviews have neither.",
            "score",
            "Check the score and recommendation."),
        ["UQ_OvertimeRecord_EmployeeId_Date"] = AppError.Validation(
            "Overtime is already recorded for this employee on that date.",
            "date",
            "Already recorded for this date."),
    };

    private static readonly AppError ReferenceViolation = AppError.Conflict(
        "The change conflicts with related records.");

    private static readonly AppError Duplicate = AppError.Conflict(
        "A record with the same unique value already exists.");

    public static bool TryTranslate(DbUpdateException exception, out AppError error)
    {
        var translated = exception.InnerException is SqlException sql ? Translate(sql.Number, sql.Message) : null;
        error = translated!;
        return translated is not null;
    }

    /// <summary>Returns null for errors that are not constraint violations (they stay unexpected failures).</summary>
    internal static AppError? Translate(int sqlErrorNumber, string sqlMessage)
    {
        if (FinalityMessages.TryGetValue(sqlErrorNumber, out var finality))
        {
            return AppError.Conflict(finality);
        }

        var constraint = ConstraintName(sqlMessage);
        if (constraint is not null
            && sqlErrorNumber is UniqueIndexViolation or UniqueConstraintViolation or ConstraintConflict
            && NamedConstraints.TryGetValue(constraint, out var named))
        {
            return named;
        }

        return sqlErrorNumber switch
        {
            UniqueIndexViolation or UniqueConstraintViolation => Unique(constraint),
            ConstraintConflict => Constraint(constraint),
            _ => null,
        };
    }

    /// <summary>UQ_Company_Code becomes a validation error on code, UQ_Company_Name one on name.</summary>
    private static AppError Unique(string? constraint)
    {
        var field = FieldOf(constraint, "UQ_");
        if (field is null)
        {
            return Duplicate;
        }

        var words = Words(field);
        return AppError.Validation(
            $"A record with the same {words} already exists.",
            field,
            $"{char.ToUpper(words[0], CultureInfo.InvariantCulture)}{words[1..]} must be unique.");
    }

    /// <summary>A CHECK violation is bad input (validation); a FOREIGN KEY or REFERENCE violation is a conflict with other records.</summary>
    private static AppError Constraint(string? constraint)
    {
        if (constraint is null || !constraint.StartsWith("CK_", StringComparison.Ordinal))
        {
            return ReferenceViolation;
        }

        var field = CheckFields.TryGetValue(constraint, out var mapped) ? mapped : FieldOf(constraint, "CK_");
        return field is null
            ? AppError.Validation("A value is not allowed by a database rule.")
            : AppError.Validation("A value is not allowed by a database rule.", field, "This value is not allowed.");
    }

    /// <summary>Reads the field from a Prefix_Table_Field name; other shapes (composite keys) have no single field.</summary>
    private static string? FieldOf(string? constraint, string prefix)
    {
        if (constraint is null || !constraint.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = constraint.Split('_');
        return parts.Length == 3 ? char.ToLower(parts[2][0], CultureInfo.InvariantCulture) + parts[2][1..] : null;
    }

    private static string Words(string camelCase) =>
        CamelBoundary().Replace(camelCase, " $1").ToLower(CultureInfo.InvariantCulture);

    private static string? ConstraintName(string message)
    {
        var match = ConstraintPattern().Match(message);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"(?:UQ|FK|CK)_[A-Za-z0-9_]+", RegexOptions.CultureInvariant)]
    private static partial Regex ConstraintPattern();

    [GeneratedRegex(@"([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex CamelBoundary();
}

/// <summary>Reads SQL Server diagnostics from an exception chain without exposing its message.</summary>
public static class DatabaseDiagnostics
{
    /// <summary>The SQL Server error number found in the exception chain, or null when no SQL error is involved.</summary>
    public static int? SqlErrorNumber(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql.Number;
            }
        }

        return null;
    }
}
