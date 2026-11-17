using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>The untracked sources and the executors of the read-model queries (see <see cref="IReadModel"/>).</summary>
internal sealed class EfReadModel(HrmsDbContext context) : IReadModel
{
    public IQueryable<TEntity> Query<TEntity>()
        where TEntity : Entity =>
        context.Set<TEntity>().AsNoTracking();

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.ToListAsync(cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.CountAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) =>
        query.FirstOrDefaultAsync(cancellationToken);
}
