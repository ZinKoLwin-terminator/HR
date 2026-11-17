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
    /// <summary>Finds a record by id without any access restriction. For rule checks on parents, not for responses.</summary>
    Task<TEntity?> FindAsync(int id, CancellationToken cancellationToken);

    /// <summary>Finds a record by id when it also satisfies <paramref name="scope"/> (see <see cref="IAccessScope{TEntity}"/>).</summary>
    Task<TEntity?> FindAsync(int id, Expression<Func<TEntity, bool>> scope, CancellationToken cancellationToken);

    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> scope, CancellationToken cancellationToken);

    /// <summary>Like <see cref="ListAsync"/> but the records are tracked, so changing them and saving writes the changes (a use case that edits many records of one parent).</summary>
    Task<IReadOnlyList<TEntity>> ListForChangeAsync(Expression<Func<TEntity, bool>> scope, CancellationToken cancellationToken);

    Task<PagedResult<TEntity>> QueryAsync(EntityQuery<TEntity> query, CancellationToken cancellationToken);

    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken);

    void Add(TEntity entity);

    void Remove(TEntity entity);

    /// <summary>Makes the next save fail with a conflict if the stored row version differs from this one.</summary>
    void ExpectRowVersion(TEntity entity, byte[] rowVersion);

    /// <summary>
    /// Makes the next save write this audited record even when none of its own fields changed, so its row version moves. A parent
    /// whose children are changed in the same save uses it to keep one concurrency token for the whole aggregate.
    /// </summary>
    void MarkModified(TEntity entity);
}
