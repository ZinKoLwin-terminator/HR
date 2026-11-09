using Apex.Hrms.Api.Http;
using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace Apex.Hrms.Api.Auth;

/// <summary>
/// Validates the antiforgery token (header <c>X-XSRF-TOKEN</c>) on every unsafe method: POST, PUT, PATCH and DELETE
/// (and any other method that is not GET, HEAD, OPTIONS or TRACE). A missing or wrong token is a 400. The token is
/// bound to the signed-in user, so run it after authentication; fetch a new one after sign-in and sign-out.
/// </summary>
internal sealed partial class AntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery, ILogger<AntiforgeryMiddleware> logger)
{
    public const string RejectedMessage = "The request could not be verified. Reload the page and try again.";

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsUnsafe(context.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                LogRejected(context.Request.Method);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await Results.Problem(ApiProblems.From(AppError.Validation(RejectedMessage))).ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }

    private static bool IsUnsafe(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Antiforgery token missing or invalid on a {Method} request.")]
    private partial void LogRejected(string method);
}
