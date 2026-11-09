using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// Last line of defence. A client error raised while reading the request (malformed JSON, body too large ...) keeps
/// its own 4xx status and becomes a Validation problem; anything else becomes an Unknown problem with a generic
/// message. A request the client aborted is not an error and is not logged as one.
/// Logging never includes the exception message, because it can carry personal data (for example the value of a
/// duplicate key): only the exception type chain, the SQL error number and the stack trace are written.
/// </summary>
internal sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Non-standard status (as used by nginx) for "client closed the request"; nobody reads the response.</summary>
    internal const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = exception switch
        {
            BadHttpRequestException badRequest => ClientProblem(badRequest.StatusCode),

            // Code that saves audited rows without a signed-in user (handlers get a 401 result instead).
            UnauthenticatedWriteException => ApiProblems.From(AppError.Unauthorized(UnauthenticatedWriteException.PublicMessage)),
            _ => UnexpectedProblem(exception),
        };

        httpContext.Response.StatusCode = problem.Status!.Value;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
        return true;
    }

    private static ProblemDetails ClientProblem(int statusCode)
    {
        var message = statusCode switch
        {
            StatusCodes.Status413PayloadTooLarge => "The request body is too large.",
            StatusCodes.Status415UnsupportedMediaType => "The request content type is not supported.",
            StatusCodes.Status408RequestTimeout => "The request took too long to arrive.",
            _ => "The request body or parameters are malformed.",
        };

        var problem = ApiProblems.From(AppError.Validation(message));
        problem.Status = statusCode is >= 400 and < 500 ? statusCode : StatusCodes.Status400BadRequest;
        return problem;
    }

    private ProblemDetails UnexpectedProblem(Exception exception)
    {
        LogUnhandled(
            string.Join(" > ", Chain(exception).Select(inner => inner.GetType().Name)),
            DatabaseDiagnostics.SqlErrorNumber(exception),
            exception.StackTrace ?? string.Empty);
        return ApiProblems.From(AppError.Unknown("An unexpected error occurred."));
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
        Message = "Unhandled exception while handling a request. Types: {ExceptionTypes}. SQL error number: {SqlErrorNumber}. Stack trace: {StackTrace}")]
    private partial void LogUnhandled(string exceptionTypes, int? sqlErrorNumber, string stackTrace);
}
