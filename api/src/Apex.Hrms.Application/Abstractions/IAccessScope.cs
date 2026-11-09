using System.Linq.Expressions;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Restricts which records of a type the caller may see or change (for example "only my company" or "only my
/// department"). The predicate is applied inside the database query by the list, get, update, delete and command
/// use cases, so a record outside the scope behaves exactly like a missing one (NotFound).
/// Register a type-specific implementation to narrow a resource; the default allows everything.
/// </summary>
public interface IAccessScope<TEntity>
{
    Expression<Func<TEntity, bool>> PredicateFor(ICurrentUser user);
}

/// <summary>Default scope: no restriction. TODO(Identity): replace per resource once roles are scoped to data.</summary>
internal sealed class AllowAllAccessScope<TEntity> : IAccessScope<TEntity>
{
    public Expression<Func<TEntity, bool>> PredicateFor(ICurrentUser user) => Predicates.Always<TEntity>();
}
