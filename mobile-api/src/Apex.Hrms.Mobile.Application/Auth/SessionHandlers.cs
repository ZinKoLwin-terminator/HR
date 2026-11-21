using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Mobile.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Mobile.Application.Auth;

/// <summary>Signs the calling device out: its session ends, and with it the access token (checked on every request).</summary>
internal sealed class LogoutHandler(IMobileSessionStore sessions) : IUseCase<LogoutCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(LogoutCommand request, CancellationToken cancellationToken)
    {
        await sessions.RemoveAsync(request.Caller.IdentityUserId, request.Caller.DeviceId, cancellationToken);
        return Result.Success(Done.Value);
    }
}

/// <summary>The signed-in employee with the role and its permissions, read live.</summary>
internal sealed class GetMeHandler(MobileAccounts accounts) : IUseCase<GetMeQuery, MobileMeDto>
{
    public async Task<Result<MobileMeDto>> HandleAsync(GetMeQuery request, CancellationToken cancellationToken)
    {
        var (account, profile) = await accounts.ByIdAsync(request.Caller.AccountId, includePermissions: true, cancellationToken);
        return AccountEligibility.Refusal(account, profile) is not null
            ? Result.Failure<MobileMeDto>(AuthErrors.SessionEnded)
            : Result.Success(new MobileMeDto(AccountEligibility.ToUser(account!, profile!), account!.RoleName, account.Permissions));
    }
}

/// <summary>
/// Changes the password. The new security stamp ends every web session and every other device; this device stays signed
/// in because its session takes the new stamp. Wrong current passwords count towards the lockout (a stolen access token
/// must not be enough to guess the password).
/// </summary>
internal sealed partial class ChangePasswordHandler(
    IIdentityGateway identity,
    IMobileSessionStore sessions,
    ILogger<ChangePasswordHandler> logger) : IUseCase<ChangePasswordCommand, Done>
{
    public async Task<Result<Done>> HandleAsync(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (Validate(request) is { } invalid)
        {
            return Result.Failure<Done>(invalid);
        }

        var caller = request.Caller;
        var result = await identity.ChangePasswordAsync(caller.IdentityUserId, request.CurrentPassword!, request.NewPassword!, cancellationToken);
        switch (result.Outcome)
        {
            case PasswordChangeOutcome.UnknownUser:
                return Result.Failure<Done>(AuthErrors.SessionEnded);
            case PasswordChangeOutcome.LockedOut:
                return Result.Failure<Done>(AppError.Validation(MobileAuthMessages.ChangeLockedOut, "currentPassword", MobileAuthMessages.ChangeLockedOut));
            case PasswordChangeOutcome.WrongCurrentPassword:
                return Result.Failure<Done>(AppError.Validation(MobileAuthMessages.WrongCurrentPassword, "currentPassword", MobileAuthMessages.WrongCurrentPassword));
            case PasswordChangeOutcome.Refused:
                LogRefused(result.Detail ?? "Unknown", caller.IdentityUserId);
                return Result.Failure<Done>(AppError.Validation(MobileAuthMessages.PasswordNotChanged, "newPassword", "Choose a different password."));
        }

        var stamp = await identity.GetSecurityStampAsync(caller.IdentityUserId, cancellationToken);
        if (await sessions.FindAsync(caller.IdentityUserId, caller.DeviceId, cancellationToken) is { } state)
        {
            await sessions.SaveAsync(state.Session.WithSecurityStamp(stamp), cancellationToken);
        }

        await sessions.RemoveAllAsync(caller.IdentityUserId, keepDeviceId: caller.DeviceId, cancellationToken);
        return Result.Success(Done.Value);
    }

    private static AppError? Validate(ChangePasswordCommand request)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(request.CurrentPassword))
        {
            fieldErrors["currentPassword"] = ["Enter your current password."];
        }

        if (SecurityPolicy.PasswordViolation(request.NewPassword) is { } passwordProblem)
        {
            fieldErrors["newPassword"] = [passwordProblem];
        }
        else if (string.Equals(request.NewPassword, request.CurrentPassword, StringComparison.Ordinal))
        {
            fieldErrors["newPassword"] = ["Choose a password that is different from the current one."];
        }

        return fieldErrors.Count > 0 ? AppError.Validation(MobileAuthMessages.PasswordNotChanged, fieldErrors) : null;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mobile password change refused: {Reason}. Identity user: {AuthUserId}.")]
    private partial void LogRefused(string reason, string authUserId);
}

/// <summary>
/// The per-request check of an access token's session: it must exist, not be expired, carry the user's current security
/// stamp, and the account must still be allowed to sign in. So signing out or a password change ends access at once.
/// </summary>
internal sealed class MobileAccessValidator(IMobileSessionStore sessions, IAccountDirectory accounts, TimeProvider time) : IMobileAccessValidator
{
    public async Task<bool> IsValidAsync(MobileCaller caller, CancellationToken cancellationToken)
    {
        var state = await sessions.FindAsync(caller.IdentityUserId, caller.DeviceId, cancellationToken);
        if (state is null || !state.Session.IsUsableAt(time.GetUtcNow(), state.CurrentSecurityStamp))
        {
            return false;
        }

        var account = await accounts.FindByIdAsync(caller.AccountId, includePermissions: false, cancellationToken);
        return account is { CanSignIn: true, EmployeeId: not null } && account.ExternalSubject == caller.IdentityUserId;
    }
}
