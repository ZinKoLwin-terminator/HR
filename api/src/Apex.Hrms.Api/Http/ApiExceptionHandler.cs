using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// Last line of defence. Malformed requests become a Validation problem; anything else becomes an Unknown problem
/// with a generic message. Only the exception type is logged, never its message, which could carry personal data.
/// </summary>
internal sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = exception is BadHttpRequestException
            ? AppError.Validation("The request body or parameters are malformed.")
            : AppError.Unknown("An unexpected error occurred.");

        if (error.Type == ErrorType.Unknown)
        {
            LogUnhandled(exception.GetType().Name);
        }

        var problem = ApiProblems.From(error);
        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled {ExceptionType} while handling a request")]
    private partial void LogUnhandled(string exceptionType);
}
