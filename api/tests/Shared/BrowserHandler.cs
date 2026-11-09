using System.Net;

namespace Apex.Hrms.TestSupport;

/// <summary>
/// Makes an in-process test client behave like the Angular frontend: it keeps cookies and, on every unsafe method, copies
/// the <c>XSRF-TOKEN</c> cookie into the <c>X-XSRF-TOKEN</c> header (fetching the token first when it has none).
/// Turn the header off to test the rejection of a request without a token.
/// </summary>
internal sealed class BrowserHandler(HttpMessageHandler inner, bool sendXsrfHeader = true, Uri? origin = null) : DelegatingHandler(inner)
{
    public Uri Origin { get; } = origin ?? new("http://localhost/");

    public CookieContainer Cookies { get; } = new();

    public string? XsrfToken => Cookies.GetCookies(Origin)["XSRF-TOKEN"]?.Value is { } value ? WebUtility.UrlDecode(value) : null;

    public static HttpClient CreateClient(HttpMessageHandler serverHandler, out BrowserHandler handler, bool sendXsrfHeader = true, Uri? origin = null)
    {
        handler = new BrowserHandler(serverHandler, sendXsrfHeader, origin);
        return new HttpClient(handler) { BaseAddress = handler.Origin };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (sendXsrfHeader && IsUnsafe(request.Method) && XsrfToken is null)
        {
            using var fetch = new HttpRequestMessage(HttpMethod.Get, new Uri(Origin, "/api/auth/antiforgery"));
            using var fetched = await SendCoreAsync(fetch, cancellationToken);
        }

        if (sendXsrfHeader && IsUnsafe(request.Method) && XsrfToken is { } token)
        {
            request.Headers.Remove("X-XSRF-TOKEN");
            request.Headers.Add("X-XSRF-TOKEN", token);
        }

        return await SendCoreAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!.IsAbsoluteUri ? request.RequestUri : new Uri(Origin, request.RequestUri);
        request.RequestUri = uri;
        var cookieHeader = Cookies.GetCookieHeader(uri);
        if (cookieHeader.Length > 0)
        {
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", cookieHeader);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                Cookies.SetCookies(uri, setCookie);
            }
        }

        return response;
    }

    private static bool IsUnsafe(HttpMethod method) =>
        method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options && method != HttpMethod.Trace;
}
