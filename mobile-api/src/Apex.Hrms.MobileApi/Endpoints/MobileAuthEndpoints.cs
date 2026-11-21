using Apex.Hrms.Mobile.Application.Auth;
using Apex.Hrms.MobileApi.Auth;
using Apex.Hrms.MobileApi.Http;

namespace Apex.Hrms.MobileApi.Endpoints;

/// <summary>
/// <c>/api/v1/auth</c> (hr_employee_app/docs/api_contracts.md §2). Each endpoint binds the request, runs its use case and
/// turns the result into the envelope; nothing else. Anonymous: login and refresh (rate limited per IP). With a valid access
/// token: logout, me and change-password.
/// </summary>
internal static class MobileAuthEndpoints
{
    public const string Prefix = "/api/v1/auth";
    private const string Tag = "Auth";

    public static IEndpointRouteBuilder MapMobileAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Prefix).WithTags(Tag);

        group.MapPost("/login", async (LoginRequest request, IUseCase<LoginCommand, MobileTokensDto> login, CancellationToken cancellationToken) =>
                (await login.HandleAsync(new LoginCommand(request.UserName, request.Password, request.DeviceId, request.DeviceName), cancellationToken)).ToEnvelope())
            .AllowAnonymous()
            .RequireRateLimiting(MobileAuthenticationSetup.RateLimitPolicy);

        group.MapPost("/refresh", async (RefreshRequest request, IUseCase<RefreshSessionCommand, MobileTokensDto> refresh, CancellationToken cancellationToken) =>
                (await refresh.HandleAsync(new RefreshSessionCommand(request.RefreshToken, request.DeviceId), cancellationToken)).ToEnvelope())
            .AllowAnonymous()
            .RequireRateLimiting(MobileAuthenticationSetup.RateLimitPolicy);

        // The app sends { refreshToken } as the body; the session is taken from the access token instead, so the body is ignored.
        group.MapPost("/logout", async (HttpContext http, IUseCase<LogoutCommand, Done> logout, CancellationToken cancellationToken) =>
            MobileClaims.Read(http.User) is { } caller
                ? (await logout.HandleAsync(new LogoutCommand(caller), cancellationToken)).ToNoContent()
                : MobileResults.SessionEnded());

        group.MapGet("/me", async (HttpContext http, IUseCase<GetMeQuery, MobileMeDto> me, CancellationToken cancellationToken) =>
            MobileClaims.Read(http.User) is { } caller
                ? (await me.HandleAsync(new GetMeQuery(caller), cancellationToken)).ToEnvelope()
                : MobileResults.SessionEnded());

        group.MapPost("/change-password", async (HttpContext http, ChangePasswordRequest request, IUseCase<ChangePasswordCommand, Done> change, CancellationToken cancellationToken) =>
                MobileClaims.Read(http.User) is { } caller
                    ? (await change.HandleAsync(new ChangePasswordCommand(caller, request.CurrentPassword, request.NewPassword), cancellationToken)).ToNoContent()
                    : MobileResults.SessionEnded())
            .RequireRateLimiting(MobileAuthenticationSetup.RateLimitPolicy);

        return app;
    }

    /// <summary>The login body. <c>userName</c> is the employee code or the work email.</summary>
    internal sealed record LoginRequest(string? UserName, string? Password, string? DeviceId, string? DeviceName);

    internal sealed record RefreshRequest(string? RefreshToken, string? DeviceId);

    internal sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
}
