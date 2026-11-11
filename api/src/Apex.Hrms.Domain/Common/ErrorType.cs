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

    /// <summary>A needed service is not configured or reachable (HTTP 503, shown to the frontend as <c>Unknown</c>).</summary>
    Unavailable,
}
