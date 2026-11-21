using System.Diagnostics;
using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Apex.Hrms.MobileApi.Http;

/// <summary>
/// The body of every JSON response of the mobile API (hr_employee_app/docs/api_contracts.md §1.1 and §1.2):
/// <c>{ success, message, data, errors, traceId }</c>. <c>errors</c> maps a camelCase field name to its messages.
/// </summary>
internal sealed record ApiEnvelope(
    bool Success,
    string? Message,
    object? Data,
    IReadOnlyDictionary<string, string[]>? Errors,
    string? TraceId);

/// <summary>Builds the enveloped results of the mobile endpoints.</summary>
internal static class MobileResults
{
    public static IResult Ok(object data) => new EnvelopeResult(StatusCodes.Status200OK, new ApiEnvelope(true, null, data, null, null));

    public static IResult NoContent() => Results.NoContent();

    /// <summary>The 401 of a signed-in endpoint whose token names no session.</summary>
    public static IResult SessionEnded() => Error(AppError.Unauthorized(Apex.Hrms.Mobile.Application.Auth.MobileAuthMessages.SessionEnded));

    /// <summary>A use case result as the envelope: 200 with the value, or the status of its error.</summary>
    public static IResult ToEnvelope<T>(this Result<T> result) where T : notnull =>
        result.IsSuccess ? Ok(result.Value) : Error(result.Error);

    /// <summary>A use case result that answers nothing on success: 204, or the status of its error.</summary>
    public static IResult ToNoContent(this Result result) =>
        result.IsSuccess ? NoContent() : Error(result.Error);

    public static IResult Error(AppError error) => Error(error, StatusCodeFor(error.Type));

    public static IResult Error(AppError error, int statusCode) =>
        new EnvelopeResult(statusCode, new ApiEnvelope(false, error.Message, null, error.FieldErrors, null));

    /// <summary>Writes an error envelope from places that are not endpoints (authentication events, the rate limiter, the exception handler).</summary>
    public static Task WriteErrorAsync(HttpContext http, AppError error, int statusCode) =>
        Error(error, statusCode).ExecuteAsync(http);

    public static string TraceId(HttpContext http) => Activity.Current?.Id ?? http.TraceIdentifier;

    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>The generic message of a status that has no message of its own (an unknown route, a wrong method ...).</summary>
    public static string MessageFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "The request body or parameters are malformed.",
        StatusCodes.Status401Unauthorized => "Your session has expired. Please sign in again.",
        StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
        StatusCodes.Status404NotFound => "The requested item was not found.",
        StatusCodes.Status405MethodNotAllowed => "This method is not allowed here.",
        StatusCodes.Status413PayloadTooLarge => "The request body is too large.",
        StatusCodes.Status415UnsupportedMediaType => "The request content type is not supported.",
        StatusCodes.Status429TooManyRequests => "Too many attempts. Wait a minute and try again.",
        _ when statusCode >= 500 => "An unexpected error occurred.",
        _ => "The request could not be completed.",
    };

    private sealed class EnvelopeResult(int statusCode, ApiEnvelope envelope) : IResult, IStatusCodeHttpResult
    {
        public int? StatusCode => statusCode;

        public Task ExecuteAsync(HttpContext httpContext)
        {
            var options = httpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
            httpContext.Response.StatusCode = statusCode;
            return httpContext.Response.WriteAsJsonAsync(envelope with { TraceId = TraceId(httpContext) }, options, httpContext.RequestAborted);
        }
    }
}
