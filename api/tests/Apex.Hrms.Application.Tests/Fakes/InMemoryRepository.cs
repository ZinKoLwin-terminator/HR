using System.Linq.Expressions;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// List-backed repository that assigns identities and row versions on Add, like the database would. A record whose
/// row version was checked by a change (ExpectRowVersion) gets a new version when the fake database saves.
/// </summary>
internal sealed class InMemoryRepository<TEntity> : IRepository<TEntity>, IRecordCounter<TEntity>, IVersionedStore
    where TEntity : Entity
{
    private static readonly System.Reflection.PropertyInfo IdProperty = typeof(Entity).GetProperty(nameof(Entity.Id))!;
    private static readonly System.Reflection.PropertyInfo RowVersionProperty = typeof(AuditedEntity).GetProperty(nameof(AuditedEntity.RowVersion))!;

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

        items.Add(entity);
    }

    public void Remove(TEntity entity) => items.Remove(entity);

    public void ExpectRowVersion(TEntity entity, byte[] rowVersion)
    {
        ExpectedRowVersions[entity] = rowVersion;
        changed.Add(entity);
    }

    void IVersionedStore.StampChanged()
    {
        foreach (var entity in changed)
        {
            RowVersionProperty.SetValue(entity, database.NextRowVersion());
        }

        changed.Clear();
    }
}

internal interface IVersionedStore
{
    void StampChanged();
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
}
