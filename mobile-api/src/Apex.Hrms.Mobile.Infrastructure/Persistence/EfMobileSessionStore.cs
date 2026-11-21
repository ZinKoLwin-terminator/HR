using System.Text.Json;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Mobile.Infrastructure.Persistence;

/// <summary>
/// Keeps the sessions in auth.UserToken, the Identity token table that already exists, so no schema change is needed: one row
/// per identity user and device. <c>Value</c> is <c>{refresh token hash}|{json}</c>; the hash comes first so a rotation can
/// compare it in SQL without parsing the JSON. The refresh token itself is never stored.
/// </summary>
internal sealed class EfMobileSessionStore(MobileDbContext context) : IMobileSessionStore
{
    /// <summary>Distinguishes the mobile sessions from the other Identity tokens of the user (password reset ...).</summary>
    public const string LoginProvider = "hrms-mobile";

    private const char Separator = '|';

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<MobileSessionState?> FindAsync(string identityUserId, string deviceId, CancellationToken cancellationToken)
    {
        var row = await (
            from token in Row(identityUserId, deviceId).AsNoTracking()
            join user in context.IdentityUsers.AsNoTracking() on token.UserId equals user.Id
            select new { token.Value, user.SecurityStamp }).SingleOrDefaultAsync(cancellationToken);

        return row is null || Parse(identityUserId, deviceId, row.Value) is not { } session
            ? null
            : new MobileSessionState(session, row.SecurityStamp);
    }

    public async Task SaveAsync(MobileSession session, CancellationToken cancellationToken)
    {
        var value = Serialize(session);
        if (await UpdateAsync(session.IdentityUserId, session.DeviceId, value, cancellationToken) > 0)
        {
            return;
        }

        var row = new SessionTokenRow { UserId = session.IdentityUserId, LoginProvider = LoginProvider, Name = session.DeviceId, Value = value };
        context.SessionTokens.Add(row);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The same device signed in twice at once and the other request inserted the row first: the later sign-in wins.
            await UpdateAsync(session.IdentityUserId, session.DeviceId, value, cancellationToken);
        }
        finally
        {
            context.Entry(row).State = EntityState.Detached;
        }
    }

    public async Task<bool> ReplaceAsync(MobileSession current, MobileSession replacement, CancellationToken cancellationToken)
    {
        var expectedPrefix = current.RefreshTokenHash + Separator;
        var value = Serialize(replacement);
        var updated = await Row(current.IdentityUserId, current.DeviceId)
            .Where(token => token.Value != null && token.Value.StartsWith(expectedPrefix))
            .ExecuteUpdateAsync(set => set.SetProperty(token => token.Value, value), cancellationToken);
        return updated == 1;
    }

    public Task RemoveAsync(string identityUserId, string deviceId, CancellationToken cancellationToken) =>
        Row(identityUserId, deviceId).ExecuteDeleteAsync(cancellationToken);

    public Task RemoveAllAsync(string identityUserId, string? keepDeviceId, CancellationToken cancellationToken) =>
        context.SessionTokens
            .Where(token => token.UserId == identityUserId && token.LoginProvider == LoginProvider)
            .Where(token => keepDeviceId == null || token.Name != keepDeviceId)
            .ExecuteDeleteAsync(cancellationToken);

    internal static string Serialize(MobileSession session) =>
        session.RefreshTokenHash + Separator + JsonSerializer.Serialize(
            new StoredSession(
                session.DeviceName,
                session.PreviousRefreshTokenHash,
                session.SecurityStamp,
                session.IssuedAtUtc,
                session.ExpiresAtUtc,
                session.AbsoluteExpiresAtUtc),
            Json);

    /// <summary>Null for a value this store cannot read (an older or damaged format): that session is simply not usable.</summary>
    internal static MobileSession? Parse(string identityUserId, string deviceId, string? value)
    {
        var separator = value?.IndexOf(Separator, StringComparison.Ordinal) ?? -1;
        if (value is null || separator <= 0)
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredSession>(value.AsSpan(separator + 1), Json);
            return stored?.SecurityStamp is not { Length: > 0 } stamp
                ? null
                : MobileSession.Restore(
                    identityUserId,
                    deviceId,
                    stored.DeviceName,
                    value[..separator],
                    stored.PreviousRefreshTokenHash,
                    stamp,
                    stored.IssuedAtUtc,
                    stored.ExpiresAtUtc,
                    stored.AbsoluteExpiresAtUtc);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private IQueryable<SessionTokenRow> Row(string identityUserId, string deviceId) =>
        context.SessionTokens.Where(token => token.UserId == identityUserId && token.LoginProvider == LoginProvider && token.Name == deviceId);

    private Task<int> UpdateAsync(string identityUserId, string deviceId, string value, CancellationToken cancellationToken) =>
        Row(identityUserId, deviceId).ExecuteUpdateAsync(set => set.SetProperty(token => token.Value, value), cancellationToken);

    private sealed record StoredSession(
        string? DeviceName,
        string? PreviousRefreshTokenHash,
        string? SecurityStamp,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        DateTimeOffset AbsoluteExpiresAtUtc);
}
