using System.Globalization;
using System.Security.Claims;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Domain.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Apex.Hrms.Mobile.Infrastructure.Tokens;

/// <summary>
/// The <c>MobileAuth</c> section. <c>SigningKey</c> is required everywhere: at least 32 random bytes in Base64 (HMAC-SHA256),
/// from user-secrets or the environment variable <c>MobileAuth__SigningKey</c>, never from a committed file outside
/// Development. Without it the host does not start. The lifetimes are optional and bounded.
/// </summary>
public sealed class MobileTokenSettings
{
    public const string SectionName = "MobileAuth";
    public const string SigningKeyKey = SectionName + ":SigningKey";
    public const int MinimumKeyBytes = 32;
    public const string DefaultIssuer = "apex-hrms";
    public const string DefaultAudience = "apex-hrms-mobile";

    private MobileTokenSettings(string issuer, string audience, byte[] signingKey, SessionLifetimes lifetimes)
    {
        Issuer = issuer;
        Audience = audience;
        SecurityKey = new SymmetricSecurityKey(signingKey);
        Lifetimes = lifetimes;
    }

    public string Issuer { get; }

    public string Audience { get; }

    public SymmetricSecurityKey SecurityKey { get; }

    public SessionLifetimes Lifetimes { get; }

    /// <exception cref="InvalidOperationException">The signing key is missing, not Base64 or shorter than 32 bytes, or a lifetime is out of range.</exception>
    public static MobileTokenSettings From(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var key = DecodeKey(section["SigningKey"]);

        var accessMinutes = Bounded(section, "AccessTokenMinutes", (int)SessionLifetimes.Default.AccessToken.TotalMinutes, 1, 60);
        var refreshDays = Bounded(section, "RefreshTokenDays", (int)SessionLifetimes.Default.RefreshToken.TotalDays, 1, 90);
        var sessionDays = Bounded(section, "SessionMaxDays", (int)SessionLifetimes.Default.AbsoluteMaximum.TotalDays, 1, 365);
        if (sessionDays < refreshDays)
        {
            throw new InvalidOperationException($"{SectionName}:SessionMaxDays must not be shorter than {SectionName}:RefreshTokenDays.");
        }

        return new MobileTokenSettings(
            section["Issuer"] is { Length: > 0 } issuer ? issuer : DefaultIssuer,
            section["Audience"] is { Length: > 0 } audience ? audience : DefaultAudience,
            key,
            new SessionLifetimes(TimeSpan.FromMinutes(accessMinutes), TimeSpan.FromDays(refreshDays), TimeSpan.FromDays(sessionDays)));
    }

    /// <summary>What the bearer handler of the API host checks on every request (signature, issuer, audience, lifetime).</summary>
    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidIssuer = Issuer,
        ValidAudience = Audience,
        IssuerSigningKey = SecurityKey,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = MobileClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
    };

    private static byte[] DecodeKey(string? value)
    {
        const string Hint = $"{SigningKeyKey} must be at least 32 random bytes in Base64 (for example the output of "
            + "'[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))'), set with user-secrets or the "
            + "environment variable MobileAuth__SigningKey.";
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(Hint);
        }

        try
        {
            var key = Convert.FromBase64String(value.Trim());
            return key.Length >= MinimumKeyBytes ? key : throw new InvalidOperationException(Hint);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(Hint);
        }
    }

    private static int Bounded(IConfiguration section, string name, int fallback, int min, int max)
    {
        var text = section[name];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max
            ? value
            : throw new InvalidOperationException($"{SectionName}:{name} must be a whole number from {min} to {max}.");
    }
}
