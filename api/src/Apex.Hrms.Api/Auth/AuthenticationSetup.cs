using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Api.Auth;

/// <summary>
/// Registers everything for cookie sign-in: the session cookie (same-site, HttpOnly), antiforgery, the per-IP rate limit
/// of the sign-in endpoints, the Identity token providers and the HttpContext based current user.
/// </summary>
internal static class AuthenticationSetup
{
    public const string RateLimitPolicy = "auth-endpoints";
    public const int DefaultPermitsPerMinute = 10;
    public const string PermitsPerMinuteKey = "Auth:RateLimit:PermitsPerMinute";

    public static IServiceCollection AddHrmsAuthentication(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var securePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpContextCurrentUser>());
        services.AddScoped<AuthFlows>();
        services.AddSingleton<PasswordResetThrottle>();

        // Reset tokens come from Identity's data-protection token provider (lifetime set in SecurityPolicy).
        new IdentityBuilder(typeof(IdentityUser), services).AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = SecurityPolicy.PasswordResetTokenLifetime);

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = AuthCookies.SessionName(configuration, isDevelopment);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = securePolicy;
                options.Cookie.Path = "/";
                options.Cookie.IsEssential = true;

                // The window below is only a default: sign-in sets it per session (30 minutes sliding, or 7 days when
                // "remember me" is chosen) and the validator enforces the 8 hour absolute maximum of a session cookie.
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = AuthFlows.SessionWindow;

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = SessionValidator.ValidateAsync,
                    OnRedirectToLogin = context => RespondWithStatus(context, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = context => RespondWithStatus(context, StatusCodes.Status403Forbidden),
                };
            });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AuthCookies.XsrfHeader;
            options.Cookie.Name = AuthCookies.AntiforgeryName(configuration, isDevelopment);
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = securePolicy;
            options.Cookie.Path = "/";
        });

        AddRateLimiting(services, configuration);
        return services;
    }

    /// <summary>An API path gets a status code, never a redirect to a login page that does not exist.</summary>
    private static Task RespondWithStatus(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var permits = int.TryParse(configuration[PermitsPerMinuteKey], NumberStyles.None, CultureInfo.InvariantCulture, out var configured) && configured > 0
            ? configured
            : DefaultPermitsPerMinute;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per client IP. Behind a reverse proxy, enable Hosting:ForwardedHeaders or every caller shares one bucket.
            options.AddPolicy(RateLimitPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Too many attempts. Wait a minute and try again.",
                };
                problem.Extensions[ApiProblems.CodeKey] = ErrorType.Unknown.ToString();
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
            };
        });
    }
}

/// <summary>
/// Runs on every request that carries a session cookie. The session ends when it passed its absolute maximum, when the
/// business account is no longer Active (or its role is inactive), or when the Identity security stamp changed (a
/// password reset). Everything else stays valid until the cookie expires.
/// </summary>
internal static class SessionValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal?.FindFirstValue(HrmsClaims.SecurityStamp);
        var accountIdText = principal?.FindFirstValue(HrmsClaims.AccountId);
        if (subject is null || stamp is null || !int.TryParse(accountIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId))
        {
            await RejectAsync(context);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var time = services.GetRequiredService<TimeProvider>();
        if (context.Properties.Items.TryGetValue(HrmsClaims.AbsoluteExpiryProperty, out var absolute)
            && (!DateTimeOffset.TryParse(absolute, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var limit) || time.GetUtcNow() >= limit))
        {
            await RejectAsync(context);
            return;
        }

        var cancellationToken = context.HttpContext.RequestAborted;
        var account = await services.GetRequiredService<IAccountDirectory>().FindByIdAsync(accountId, includePermissions: false, cancellationToken);
        if (account is not { CanSignIn: true } || account.ExternalSubject != subject)
        {
            await RejectAsync(context);
            return;
        }

        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByIdAsync(subject);
        if (user is null || await users.GetSecurityStampAsync(user) != stamp)
        {
            await RejectAsync(context);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
