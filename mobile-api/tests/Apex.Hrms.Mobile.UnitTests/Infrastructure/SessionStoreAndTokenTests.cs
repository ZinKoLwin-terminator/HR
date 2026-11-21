using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Domain.Sessions;
using Apex.Hrms.Mobile.Infrastructure.Persistence;
using Apex.Hrms.Mobile.Infrastructure.Tokens;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Apex.Hrms.Mobile.UnitTests.Infrastructure;

/// <summary>The stored session format and the token issuer. No database, no host.</summary>
public sealed class SessionStoreAndTokenTests
{
    private static readonly MobileSession Session = MobileSession.Restore(
        "user-1",
        "device-a",
        "Test phone",
        "AB12",
        "CD34",
        "stamp",
        new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 1, 30, 8, 0, 0, TimeSpan.Zero));

    private static JwtTokenIssuer Issuer() => new(MobileTokenSettings.From(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [MobileTokenSettings.SigningKeyKey] = Convert.ToBase64String(new byte[48]) })
        .Build()));

    [Fact]
    public void A_stored_session_survives_the_round_trip_and_its_value_starts_with_the_refresh_token_hash()
    {
        var value = EfMobileSessionStore.Serialize(Session);
        var parsed = EfMobileSessionStore.Parse("user-1", "device-a", value)!;

        Assert.StartsWith("AB12|{", value, StringComparison.Ordinal);
        Assert.Equal(
            (Session.DeviceName, Session.RefreshTokenHash, Session.PreviousRefreshTokenHash, Session.SecurityStamp, Session.IssuedAtUtc, Session.ExpiresAtUtc, Session.AbsoluteExpiresAtUtc),
            (parsed.DeviceName, parsed.RefreshTokenHash, parsed.PreviousRefreshTokenHash, parsed.SecurityStamp, parsed.IssuedAtUtc, parsed.ExpiresAtUtc, parsed.AbsoluteExpiresAtUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("|{}")]
    [InlineData("AB12|not json")]
    [InlineData("AB12|{}")]
    public void A_value_the_store_cannot_read_is_no_session(string? value) =>
        Assert.Null(EfMobileSessionStore.Parse("user-1", "device-a", value));

    [Fact]
    public void A_refresh_token_reads_back_to_its_user_and_the_hash_that_was_stored()
    {
        var issuer = Issuer();
        var token = issuer.NewRefreshToken("6f1d-user");

        Assert.True(issuer.TryReadRefreshToken(token.Token, out var userId, out var hash));
        Assert.Equal("6f1d-user", userId);
        Assert.Equal(token.Hash, hash);
        Assert.DoesNotContain(token.Hash, token.Token, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-dot")]
    [InlineData("a.b.c")]
    [InlineData("!!!.secret")]
    public void A_malformed_refresh_token_does_not_read(string? token) =>
        Assert.False(Issuer().TryReadRefreshToken(token, out _, out _));

    [Fact]
    public void An_access_token_is_hs256_names_the_session_and_expires_after_the_configured_lifetime()
    {
        var now = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

        var issued = Issuer().IssueAccessToken(new MobileTokenSubject("user-1", "device-a", 7, 42, "TST- Aye"), now);
        var token = new JsonWebToken(issued.Token);

        Assert.Equal("HS256", token.Alg);
        Assert.Equal("user-1", token.Subject);
        Assert.Equal("device-a", token.GetClaim(MobileClaimTypes.SessionId).Value);
        Assert.Equal("7", token.GetClaim(MobileClaimTypes.AccountId).Value);
        Assert.Equal(now.AddMinutes(15), issued.ExpiresAtUtc);
        Assert.DoesNotContain(token.Claims, claim => claim.Type is "email" or "permissions" or "role");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !")]
    [InlineData("c2hvcnQga2V5")]
    public void Settings_refuse_a_missing_or_short_signing_key(string key)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [MobileTokenSettings.SigningKeyKey] = key })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() => MobileTokenSettings.From(configuration));

        Assert.Contains(MobileTokenSettings.SigningKeyKey, error.Message, StringComparison.Ordinal);
    }
}
