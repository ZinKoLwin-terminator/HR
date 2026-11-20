using System.Linq.Expressions;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// List-backed repository that assigns identities and row versions on Add, like the database would. A record whose
/// row version was checked by a change (ExpectRowVersion) gets a new version when the fake database saves.
/// </summary>
internal sealed class InMemoryRepository<TEntity> : IRepository<TEntity>, IRecordCounter<TEntity>, IVersionedStore, IRollbackStore
    where TEntity : Entity
{
    private static readonly System.Reflection.PropertyInfo IdProperty = typeof(Entity).GetProperty(nameof(Entity.Id))!;
    private static readonly System.Reflection.PropertyInfo RowVersionProperty = typeof(AuditedEntity).GetProperty(nameof(AuditedEntity.RowVersion))!;
    private static readonly System.Reflection.PropertyInfo ConfidentialRowVersionProperty = typeof(ConfidentialEntity).GetProperty(nameof(ConfidentialEntity.RowVersion))!;
    private static readonly System.Reflection.PropertyInfo ConfidentialModifiedProperty = typeof(ConfidentialEntity).GetProperty(nameof(ConfidentialEntity.ModifiedAtUtc))!;
    private static readonly System.Reflection.PropertyInfo ConfidentialCreatedProperty = typeof(ConfidentialEntity).GetProperty(nameof(ConfidentialEntity.CreatedAtUtc))!;
    private static long confidentialTicks = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    private readonly FakeDatabase database;
    private readonly List<TEntity> items = [];
    private readonly HashSet<TEntity> changed = [];
    private int lastId;

    public InMemoryRepository(FakeDatabase database)
    {
        this.database = database;
        database.Register(this);
    }

    public Dictionary<TEntity, byte[]> ExpectedRowVersions { get; } = [];

    public IReadOnlyList<TEntity> Items => items;

    public Task<TEntity?> FindAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(items.FirstOrDefault(item => item.Id == id));

    public Task<TEntity?> FindAsync(int id, Expression<Func<TEntity, bool>> scope, CancellationToken cancellationToken) =>
        Task.FromResult(items.Where(scope.Compile()).FirstOrDefault(item => item.Id == id));

    public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TEntity>>([.. items.Where(scope.Compile()).OrderBy(item => item.Id)]);

    public Task<IReadOnlyList<TEntity>> ListForChangeAsync(Expression<Func<TEntity, bool>> scope, CancellationToken cancellationToken) =>
        ListAsync(scope, cancellationToken);

    public Task<PagedResult<TEntity>> QueryAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        var filtered = items.Where(query.Predicate.Compile()).ToList();
        IEnumerable<TEntity> ordered = filtered.OrderBy(item => item.Id);
        if (query.Sort is { } sort)
        {
            var key = sort.Key.Compile();
            ordered = (sort.Descending ? filtered.OrderByDescending(key) : filtered.OrderBy(key)).ThenBy(item => item.Id);
        }

        var pageIndex = Paging.ClampPageIndex(query.PageIndex, filtered.Count, query.PageSize);
        var page = ordered.Skip(pageIndex * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult(new PagedResult<TEntity>(page, filtered.Count, pageIndex, query.PageSize));
    }

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken) =>
        Task.FromResult(items.Any(predicate.Compile()));

    public Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken) =>
        Task.FromResult(items.Count(predicate.Compile()));

    public void Add(TEntity entity)
    {
        IdProperty.SetValue(entity, ++lastId);
        if (entity is AuditedEntity)
        {
            RowVersionProperty.SetValue(entity, database.NextRowVersion());
        }
        else if (entity is ConfidentialEntity)
        {
            // Like the database and the audit interceptor: a row version, and increasing creation and modification times.
            ConfidentialRowVersionProperty.SetValue(entity, database.NextRowVersion());
            var now = NextConfidentialTime();
            ConfidentialCreatedProperty.SetValue(entity, now);
            ConfidentialModifiedProperty.SetValue(entity, now);
        }

        items.Add(entity);
    }

    public void Remove(TEntity entity) => items.Remove(entity);

    public void ExpectRowVersion(TEntity entity, byte[] rowVersion)
    {
        ExpectedRowVersions[entity] = rowVersion;
        changed.Add(entity);
    }

    public void MarkModified(TEntity entity) => changed.Add(entity);

    private static DateTime NextConfidentialTime() => new(Interlocked.Add(ref confidentialTicks, TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    void IVersionedStore.StampChanged()
    {
        foreach (var entity in changed)
        {
            if (entity is ConfidentialEntity)
            {
                ConfidentialRowVersionProperty.SetValue(entity, database.NextRowVersion());
                ConfidentialModifiedProperty.SetValue(entity, NextConfidentialTime());
            }
            else
            {
                RowVersionProperty.SetValue(entity, database.NextRowVersion());
            }
        }

        changed.Clear();
    }

    Action IRollbackStore.Snapshot()
    {
        var kept = items.ToList();
        var values = kept.Select(item => (Item: item, Fields: EntityFields.Copy(item))).ToList();
        var keptChanged = changed.ToList();
        var keptExpected = ExpectedRowVersions.ToDictionary();
        var keptLastId = lastId;
        return () =>
        {
            items.Clear();
            items.AddRange(kept);
            foreach (var (item, fields) in values)
            {
                EntityFields.Restore(item, fields);
            }

            changed.Clear();
            changed.UnionWith(keptChanged);
            ExpectedRowVersions.Clear();
            foreach (var (entity, rowVersion) in keptExpected)
            {
                ExpectedRowVersions[entity] = rowVersion;
            }

            lastId = keptLastId;
        };
    }
}

internal interface IVersionedStore
{
    void StampChanged();
}

/// <summary>A store that can put its records back as they were (the fake of a transaction rollback, see <see cref="FakeUnitOfWork.RollBackUncommittedTransactions"/>).</summary>
internal interface IRollbackStore
{
    /// <summary>Takes a copy of the records now and returns the action that restores it.</summary>
    Action Snapshot();
}

/// <summary>
/// Copies and restores the fields of an entity, its base classes included. The copy is shallow: scalar state (ids, foreign
/// keys, names, statuses, row versions) comes back exactly; a child collection comes back as the same list instance.
/// </summary>
internal static class EntityFields
{
    private const System.Reflection.BindingFlags Declared =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;

    public static List<(System.Reflection.FieldInfo Field, object? Value)> Copy(object entity) =>
        [.. FieldsOf(entity.GetType()).Select(field => (field, field.GetValue(entity)))];

    public static void Restore(object entity, List<(System.Reflection.FieldInfo Field, object? Value)> fields)
    {
        foreach (var (field, value) in fields)
        {
            field.SetValue(entity, value);
        }
    }

    private static IEnumerable<System.Reflection.FieldInfo> FieldsOf(Type? type)
    {
        for (; type is not null && type != typeof(object); type = type.BaseType)
        {
            foreach (var field in type.GetFields(Declared))
            {
                yield return field;
            }
        }
    }
}

/// <summary>Shared by the fake repositories and the fake unit of work: hands out row versions and stamps saved changes.</summary>
internal sealed class FakeDatabase
{
    private readonly List<IVersionedStore> stores = [];
    private long version;

    public void Register(IVersionedStore store) => stores.Add(store);

    public byte[] NextRowVersion() => BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(++version));

    public void Commit()
    {
        foreach (var store in stores)
        {
            store.StampChanged();
        }
    }

    /// <summary>A copy of every repository now; the returned action puts them all back (a rollback).</summary>
    public Action Snapshot()
    {
        var restores = stores.OfType<IRollbackStore>().Select(store => store.Snapshot()).ToList();
        return () => restores.ForEach(restore => restore());
    }
}
