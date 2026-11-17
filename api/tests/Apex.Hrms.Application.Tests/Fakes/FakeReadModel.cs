using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>The read-model sources over the in-memory repositories: the Application layer's own LINQ runs against the lists.</summary>
internal sealed class FakeReadModel(IServiceProvider provider) : IReadModel
{
    public IQueryable<TEntity> Query<TEntity>()
        where TEntity : Entity =>
        ((InMemoryRepository<TEntity>)provider.GetRequiredService<IRepository<TEntity>>()).Items.ToList().AsQueryable();

    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.ToList());

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.Count());

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken) => Task.FromResult(query.FirstOrDefault());
}
