using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Apex.Hrms.Api.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace Apex.Hrms.Api.Http;

/// <summary>
/// The rate limit of the KPI evidence upload (<c>POST /api/kpi/scorecard-items/{itemId}/attachments</c>): each signed-in account may
/// start at most <see cref="DefaultPermitsPerMinute"/> uploads per minute (fixed window, no queue; configurable with
/// <see cref="PermitsPerMinuteKey"/>). Every attempt counts, whatever its answer, so a client cannot keep the server reading 10 MB
/// bodies in a loop. A refusal is 429 with ProblemDetails and <c>Retry-After</c>. The partition is the account id of the session,
/// which needs the rate limiter to run after authentication (see the pipeline); a request without a session falls back to its client IP
/// and is then refused by authorization anyway. The counters live in memory per instance.
/// </summary>
internal static class EvidenceUploadRateLimit
{
    public const string Policy = "kpi-evidence-upload";

    public const int DefaultPermitsPerMinute = 10;

    public const string PermitsPerMinuteKey = "Kpi:EvidenceUploadsPerMinute";

    public const string RejectedMessage = "Too many evidence uploads. Wait a minute and try again.";

    /// <summary>The configured permits per minute: a positive whole number, otherwise the default.</summary>
    public static int PermitsPerMinute(IConfiguration configuration) =>
        int.TryParse(configuration[PermitsPerMinuteKey], NumberStyles.None, CultureInfo.InvariantCulture, out var configured) && configured > 0
            ? configured
            : DefaultPermitsPerMinute;

    public static void Add(RateLimiterOptions options, IConfiguration configuration)
    {
        var permits = PermitsPerMinute(configuration);
        options.AddPolicy(Policy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            PartitionOf(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }

    private static string PartitionOf(HttpContext httpContext) =>
        httpContext.User is { Identity.IsAuthenticated: true } user && user.FindFirstValue(HrmsClaims.AccountId) is { Length: > 0 } account
            ? "user:" + account
            : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
