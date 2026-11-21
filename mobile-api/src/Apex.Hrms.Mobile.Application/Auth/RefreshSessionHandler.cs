using Apex.Hrms.Domain.Common;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Domain.Sessions;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Mobile.Application.Auth;

/// <summary>
/// Exchanges a refresh token for a new pair. The token is rotated every time. The replaced token presented again ends the
/// session (it was copied); any other wrong token is only refused, so guessing a user and device id cannot sign a device
/// out. An expired session, a changed password, or an account that may no longer sign in also ends the session.
/// </summary>
internal sealed partial class RefreshSessionHandler(
    IMobileSessionStore sessions,
    MobileAccounts accounts,
    ITokenIssuer tokens,
    TokenResponses responses,
    SessionLifetimes lifetimes,
    TimeProvider time,
    ILogger<RefreshSessionHandler> logger) : IUseCase<RefreshSessionCommand, MobileTokensDto>
{
    public async Task<Result<MobileTokensDto>> HandleAsync(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        if (!tokens.TryReadRefreshToken(request.RefreshToken, out var userId, out var presentedHash) || Device.IdProblem(request.DeviceId) is not null)
        {
            LogRefused("Malformed", null);
            return Ended();
        }

        var deviceId = request.DeviceId!.Trim();
        var state = await sessions.FindAsync(userId, deviceId, cancellationToken);
        if (state is null)
        {
            LogRefused("NoSession", userId);
            return Ended();
        }

        var current = state.Session;
        switch (current.Check(presentedHash))
        {
            case RefreshTokenCheck.Replayed:
                await sessions.RemoveAsync(userId, deviceId, cancellationToken);
                LogReplayed(userId);
                return Ended();
            case RefreshTokenCheck.Unknown:
                LogRefused("UnknownToken", userId);
                return Ended();
        }

        var now = Clock.Now(time);
        if (!current.IsUsableAt(now, state.CurrentSecurityStamp))
        {
            await sessions.RemoveAsync(userId, deviceId, cancellationToken);
            LogRefused("ExpiredOrPasswordChanged", userId);
            return Ended();
        }

        var (account, profile) = await accounts.BySubjectAsync(userId, includePermissions: false, cancellationToken);
        if (AccountEligibility.Refusal(account, profile) is { } refusal)
        {
            await sessions.RemoveAsync(userId, deviceId, cancellationToken);
            LogRefused(refusal, userId);
            return Ended();
        }

        var refresh = tokens.NewRefreshToken(userId);
        var next = current.Rotate(refresh.Hash, now, lifetimes);
        if (!await sessions.ReplaceAsync(current, next, cancellationToken))
        {
            LogRefused("ConcurrentRotation", userId);
            return Ended();
        }

        return Result.Success(responses.Create(account!, profile!, next, refresh.Token, now));
    }

    private static Result<MobileTokensDto> Ended() => Result.Failure<MobileTokensDto>(AuthErrors.SessionEnded);

    [LoggerMessage(Level = LogLevel.Information, Message = "Mobile token refresh refused: {Reason}. Identity user: {AuthUserId}.")]
    private partial void LogRefused(string reason, string? authUserId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A rotated mobile refresh token was used again; the device session was ended. Identity user: {AuthUserId}.")]
    private partial void LogReplayed(string authUserId);
}
