using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Read-only, untracked queries that join several record types and project them into read models (a list row with the
/// employee code and the period name, say), so the database returns only the columns shown and nobody loads a record per row.
/// The queries are written as <see cref="IQueryable{T}"/> in the Application layer; this port only supplies the sources and
/// runs them, because the Application layer has no reference to the ORM. Apply the access scope of the caller to the source of
/// the records a user sees (see <see cref="IAccessScope{TEntity}"/>) before anything is joined to it.
/// </summary>
public interface IReadModel
{
    /// <summary>All rows of a record type, not tracked.</summary>
    IQueryable<TEntity> Query<TEntity>()
        where TEntity : Entity;

    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);

    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken);
}
