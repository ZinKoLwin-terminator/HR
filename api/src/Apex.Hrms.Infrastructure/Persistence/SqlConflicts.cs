using Apex.Hrms.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>Translates SQL Server constraint violations into friendly Conflict errors (never the raw SQL message).</summary>
internal static class SqlConflicts
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int ConstraintConflict = 547;

    public static readonly AppError Concurrency = AppError.Conflict(
        "The record was changed or deleted by someone else. Reload it and try again.");

    private static readonly AppError Duplicate = AppError.Conflict(
        "A record with the same unique value already exists.");

    private static readonly AppError ReferenceViolation = AppError.Conflict(
        "The change conflicts with related records or database rules.");

    public static bool TryTranslate(DbUpdateException exception, out AppError conflict)
    {
        var translated = exception.InnerException is SqlException sql ? Translate(sql.Number) : null;
        conflict = translated!;
        return translated is not null;
    }

    private static AppError? Translate(int sqlErrorNumber) => sqlErrorNumber switch
    {
        UniqueIndexViolation or UniqueConstraintViolation => Duplicate,
        ConstraintConflict => ReferenceViolation,
        _ => null,
    };
}
