using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.UseCases;

/// <summary>The optimistic-concurrency token as the API shows it: the 8-byte SQL rowversion as a base64 string.</summary>
public static class RowVersionToken
{
    public const string Field = "rowVersion";

    private const int RowVersionLength = 8;

    /// <summary>The conflict returned when the token a client sent no longer matches the stored record.</summary>
    public static readonly AppError Stale = AppError.Conflict(
        "The record was changed or deleted by someone else. Reload it and try again.");

    /// <summary>A missing or malformed token is a Validation error on <c>rowVersion</c>.</summary>
    public static Result<byte[]> Decode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Fail("Row version is required. Reload the record to get its current value.");
        }

        var buffer = new byte[RowVersionLength + 1];
        if (!Convert.TryFromBase64String(text.Trim(), buffer, out var written) || written != RowVersionLength)
        {
            return Fail("Row version is not valid. Use the value returned by the API.");
        }

        return Result.Success(buffer[..RowVersionLength]);
    }

    private static Result<byte[]> Fail(string message) =>
        Result.Failure<byte[]>(AppError.Validation(message, Field, message));
}
