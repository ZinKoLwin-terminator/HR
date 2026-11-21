using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Apex.Hrms.Mobile.Application.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Apex.Hrms.Mobile.Infrastructure.Tokens;

/// <summary>
/// Access tokens are JWTs signed with HMAC-SHA256 that name the session (user + device) and the account, but carry no
/// permissions or email. A refresh token is <c>{identity user id, Base64Url}.{32 random bytes, Base64Url}</c>: the first part
/// finds the session, the second is the secret whose SHA-256 hash (hex) the session holds.
/// </summary>
internal sealed class JwtTokenIssuer(MobileTokenSettings settings) : ITokenIssuer
{
    private const int SecretBytes = 32;
    private static readonly JsonWebTokenHandler Handler = new();

    public IssuedAccessToken IssueAccessToken(MobileTokenSubject subject, DateTimeOffset now)
    {
        var expires = now + settings.Lifetimes.AccessToken;
        var claims = new Dictionary<string, object>
        {
            [MobileClaimTypes.Subject] = subject.IdentityUserId,
            [MobileClaimTypes.SessionId] = subject.DeviceId,
            [MobileClaimTypes.TokenId] = Guid.NewGuid().ToString("N"),
            [MobileClaimTypes.Name] = subject.DisplayName,
            [MobileClaimTypes.AccountId] = subject.AccountId.ToString(CultureInfo.InvariantCulture),
        };
        if (subject.EmployeeId is { } employeeId)
        {
            claims[MobileClaimTypes.EmployeeId] = employeeId.ToString(CultureInfo.InvariantCulture);
        }

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(settings.SecurityKey, SecurityAlgorithms.HmacSha256),
        });
        return new IssuedAccessToken(token, expires);
    }

    public NewRefreshToken NewRefreshToken(string identityUserId)
    {
        var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(SecretBytes));
        return new NewRefreshToken($"{Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(identityUserId))}.{secret}", Hash(secret));
    }

    public bool TryReadRefreshToken(string? refreshToken, out string identityUserId, out string hash)
    {
        identityUserId = string.Empty;
        hash = string.Empty;
        var parts = refreshToken?.Trim().Split('.');
        if (parts is not [{ Length: > 0 and <= 600 } user, { Length: > 0 and <= 64 } secret])
        {
            return false;
        }

        try
        {
            identityUserId = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(user));
        }
        catch (FormatException)
        {
            return false;
        }

        hash = Hash(secret);
        return identityUserId.Length > 0;
    }

    internal static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
}
