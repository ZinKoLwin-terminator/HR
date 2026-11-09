namespace Apex.Hrms.Api.Auth;

/// <summary>
/// Cookie and header names. Outside Development the session cookie is <c>__Host-hrms</c> (HttpOnly, Secure,
/// SameSite=Strict, Path=/, no Domain: the browser enforces the prefix rules). In Development the API usually runs on plain
/// http, where a <c>__Host-</c> cookie would be rejected, so the same code path uses a name without the prefix
/// (<c>Auth:DevelopmentCookieName</c>, default <c>hrms-dev</c>) and the Secure flag follows the request scheme.
/// </summary>
internal static class AuthCookies
{
    public const string ProductionSessionName = "__Host-hrms";
    public const string DevelopmentSessionNameKey = "Auth:DevelopmentCookieName";
    public const string DevelopmentSessionNameDefault = "hrms-dev";

    /// <summary>The readable cookie that carries the request token; Angular copies it into the header.</summary>
    public const string XsrfTokenCookie = "XSRF-TOKEN";

    public const string XsrfHeader = "X-XSRF-TOKEN";

    public static string SessionName(IConfiguration configuration, bool isDevelopment) =>
        isDevelopment
            ? configuration[DevelopmentSessionNameKey] is { Length: > 0 } name ? name : DevelopmentSessionNameDefault
            : ProductionSessionName;

    /// <summary>The cookie that holds the secret half of the antiforgery token pair (HttpOnly).</summary>
    public static string AntiforgeryName(IConfiguration configuration, bool isDevelopment) =>
        isDevelopment
            ? $"{SessionName(configuration, isDevelopment)}-antiforgery"
            : "__Host-hrms-antiforgery";
}
