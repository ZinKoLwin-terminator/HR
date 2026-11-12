using System.Collections.Concurrent;

namespace Apex.Hrms.Application.Security;

/// <summary>Tells the permission check that the grants of a role changed (or the role was deactivated or deleted).</summary>
public interface IRoleGrantInvalidator
{
    /// <summary>Forgets the cached grants of one role; the next request reads sec.RolePermission again.</summary>
    void Invalidate(int roleId);

    /// <summary>Forgets the cached grants of every role.</summary>
    void InvalidateAll();
}

/// <summary>
/// A per-role cache of the "module:Action" grants in sec.RolePermission. It exists only to spare the database one query per
/// request. The account, its status and its role are never cached (they are read live on every request, and that read
/// includes the role's row version), so a deactivated user or a demoted administrator loses access at once.
/// <para>
/// Validity is keyed on the role's <c>RowVersion</c>: saving the grants of a role always updates the role row in the same
/// transaction (<c>EfPermissionStore.ReplaceGrantsAsync</c>), so a changed grant set always arrives with a new row version.
/// An entry is used only while the row version read for this request equals the one it was loaded under. That is exact
/// invalidation on every API instance and has no check-then-store race: the row version is read before the grants, so a load
/// can at worst be stored under an older version, which the next request replaces. <see cref="Lifetime"/> is only a safety
/// net for grants edited directly in the database, which do not move the row version.
/// </para>
/// </summary>
public sealed class RoleGrantCache(TimeProvider time) : IRoleGrantInvalidator
{
    /// <summary>The longest a grant set is served without reading the database again (covers edits made outside the API).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<int, Entry> entries = new();

    /// <param name="roleId">The role.</param>
    /// <param name="roleRowVersion">The row version of the role read in this request.</param>
    /// <param name="load">Reads the grants from the database.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    public async ValueTask<IReadOnlySet<string>> GetAsync(
        int roleId,
        byte[] roleRowVersion,
        Func<CancellationToken, Task<IReadOnlySet<string>>> load,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (entries.TryGetValue(roleId, out var cached)
            && cached.ExpiresAt > now
            && cached.RowVersion.AsSpan().SequenceEqual(roleRowVersion))
        {
            return cached.Grants;
        }

        var grants = await load(cancellationToken);
        entries[roleId] = new Entry([.. roleRowVersion], grants, now + Lifetime);
        return grants;
    }

    public void Invalidate(int roleId) => entries.TryRemove(roleId, out _);

    public void InvalidateAll() => entries.Clear();

    private sealed record Entry(byte[] RowVersion, IReadOnlySet<string> Grants, DateTimeOffset ExpiresAt);
}
