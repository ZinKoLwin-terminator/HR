using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists pending changes. Concurrency, unique-key and reference-constraint violations are returned
    /// as <see cref="ErrorType.Conflict"/> failures rather than thrown.
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts a database transaction that spans every save and every identity store write of the request until
    /// <see cref="IUnitOfWorkTransaction.CommitAsync"/>; disposing it without a commit rolls everything back. When
    /// <paramref name="exclusiveLock"/> is given, an application lock of that name is held until the transaction ends, so
    /// two requests that check and then write the same rule (the last administrator) run one after the other. Inside a
    /// transaction that is already open (an Excel import) the call joins it. Fails with <see cref="ErrorType.Conflict"/>
    /// when the lock cannot be taken in time.
    /// </summary>
    Task<Result<IUnitOfWorkTransaction>> BeginTransactionAsync(string? exclusiveLock, CancellationToken cancellationToken);
}

/// <summary>A transaction of the unit of work. Dispose without committing to roll back.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
