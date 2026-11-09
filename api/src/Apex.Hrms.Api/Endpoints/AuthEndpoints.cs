using Apex.Hrms.Api.Auth;
using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// <c>/api/auth</c>. Anonymous: antiforgery, login, forgot-password, reset-password. Signed in: logout, me. The
/// antiforgery token is checked on every unsafe method by <see cref="AntiforgeryMiddleware"/>, not here.
/// </summary>
internal static class AuthEndpoints
{
    private const string Tag = "Auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags(Tag);

        group.MapGet("/antiforgery", (HttpContext http, AuthFlows flows) => flows.IssueAntiforgery(http))
            .AllowAnonymous();

        group.MapPost("/login", (HttpContext http, LoginRequest request, AuthFlows flows, CancellationToken cancellationToken) =>
                flows.LoginAsync(http, request, cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationSetup.RateLimitPolicy);

        group.MapPost("/logout", (HttpContext http, AuthFlows flows) => flows.LogoutAsync(http));

        group.MapGet("/me", (HttpContext http, ICurrentUser user, AuthFlows flows, CancellationToken cancellationToken) =>
            flows.MeAsync(http, user, cancellationToken));

        group.MapPost("/forgot-password", (ForgotPasswordRequest request, AuthFlows flows, CancellationToken cancellationToken) =>
                flows.ForgotPasswordAsync(request, cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationSetup.RateLimitPolicy);

        group.MapPost("/reset-password", (ResetPasswordRequest request, AuthFlows flows, CancellationToken cancellationToken) =>
                flows.ResetPasswordAsync(request, cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting(AuthenticationSetup.RateLimitPolicy);

        return app;
    }
}
