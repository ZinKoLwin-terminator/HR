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
/// A short-lived, per-role cache of the "module:Action" grants in sec.RolePermission. It exists only to spare the database
/// one query per request: the account, its status and its role are never cached (they are read live on every request), so a
/// deactivated user or a demoted administrator loses access at once, and a changed grant is seen at once by this instance
/// because the Settings handlers invalidate the role. Another instance of the API sees it after at most
/// <see cref="Lifetime"/>. A load that started before an invalidation is not stored.
/// </summary>
public sealed class RoleGrantCache(TimeProvider time) : IRoleGrantInvalidator
{
    /// <summary>The longest a grant set is served without reading the database again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<int, Entry> entries = new();
    private long generation;

    public async ValueTask<IReadOnlySet<string>> GetAsync(
        int roleId,
        Func<CancellationToken, Task<IReadOnlySet<string>>> load,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        if (entries.TryGetValue(roleId, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Grants;
        }

        var before = Interlocked.Read(ref generation);
        var grants = await load(cancellationToken);
        if (Interlocked.Read(ref generation) == before)
        {
            entries[roleId] = new Entry(grants, now + Lifetime);
        }

        return grants;
    }

    public void Invalidate(int roleId)
    {
        Interlocked.Increment(ref generation);
        entries.TryRemove(roleId, out _);
    }

    public void InvalidateAll()
    {
        Interlocked.Increment(ref generation);
        entries.Clear();
    }

    private sealed record Entry(IReadOnlySet<string> Grants, DateTimeOffset ExpiresAt);
}
