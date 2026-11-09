using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// Builds RFC 9457 problem details. Extension <c>code</c> is one of NotFound, Validation, Conflict, Unauthorized,
/// Forbidden, Unknown; extension <c>errors</c> maps a field to its messages; extension <c>traceId</c> correlates the
/// response with the server log. The frontend maps them to AppError.
/// </summary>
internal static class ApiProblems
{
    public const string CodeKey = "code";
    public const string ErrorsKey = "errors";
    public const string TraceIdKey = "traceId";

    public static ProblemDetails From(AppError error)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodeFor(error.Type),
            Title = TitleFor(error.Type),
            Detail = error.Message,
        };
        problem.Extensions[CodeKey] = error.Type.ToString();
        if (error.FieldErrors is { Count: > 0 })
        {
            problem.Extensions[ErrorsKey] = error.FieldErrors;
        }

        return problem;
    }

    public static ErrorType TypeFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => ErrorType.Validation,
        StatusCodes.Status401Unauthorized => ErrorType.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorType.Forbidden,
        StatusCodes.Status404NotFound => ErrorType.NotFound,
        StatusCodes.Status409Conflict => ErrorType.Conflict,
        _ => ErrorType.Unknown,
    };

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Validation failed",
        ErrorType.NotFound => "Not found",
        ErrorType.Conflict => "Conflict",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        _ => "Unexpected error",
    };
}
