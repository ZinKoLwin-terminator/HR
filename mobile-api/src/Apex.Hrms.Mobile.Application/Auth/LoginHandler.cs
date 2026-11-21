using Apex.Hrms.Domain.Common;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Domain.Sessions;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Mobile.Application.Auth;

/// <summary>
/// Signs an employee in on a device: the employee code or the work email and the password, with the lockout of the
/// credential store. Starts the device's session (replacing an earlier one) and answers both tokens and the user.
/// Every refusal gets the same generic 401; the real reason is logged with the identity user id, never the name or password.
/// </summary>
internal sealed partial class LoginHandler(
    IIdentityGateway identity,
    IMobileEmployeeDirectory employees,
    MobileAccounts accounts,
    IMobileSessionStore sessions,
    ITokenIssuer tokens,
    TokenResponses responses,
    SessionLifetimes lifetimes,
    TimeProvider time,
    ILogger<LoginHandler> logger) : IUseCase<LoginCommand, MobileTokensDto>
{
    public async Task<Result<MobileTokensDto>> HandleAsync(LoginCommand request, CancellationToken cancellationToken)
    {
        if (Validate(request) is { } invalid)
        {
            return Result.Failure<MobileTokensDto>(invalid);
        }

        var userName = request.UserName!.Trim();
        var userId = userName.Contains('@', StringComparison.Ordinal)
            ? await identity.FindUserIdByEmailAsync(userName, cancellationToken)
            : await employees.FindIdentityUserIdByEmployeeCodeAsync(userName, cancellationToken);
        if (userId is null)
        {
            identity.SpendPasswordCheck(request.Password!);
            LogRefused("UnknownAccount", null);
            return Result.Failure<MobileTokensDto>(AuthErrors.SignInFailed);
        }

        var credentials = await identity.CheckPasswordAsync(userId, request.Password!, cancellationToken);
        if (credentials != CredentialCheck.Valid)
        {
            LogRefused(credentials.ToString(), userId);
            return Result.Failure<MobileTokensDto>(AuthErrors.SignInFailed);
        }

        var (account, profile) = await accounts.BySubjectAsync(userId, includePermissions: false, cancellationToken);
        if (AccountEligibility.Refusal(account, profile) is { } refusal)
        {
            LogRefused(refusal, userId);
            return Result.Failure<MobileTokensDto>(AuthErrors.SignInFailed);
        }

        var now = Clock.Now(time);
        var refresh = tokens.NewRefreshToken(userId);
        var session = MobileSession.Start(
            userId,
            request.DeviceId!,
            request.DeviceName,
            refresh.Hash,
            await identity.GetSecurityStampAsync(userId, cancellationToken),
            now,
            lifetimes);

        await sessions.SaveAsync(session, cancellationToken);
        await accounts.RecordSignInAsync(account!.Id, cancellationToken);
        return Result.Success(responses.Create(account, profile!, session, refresh.Token, now));
    }

    private static AppError? Validate(LoginCommand request)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            fieldErrors["userName"] = ["Enter your employee ID or work email."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            fieldErrors["password"] = ["Enter your password."];
        }

        if (Device.IdProblem(request.DeviceId) is { } deviceProblem)
        {
            fieldErrors["deviceId"] = [deviceProblem];
        }

        return fieldErrors.Count > 0 ? AppError.Validation(MobileAuthMessages.EnterCredentials, fieldErrors) : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mobile sign-in refused: {Reason}. Identity user: {AuthUserId}.")]
    private partial void LogRefused(string reason, string? authUserId);
}
