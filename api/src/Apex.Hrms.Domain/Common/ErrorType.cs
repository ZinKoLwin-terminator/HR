namespace Apex.Hrms.Domain.Common;

/// <summary>Failure categories shared with the frontend's <c>AppError.code</c>.</summary>
public enum ErrorType
{
    NotFound,
    Validation,
    Conflict,
    Unauthorized,
    Forbidden,
    Unknown,
}
