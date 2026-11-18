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

    public static void Add(RateLimiterOptions options, IConfiguration configuration) =>
        PerAccountUploadLimit.Add(options, Policy, PermitsPerMinute(configuration));
}

/// <summary>
/// The limiter shared by the KPI evidence and grievance uploads: a fixed window of a minute per signed-in account (the account id of the
/// session; a request without one falls back to its client IP and is refused by authorization anyway), no queue. Each policy has its
/// own counters.
/// </summary>
internal static class PerAccountUploadLimit
{
    public static void Add(RateLimiterOptions options, string policy, int permitsPerMinute)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(policy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            PartitionOf(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }

    public static string PartitionOf(HttpContext httpContext) =>
        httpContext.User is { Identity.IsAuthenticated: true } user && user.FindFirstValue(HrmsClaims.AccountId) is { Length: > 0 } account
            ? "user:" + account
            : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}

/// <summary>
/// The rate limit of the grievance uploads (the submitter's <c>POST /api/grievances/my/{id}/attachments</c> and the case team's
/// <c>POST /api/grievances/cases/{id}/attachments</c>): the same rule as the KPI evidence upload (per signed-in account, fixed window of a
/// minute, no queue, every attempt counts), with its own counter and setting <see cref="PermitsPerMinuteKey"/>.
/// </summary>
internal static class GrievanceUploadRateLimit
{
    public const string Policy = "grievance-file-upload";

    public const int DefaultPermitsPerMinute = 10;

    public const string PermitsPerMinuteKey = "Grievance:FileUploadsPerMinute";

    public const string RejectedMessage = "Too many file uploads. Wait a minute and try again.";

    public static int PermitsPerMinute(IConfiguration configuration) =>
        int.TryParse(configuration[PermitsPerMinuteKey], NumberStyles.None, CultureInfo.InvariantCulture, out var configured) && configured > 0
            ? configured
            : DefaultPermitsPerMinute;

    public static void Add(RateLimiterOptions options, IConfiguration configuration) =>
        PerAccountUploadLimit.Add(options, Policy, PermitsPerMinute(configuration));
}
