namespace Apex.Hrms.Domain.Common;

/// <summary>
/// Describes why an operation failed. <see cref="FieldErrors"/> maps a camelCase field name to its messages
/// and is only set for validation failures.
/// </summary>
public sealed record AppError(
    ErrorType Type,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static AppError NotFound(string message) => new(ErrorType.NotFound, message);

    public static AppError Conflict(string message) => new(ErrorType.Conflict, message);

    public static AppError Unauthorized(string message) => new(ErrorType.Unauthorized, message);

    public static AppError Forbidden(string message) => new(ErrorType.Forbidden, message);

    public static AppError Unknown(string message) => new(ErrorType.Unknown, message);

    public static AppError Unavailable(string message) => new(ErrorType.Unavailable, message);

    public static AppError Validation(string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null) =>
        new(ErrorType.Validation, message, fieldErrors);

    /// <summary>A validation failure caused by one field.</summary>
    public static AppError Validation(string message, string field, string fieldMessage) =>
        new(ErrorType.Validation, message, new Dictionary<string, string[]> { [field] = [fieldMessage] });
}
