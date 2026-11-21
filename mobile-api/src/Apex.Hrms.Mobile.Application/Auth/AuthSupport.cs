using System.Globalization;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Domain.Sessions;

namespace Apex.Hrms.Mobile.Application.Auth;

/// <summary>Who may use the employee app: the web sign-in rules, plus a linked employee who still exists.</summary>
internal static class AccountEligibility
{
    /// <summary>Why this account may not use the app (for the log), or null when it may.</summary>
    public static string? Refusal(AccountSnapshot? account, MobileEmployeeProfile? profile) => account switch
    {
        null => "NoLinkedAccount",
        { Status: AccountStatus.Inactive } => "AccountInactive",
        { Status: AccountStatus.Locked } => "AccountLocked",
        { RoleActive: false } => "RoleInactive",
        { EmployeeLeft: true } => "EmployeeLeft",
        { EmployeeId: null } => "NoLinkedEmployee",
        _ when profile is null => "EmployeeNotFound",
        _ => null,
    };

    public static MobileUserDto ToUser(AccountSnapshot account, MobileEmployeeProfile profile) => new(
        account.Id.ToString(CultureInfo.InvariantCulture),
        profile.EmployeeCode,
        string.IsNullOrWhiteSpace(profile.FullName) ? account.DisplayName : profile.FullName.Trim(),
        account.Email,
        profile.Department,
        profile.Designation,
        profile.PhotoUrl,
        MustChangePassword: false);
}

/// <summary>Loads the account and the employee of an identity user or account, in one place for every use case.</summary>
internal sealed class MobileAccounts(IAccountDirectory accounts, IMobileEmployeeDirectory employees)
{
    public async Task<(AccountSnapshot? Account, MobileEmployeeProfile? Profile)> BySubjectAsync(
        string identityUserId, bool includePermissions, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByExternalSubjectAsync(identityUserId, includePermissions, cancellationToken);
        return (account, await ProfileAsync(account, cancellationToken));
    }

    public async Task<(AccountSnapshot? Account, MobileEmployeeProfile? Profile)> ByIdAsync(
        int accountId, bool includePermissions, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, includePermissions, cancellationToken);
        return (account, await ProfileAsync(account, cancellationToken));
    }

    public Task RecordSignInAsync(int accountId, CancellationToken cancellationToken) => accounts.RecordSignInAsync(accountId, cancellationToken);

    private async Task<MobileEmployeeProfile?> ProfileAsync(AccountSnapshot? account, CancellationToken cancellationToken) =>
        account?.EmployeeId is { } employeeId ? await employees.FindProfileAsync(employeeId, cancellationToken) : null;
}

/// <summary>Builds the token answer of login and refresh.</summary>
internal sealed class TokenResponses(ITokenIssuer tokens)
{
    public MobileTokensDto Create(AccountSnapshot account, MobileEmployeeProfile profile, MobileSession session, string refreshToken, DateTimeOffset now)
    {
        var access = tokens.IssueAccessToken(
            new MobileTokenSubject(session.IdentityUserId, session.DeviceId, account.Id, account.EmployeeId, account.DisplayName),
            now);
        return new MobileTokensDto(
            access.Token,
            refreshToken,
            access.ExpiresAtUtc.UtcDateTime,
            session.ExpiresAtUtc.UtcDateTime,
            MobileAuthMessages.TokenType,
            AccountEligibility.ToUser(account, profile));
    }
}

internal static class Clock
{
    /// <summary>Now in whole seconds: the times go to the device as ISO 8601 and are compared there.</summary>
    public static DateTimeOffset Now(TimeProvider time)
    {
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
    }
}

internal static class AuthErrors
{
    public static AppError SignInFailed { get; } = AppError.Unauthorized(MobileAuthMessages.SignInFailed);

    public static AppError SessionEnded { get; } = AppError.Unauthorized(MobileAuthMessages.SessionEnded);
}
