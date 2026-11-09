using System.Linq.Expressions;

namespace Apex.Hrms.Application.Abstractions;

/// <param name="Predicate">Rows to include.</param>
/// <param name="Sort">When null, rows come back ordered by id.</param>
/// <param name="PageIndex">Zero-based; the repository clamps it to the last page.</param>
/// <param name="PageSize">Already limited by <see cref="Paging.ClampPageSize"/>.</param>
public sealed record EntityQuery<TEntity>(
    Expression<Func<TEntity, bool>> Predicate,
    SortCriterion<TEntity>? Sort,
    int PageIndex,
    int PageSize);

public sealed record SortCriterion<TEntity>(Expression<Func<TEntity, object>> Key, bool Descending);
