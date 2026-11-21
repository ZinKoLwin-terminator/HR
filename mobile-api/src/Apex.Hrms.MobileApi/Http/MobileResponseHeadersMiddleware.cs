using Microsoft.Net.Http.Headers;

namespace Apex.Hrms.MobileApi.Http;

/// <summary>
/// Adds the headers every response carries: no content sniffing, and no caching anywhere (the responses hold tokens and
/// personal HR data). Added when the response starts, so error responses get them too.
/// </summary>
internal sealed class MobileResponseHeadersMiddleware(RequestDelegate next)
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
