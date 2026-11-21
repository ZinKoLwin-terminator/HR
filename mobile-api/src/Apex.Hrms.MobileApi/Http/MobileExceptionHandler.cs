using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;

namespace Apex.Hrms.MobileApi.Http;

/// <summary>
/// Last line of defence, in the envelope shape. A client error raised while reading the request (malformed JSON, body too
/// large ...) keeps its 4xx status; anything else is a 500 with a generic message. A request the client aborted is not an
/// error. Logging never includes the exception message (it can carry personal data): only the type chain, the SQL error
/// number and the stack trace.
/// </summary>
internal sealed partial class MobileExceptionHandler(ILogger<MobileExceptionHandler> logger) : IExceptionHandler
{
    internal const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var (error, status) = exception switch
        {
            BadHttpRequestException badRequest when badRequest.StatusCode is >= 400 and < 500 =>
                (AppError.Validation(MobileResults.MessageFor(badRequest.StatusCode)), badRequest.StatusCode),
            UnauthenticatedWriteException =>
                (AppError.Unauthorized(UnauthenticatedWriteException.PublicMessage), StatusCodes.Status401Unauthorized),
            _ => (Unexpected(exception), StatusCodes.Status500InternalServerError),
        };

        await MobileResults.WriteErrorAsync(httpContext, error, status);
        return true;
    }

    private AppError Unexpected(Exception exception)
    {
        LogUnhandled(
            string.Join(" > ", Chain(exception).Select(inner => inner.GetType().Name)),
            DatabaseDiagnostics.SqlErrorNumber(exception),
            exception.StackTrace ?? string.Empty);
        return AppError.Unknown(MobileResults.MessageFor(StatusCodes.Status500InternalServerError));
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Unhandled exception while handling a mobile request. Types: {ExceptionTypes}. SQL error number: {SqlErrorNumber}. Stack trace: {StackTrace}")]
    private partial void LogUnhandled(string exceptionTypes, int? sqlErrorNumber, string stackTrace);
}
