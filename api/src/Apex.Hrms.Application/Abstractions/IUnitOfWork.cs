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
    /// and the lock's <see cref="ApplicationLock.BusyMessage"/> when the lock cannot be taken in time.
    /// </summary>
    Task<Result<IUnitOfWorkTransaction>> BeginTransactionAsync(ApplicationLock? exclusiveLock, CancellationToken cancellationToken);
}

/// <summary>A transaction of the unit of work. Dispose without committing to roll back.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// An application lock that a transaction of the unit of work holds until it ends: the resource <paramref name="Name"/> and the
/// message of the Conflict (409) answered when somebody else holds it for longer than the wait. The message names what is busy,
/// so each lock answers in its own words ("Another change to KPI plans is in progress. Try again.").
/// </summary>
public sealed record ApplicationLock(string Name, string BusyMessage);
