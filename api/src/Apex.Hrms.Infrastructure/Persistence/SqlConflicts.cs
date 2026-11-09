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
