using System.Globalization;
using System.Threading.RateLimiting;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Mobile.Application.Auth;
using Apex.Hrms.Mobile.Infrastructure.Tokens;
using Apex.Hrms.MobileApi.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Apex.Hrms.MobileApi.Auth;

/// <summary>
/// Bearer authentication (signature, issuer, audience and lifetime of the access token, then its session through
/// <see cref="IMobileAccessValidator"/> on every request), deny-by-default authorization, the per-IP rate limit of the auth
/// endpoints and the token based current user. No cookies, so no antiforgery token either.
/// </summary>
internal static class MobileAuthenticationSetup
{
    public const string RateLimitPolicy = "mobile-auth";
    public const int DefaultPermitsPerMinute = 20;
    public const string PermitsPerMinuteKey = MobileTokenSettings.SectionName + ":RateLimit:PermitsPerMinute";

    public static IServiceCollection AddMobileAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, MobileCurrentUser>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<MobileTokenSettings>((options, settings) =>
            {
                // Keep the JWT claim names ("sub", "sid" ...) instead of mapping them to the long WS-* URIs.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = settings.ValidationParameters();
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSessionAsync,
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await MobileResults.WriteErrorAsync(
                            context.HttpContext,
                            AppError.Unauthorized(MobileAuthMessages.SessionEnded),
                            StatusCodes.Status401Unauthorized);
                    },
                    OnForbidden = context => MobileResults.WriteErrorAsync(
                        context.HttpContext,
                        AppError.Forbidden(MobileResults.MessageFor(StatusCodes.Status403Forbidden)),
                        StatusCodes.Status403Forbidden),
                };
            });

        // Deny by default: an endpoint without its own metadata needs a valid access token.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        AddRateLimiting(services, configuration);
        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var validator = context.HttpContext.RequestServices.GetRequiredService<IMobileAccessValidator>();
        if (MobileClaims.Read(context.Principal) is not { } caller
            || !await validator.IsValidAsync(caller, context.HttpContext.RequestAborted))
        {
            context.Fail("The session of the access token has ended.");
        }
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

                await MobileResults.WriteErrorAsync(
                    context.HttpContext,
                    AppError.Validation(MobileResults.MessageFor(StatusCodes.Status429TooManyRequests)),
                    StatusCodes.Status429TooManyRequests);
            };
        });
    }
}
