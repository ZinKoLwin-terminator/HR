namespace Apex.Hrms.Mobile.Domain.Sessions;

/// <summary>
/// How long the tokens of a mobile session live: a short access token, a refresh token whose window slides with every
/// rotation, and an absolute end fixed at sign-in that no rotation can pass.
/// </summary>
public sealed record SessionLifetimes
{
    public SessionLifetimes(TimeSpan accessToken, TimeSpan refreshToken, TimeSpan absoluteMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(accessToken, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(refreshToken, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(absoluteMaximum, refreshToken);

        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AbsoluteMaximum = absoluteMaximum;
    }

    public static SessionLifetimes Default { get; } = new(TimeSpan.FromMinutes(15), TimeSpan.FromDays(30), TimeSpan.FromDays(90));

    public TimeSpan AccessToken { get; }

    public TimeSpan RefreshToken { get; }

    public TimeSpan AbsoluteMaximum { get; }
}
