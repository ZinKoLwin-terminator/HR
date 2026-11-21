using System.Security.Cryptography;
using System.Text;

namespace Apex.Hrms.Mobile.Domain.Sessions;

/// <summary>What a presented refresh token is to the session.</summary>
public enum RefreshTokenCheck
{
    /// <summary>The session's current token: it may be rotated.</summary>
    Current,

    /// <summary>The token the current one replaced, presented again: it was copied, so the session can no longer be trusted.</summary>
    Replayed,

    /// <summary>Any other token: refused, but the session is left alone (a guess must not sign a device out).</summary>
    Unknown,
}

/// <summary>
/// A signed-in device of one identity user. The session holds only SHA-256 hashes of refresh tokens, never a token.
/// Every refresh rotates the token: the window slides forward but never past <see cref="AbsoluteExpiresAtUtc"/>, and the
/// replaced token is remembered so that presenting it again (reuse) can be detected. The session is bound to the security
/// stamp the user had when it was issued: a password change or reset changes the stamp and so ends the session.
/// </summary>
public sealed class MobileSession
{
    private MobileSession(
        string identityUserId,
        string deviceId,
        string? deviceName,
        string refreshTokenHash,
        string? previousRefreshTokenHash,
        string securityStamp,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset absoluteExpiresAtUtc)
    {
        IdentityUserId = identityUserId;
        DeviceId = deviceId;
        DeviceName = deviceName;
        RefreshTokenHash = refreshTokenHash;
        PreviousRefreshTokenHash = previousRefreshTokenHash;
        SecurityStamp = securityStamp;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        AbsoluteExpiresAtUtc = absoluteExpiresAtUtc;
    }

    public string IdentityUserId { get; }

    public string DeviceId { get; }

    public string? DeviceName { get; }

    public string RefreshTokenHash { get; }

    public string? PreviousRefreshTokenHash { get; }

    public string SecurityStamp { get; }

    public DateTimeOffset IssuedAtUtc { get; }

    /// <summary>The end of the current refresh window.</summary>
    public DateTimeOffset ExpiresAtUtc { get; }

    /// <summary>The hard end fixed at sign-in.</summary>
    public DateTimeOffset AbsoluteExpiresAtUtc { get; }

    /// <summary>A new session at sign-in.</summary>
    public static MobileSession Start(
        string identityUserId,
        string deviceId,
        string? deviceName,
        string refreshTokenHash,
        string securityStamp,
        DateTimeOffset now,
        SessionLifetimes lifetimes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshTokenHash);
        ArgumentException.ThrowIfNullOrEmpty(securityStamp);
        ArgumentNullException.ThrowIfNull(lifetimes);
        if (Device.IdProblem(deviceId) is { } problem)
        {
            throw new ArgumentException(problem, nameof(deviceId));
        }

        var absoluteEnd = now + lifetimes.AbsoluteMaximum;
        return new MobileSession(
            identityUserId,
            deviceId.Trim(),
            Device.NormalizeName(deviceName),
            refreshTokenHash,
            previousRefreshTokenHash: null,
            securityStamp,
            now,
            Earliest(now + lifetimes.RefreshToken, absoluteEnd),
            absoluteEnd);
    }

    /// <summary>A session as it was stored. No rule is applied: the store returns what it saved.</summary>
    public static MobileSession Restore(
        string identityUserId,
        string deviceId,
        string? deviceName,
        string refreshTokenHash,
        string? previousRefreshTokenHash,
        string securityStamp,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        DateTimeOffset absoluteExpiresAtUtc) =>
        new(identityUserId, deviceId, deviceName, refreshTokenHash, previousRefreshTokenHash, securityStamp, issuedAtUtc, expiresAtUtc, absoluteExpiresAtUtc);

    /// <summary>Compares the presented hash with the current and the replaced one, in constant time.</summary>
    public RefreshTokenCheck Check(string presentedHash)
    {
        if (SameHash(RefreshTokenHash, presentedHash))
        {
            return RefreshTokenCheck.Current;
        }

        return PreviousRefreshTokenHash is { } previous && SameHash(previous, presentedHash)
            ? RefreshTokenCheck.Replayed
            : RefreshTokenCheck.Unknown;
    }

    /// <summary>The refresh window and the absolute end have not passed.</summary>
    public bool IsExpiredAt(DateTimeOffset now) => now >= ExpiresAtUtc || now >= AbsoluteExpiresAtUtc;

    /// <summary>Usable: not expired, and issued under the security stamp the user still has.</summary>
    public bool IsUsableAt(DateTimeOffset now, string? currentSecurityStamp) =>
        !IsExpiredAt(now)
        && currentSecurityStamp is not null
        && string.Equals(SecurityStamp, currentSecurityStamp, StringComparison.Ordinal);

    /// <summary>The session with a new refresh token; the current one becomes the replaced token.</summary>
    public MobileSession Rotate(string newRefreshTokenHash, DateTimeOffset now, SessionLifetimes lifetimes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newRefreshTokenHash);
        ArgumentNullException.ThrowIfNull(lifetimes);
        return new MobileSession(
            IdentityUserId,
            DeviceId,
            DeviceName,
            newRefreshTokenHash,
            RefreshTokenHash,
            SecurityStamp,
            IssuedAtUtc,
            Earliest(now + lifetimes.RefreshToken, AbsoluteExpiresAtUtc),
            AbsoluteExpiresAtUtc);
    }

    /// <summary>The session under a new security stamp: the device that changed the password stays signed in.</summary>
    public MobileSession WithSecurityStamp(string securityStamp)
    {
        ArgumentException.ThrowIfNullOrEmpty(securityStamp);
        return new MobileSession(
            IdentityUserId,
            DeviceId,
            DeviceName,
            RefreshTokenHash,
            PreviousRefreshTokenHash,
            securityStamp,
            IssuedAtUtc,
            ExpiresAtUtc,
            AbsoluteExpiresAtUtc);
    }

    private static DateTimeOffset Earliest(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;

    private static bool SameHash(string stored, string presented) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(presented));
}
