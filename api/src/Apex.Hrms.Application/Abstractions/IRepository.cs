using System.Linq.Expressions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Persistence port for one entity type. Predicates are expression trees so the database does the filtering,
/// while the Application layer stays free of any ORM reference.
/// </summary>
public interface IRepository<TEntity>
    where TEntity : Entity
{
    Task<TEntity?> FindAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken);

    Task<PagedResult<TEntity>> QueryAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken);

    void Add(TEntity entity);

    void Remove(TEntity entity);

    /// <summary>Makes the next save fail with a conflict if the stored row version differs from this one.</summary>
    void ExpectRowVersion(TEntity entity, byte[] rowVersion);
}
