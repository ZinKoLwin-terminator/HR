using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;

namespace Apex.Hrms.Application.References;

/// <summary>When a reference blocks an operation on the record it points at.</summary>
[Flags]
public enum ReferenceUse
{
    /// <summary>Deleting the record (there is no soft delete).</summary>
    Delete = 1,

    /// <summary>Moving the record under another parent. The database composite foreign keys forbid it while these exist.</summary>
    Reparent = 2,

    Both = Delete | Reparent,
}

/// <summary>
/// One "how many records point at an <typeparamref name="TOwner"/>" question. Each module registers its own checks
/// next to its other services, so adding a resource never touches shared code.
/// </summary>
public interface IReferenceCheck<TOwner>
{
    /// <summary>Wording used in messages, e.g. "department(s)".</summary>
    string Label { get; }

    ReferenceUse Use { get; }

    Task<int> CountAsync(int ownerId, CancellationToken cancellationToken);
}

/// <summary>A reference check that counts rows of <typeparamref name="TChild"/> matching a predicate built from the owner id.</summary>
internal sealed class ChildReferenceCheck<TOwner, TChild>(
    string label,
    ReferenceUse use,
    Func<int, Expression<Func<TChild, bool>>> pointsAt,
    IRecordCounter<TChild> counter) : IReferenceCheck<TOwner>
    where TChild : class
{
    public string Label => label;

    public ReferenceUse Use => use;

    public Task<int> CountAsync(int ownerId, CancellationToken cancellationToken) =>
        counter.CountAsync(pointsAt(ownerId), cancellationToken);
}
