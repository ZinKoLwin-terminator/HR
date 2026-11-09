using Microsoft.Net.Http.Headers;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// Adds the headers every API response carries: no content sniffing, and no caching by browsers or proxies (the
/// responses hold personal HR data). Added when the response starts, so error responses get them too.
/// </summary>
internal sealed class ApiResponseHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers[HeaderNames.CacheControl] = "no-store";
            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
