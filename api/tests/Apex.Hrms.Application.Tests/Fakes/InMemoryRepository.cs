using System.Linq.Expressions;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>List-backed repository that assigns identities on Add, like the database would.</summary>
internal sealed class InMemoryRepository<TEntity> : IRepository<TEntity>
    where TEntity : Entity
{
    private static readonly System.Reflection.PropertyInfo IdProperty = typeof(Entity).GetProperty(nameof(Entity.Id))!;

    private readonly List<TEntity> items = [];
    private int lastId;

    public Dictionary<TEntity, byte[]> ExpectedRowVersions { get; } = [];

    public IReadOnlyList<TEntity> Items => items;

    public Task<TEntity?> FindAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(items.FirstOrDefault(item => item.Id == id));

    public Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TEntity>>([.. items.OrderBy(item => item.Id)]);

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

    public void Add(TEntity entity)
    {
        IdProperty.SetValue(entity, ++lastId);
        items.Add(entity);
    }

    public void Remove(TEntity entity) => items.Remove(entity);

    public void ExpectRowVersion(TEntity entity, byte[] rowVersion) => ExpectedRowVersions[entity] = rowVersion;
}
