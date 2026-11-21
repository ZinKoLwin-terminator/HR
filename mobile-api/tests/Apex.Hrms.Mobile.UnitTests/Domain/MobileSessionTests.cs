using Apex.Hrms.Mobile.Domain.Sessions;

namespace Apex.Hrms.Mobile.UnitTests.Domain;

/// <summary>The session rules: windows, rotation, reuse detection and the security stamp.</summary>
public sealed class MobileSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly SessionLifetimes Lifetimes = new(TimeSpan.FromMinutes(15), TimeSpan.FromDays(30), TimeSpan.FromDays(90));

    private static MobileSession Started() =>
        MobileSession.Start("user-1", "  device-a ", "  Test phone  ", "HASH-1", "stamp-1", Now, Lifetimes);

    [Fact]
    public void A_new_session_opens_a_refresh_window_inside_the_absolute_end_and_normalizes_the_device()
    {
        var session = Started();

        Assert.Equal("device-a", session.DeviceId);
        Assert.Equal("Test phone", session.DeviceName);
        Assert.Equal(Now.AddDays(30), session.ExpiresAtUtc);
        Assert.Equal(Now.AddDays(90), session.AbsoluteExpiresAtUtc);
        Assert.Null(session.PreviousRefreshTokenHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void A_session_needs_a_device_id(string? deviceId) =>
        Assert.Throws<ArgumentException>(() => MobileSession.Start("user-1", deviceId!, null, "HASH", "stamp", Now, Lifetimes));

    [Fact]
    public void A_device_id_longer_than_the_limit_is_refused_and_a_long_name_is_cut()
    {
        Assert.Equal(Device.TooLongIdMessage, Device.IdProblem(new string('x', Device.IdMaxLength + 1)));
        Assert.Null(Device.IdProblem(new string('x', Device.IdMaxLength)));
        Assert.Equal(Device.NameMaxLength, Device.NormalizeName(new string('n', 300))!.Length);
    }

    [Fact]
    public void Rotation_slides_the_window_remembers_the_replaced_token_and_keeps_the_absolute_end()
    {
        var rotated = Started().Rotate("HASH-2", Now.AddDays(10), Lifetimes);

        Assert.Equal("HASH-2", rotated.RefreshTokenHash);
        Assert.Equal("HASH-1", rotated.PreviousRefreshTokenHash);
        Assert.Equal(Now.AddDays(40), rotated.ExpiresAtUtc);
        Assert.Equal(Now.AddDays(90), rotated.AbsoluteExpiresAtUtc);
        Assert.Equal(Now, rotated.IssuedAtUtc);
    }

    [Fact]
    public void Rotation_never_slides_past_the_absolute_end()
    {
        var rotated = Started().Rotate("HASH-2", Now.AddDays(80), Lifetimes);

        Assert.Equal(Now.AddDays(90), rotated.ExpiresAtUtc);
    }

    [Fact]
    public void The_current_token_is_current_the_replaced_one_is_a_replay_and_anything_else_is_unknown()
    {
        var rotated = Started().Rotate("HASH-2", Now, Lifetimes);

        Assert.Equal(RefreshTokenCheck.Current, rotated.Check("HASH-2"));
        Assert.Equal(RefreshTokenCheck.Replayed, rotated.Check("HASH-1"));
        Assert.Equal(RefreshTokenCheck.Unknown, rotated.Check("HASH-X"));
        Assert.Equal(RefreshTokenCheck.Unknown, Started().Check("HASH-0"));
    }

    [Fact]
    public void A_session_is_usable_only_inside_its_window_and_under_the_current_security_stamp()
    {
        var session = Started();

        Assert.True(session.IsUsableAt(Now.AddDays(1), "stamp-1"));
        Assert.False(session.IsUsableAt(Now.AddDays(30), "stamp-1"));
        Assert.False(session.IsUsableAt(Now.AddDays(1), "stamp-2"));
        Assert.False(session.IsUsableAt(Now.AddDays(1), null));
    }

    [Fact]
    public void Taking_a_new_security_stamp_keeps_the_session_usable_under_it()
    {
        var session = Started().WithSecurityStamp("stamp-2");

        Assert.True(session.IsUsableAt(Now.AddDays(1), "stamp-2"));
        Assert.Equal("HASH-1", session.RefreshTokenHash);
    }

    [Fact]
    public void Lifetimes_refuse_an_absolute_end_shorter_than_the_refresh_window() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new SessionLifetimes(TimeSpan.FromMinutes(15), TimeSpan.FromDays(30), TimeSpan.FromDays(7)));
}
