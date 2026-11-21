using Apex.Hrms.Mobile.Domain.Sessions;

namespace Apex.Hrms.Mobile.Application.Abstractions;

/// <summary>A stored session together with the identity user's current security stamp (null when the user has none).</summary>
public sealed record MobileSessionState(MobileSession Session, string? CurrentSecurityStamp);

/// <summary>Stores the mobile sessions, one per identity user and device.</summary>
public interface IMobileSessionStore
{
    Task<MobileSessionState?> FindAsync(string identityUserId, string deviceId, CancellationToken cancellationToken);

    /// <summary>Creates the session of the device, replacing any earlier one of the same user and device.</summary>
    Task SaveAsync(MobileSession session, CancellationToken cancellationToken);

    /// <summary>Replaces <paramref name="current"/> only while it is still the stored session; false when another request got there first.</summary>
    Task<bool> ReplaceAsync(MobileSession current, MobileSession replacement, CancellationToken cancellationToken);

    Task RemoveAsync(string identityUserId, string deviceId, CancellationToken cancellationToken);

    /// <summary>Ends every session of the user except the one of <paramref name="keepDeviceId"/> (all of them when null).</summary>
    Task RemoveAllAsync(string identityUserId, string? keepDeviceId, CancellationToken cancellationToken);
}

/// <summary>The work identity the app shows for an employee. No personal data.</summary>
public sealed record MobileEmployeeProfile(
    string EmployeeCode,
    string FullName,
    string? Department,
    string? Designation,
    string? PhotoUrl);

/// <summary>Reads what sign-in needs about employees.</summary>
public interface IMobileEmployeeDirectory
{
    /// <summary>The identity user id of the account linked to the employee with this code, or null.</summary>
    Task<string?> FindIdentityUserIdByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken);

    Task<MobileEmployeeProfile?> FindProfileAsync(int employeeId, CancellationToken cancellationToken);
}

public enum CredentialCheck
{
    Valid,
    WrongPassword,
    LockedOut,
}

public enum PasswordChangeOutcome
{
    Changed,
    UnknownUser,
    WrongCurrentPassword,
    LockedOut,
    Refused,
}

/// <summary>The outcome of a password change; <see cref="Detail"/> is a reason for the log (error codes), never shown.</summary>
public sealed record PasswordChangeResult(PasswordChangeOutcome Outcome, string? Detail = null);

/// <summary>
/// The credential store (ASP.NET Core Identity): passwords, lockout and security stamps. Wrong passwords count towards the
/// lockout here, so every caller gets the same protection.
/// </summary>
public interface IIdentityGateway
{
    Task<string?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>Checks the password of the user and counts a failure towards the lockout.</summary>
    Task<CredentialCheck> CheckPasswordAsync(string identityUserId, string password, CancellationToken cancellationToken);

    /// <summary>Verifies a password against a throwaway hash, so an unknown user costs the same time as a wrong password.</summary>
    void SpendPasswordCheck(string password);

    /// <summary>The user's security stamp; one is created when the user has none.</summary>
    Task<string> GetSecurityStampAsync(string identityUserId, CancellationToken cancellationToken);

    Task<PasswordChangeResult> ChangePasswordAsync(string identityUserId, string currentPassword, string newPassword, CancellationToken cancellationToken);
}

/// <summary>Who an access token is issued to.</summary>
public sealed record MobileTokenSubject(string IdentityUserId, string DeviceId, int AccountId, int? EmployeeId, string DisplayName);

/// <summary>A signed access token and its expiry (UTC).</summary>
public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAtUtc);

/// <summary>A new refresh token: <see cref="Token"/> goes to the device once, only <see cref="Hash"/> is stored.</summary>
public sealed record NewRefreshToken(string Token, string Hash);

/// <summary>Issues and reads the tokens of the mobile app.</summary>
public interface ITokenIssuer
{
    IssuedAccessToken IssueAccessToken(MobileTokenSubject subject, DateTimeOffset now);

    NewRefreshToken NewRefreshToken(string identityUserId);

    /// <summary>The identity user and the hash of the secret of a refresh token; false for anything malformed.</summary>
    bool TryReadRefreshToken(string? refreshToken, out string identityUserId, out string hash);
}

/// <summary>The claim names of an access token, shared by the token issuer and the API host that reads them.</summary>
public static class MobileClaimTypes
{
    /// <summary>The identity user id (auth.User.Id).</summary>
    public const string Subject = "sub";

    /// <summary>The device id: with <see cref="Subject"/> it names the session.</summary>
    public const string SessionId = "sid";

    public const string TokenId = "jti";

    public const string Name = "name";

    /// <summary>The sec.AppUser id (the audit user id); the same claim name as the web API's session cookie.</summary>
    public const string AccountId = "hrms:uid";

    /// <summary>The linked employee id.</summary>
    public const string EmployeeId = "hrms:eid";
}
