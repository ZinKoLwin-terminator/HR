using System.Linq.Expressions;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

internal sealed class EfRepository<TEntity>(HrmsDbContext context) : IRepository<TEntity>
    where TEntity : Entity
{
    private DbSet<TEntity> Set => context.Set<TEntity>();

    public async Task<TEntity?> FindAsync(int id, CancellationToken cancellationToken) =>
        await Set.FindAsync([id], cancellationToken);

    public async Task<TEntity?> FindAsync(
        int id,
        Expression<Func<TEntity, bool>> scope,
        CancellationToken cancellationToken) =>
        await Set.Where(scope).Where(entity => entity.Id == id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>> scope,
        CancellationToken cancellationToken) =>
        await Set.AsNoTracking().Where(scope).OrderBy(entity => entity.Id).ToListAsync(cancellationToken);

    public async Task<PagedResult<TEntity>> QueryAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken)
    {
        var filtered = Set.AsNoTracking().Where(query.Predicate);
        var total = await filtered.CountAsync(cancellationToken);
        var pageIndex = Paging.ClampPageIndex(query.PageIndex, total, query.PageSize);

        var items = await Order(filtered, query.Sort)
            .Skip(pageIndex * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TEntity>(items, total, pageIndex, query.PageSize);
    }

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken) =>
        Set.AnyAsync(predicate, cancellationToken);

    public void Add(TEntity entity) => Set.Add(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public void ExpectRowVersion(TEntity entity, byte[] rowVersion) =>
        context.Entry(entity).Property(nameof(AuditedEntity.RowVersion)).OriginalValue = rowVersion;

    /// <summary>Id is always the final sort key so paging stays stable when the chosen column has ties.</summary>
    private static IOrderedQueryable<TEntity> Order(IQueryable<TEntity> source, SortCriterion<TEntity>? sort)
    {
        if (sort is null)
        {
            return source.OrderBy(entity => entity.Id);
        }

        var ordered = sort.Descending ? source.OrderByDescending(sort.Key) : source.OrderBy(sort.Key);
        return ordered.ThenBy(entity => entity.Id);
    }
}
