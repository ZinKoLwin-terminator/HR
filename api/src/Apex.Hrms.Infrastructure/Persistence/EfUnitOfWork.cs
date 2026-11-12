using System.Data;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apex.Hrms.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(HrmsDbContext context, EfActivityRecorder activity) : IUnitOfWork
{
    /// <summary>How long a request waits for the application lock before it gives up with a Conflict (milliseconds).</summary>
    private const int LockTimeoutMilliseconds = 15_000;

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!activity.HasDeferred)
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }

            // An activity message that needs a database-assigned id: insert the change, work the text out, insert the
            // entry, and commit both together (the transaction rolls back when anything fails). When the caller already
            // opened a transaction, both saves simply join it.
            var own = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            await using (own)
            {
                await context.SaveChangesAsync(cancellationToken);
                activity.WriteDeferred();
                await context.SaveChangesAsync(cancellationToken);
                if (own is not null)
                {
                    await own.CommitAsync(cancellationToken);
                }
            }

            return Result.Success();
        }
        catch (UnauthenticatedWriteException)
        {
            return Result.Failure(AppError.Unauthorized(UnauthenticatedWriteException.PublicMessage));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(RowVersionToken.Stale);
        }
        catch (DbUpdateException exception) when (SqlConflicts.TryTranslate(exception, out var conflict))
        {
            return Result.Failure(conflict);
        }
    }

    public async Task<Result<IUnitOfWorkTransaction>> BeginTransactionAsync(string? exclusiveLock, CancellationToken cancellationToken)
    {
        var outer = context.Database.CurrentTransaction;
        var transaction = outer is null ? await context.Database.BeginTransactionAsync(cancellationToken) : null;

        // Joined mode: the outer transaction belongs to the caller (the Excel import runs every row inside one), so this scope
        // cannot commit it, but it must still be able to undo its own changes: a savepoint taken now is rolled back to when
        // the scope is disposed without a commit.
        var savepoint = outer is null ? null : "uow_" + Guid.NewGuid().ToString("N")[..28]; // a savepoint name is at most 32 characters
        try
        {
            if (outer is not null)
            {
                await outer.CreateSavepointAsync(savepoint!, cancellationToken);
            }

            if (exclusiveLock is not null && !await TryTakeLockAsync(exclusiveLock, cancellationToken))
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
                else
                {
                    await outer!.RollbackToSavepointAsync(savepoint!, cancellationToken);
                }

                return Result.Failure<IUnitOfWorkTransaction>(AppError.Conflict("Another change to the administrators is in progress. Try again."));
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            throw;
        }

        return Result.Success<IUnitOfWorkTransaction>(new EfTransaction(transaction ?? outer, savepoint));
    }

    /// <summary>
    /// An application lock owned by the transaction: SQL Server releases it at commit or rollback, so it can never be left
    /// behind. Waiting requests then read the committed state, which is what makes their rule check sound.
    /// </summary>
    private async Task<bool> TryTakeLockAsync(string name, CancellationToken cancellationToken)
    {
        var outcome = new SqlParameter("@outcome", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await context.Database.ExecuteSqlRawAsync(
            "EXEC @outcome = sys.sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = @timeout;",
            [outcome, new SqlParameter("@resource", name), new SqlParameter("@timeout", LockTimeoutMilliseconds)],
            cancellationToken);
        return outcome.Value is int code && code >= 0;
    }

    /// <summary>
    /// The transaction this unit of work opened (commit commits, dispose without commit rolls back), or, when it joined one
    /// the caller owns, a scope over a savepoint: commit leaves the work to the owner, dispose without a commit rolls back to
    /// the savepoint. The administrator application lock is owned by the outer transaction and stays held until that ends.
    /// </summary>
    private sealed class EfTransaction(IDbContextTransaction? transaction, string? savepoint) : IUnitOfWorkTransaction
    {
        private bool committed;

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            committed = true;
            return savepoint is null && transaction is not null ? transaction.CommitAsync(cancellationToken) : Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (transaction is null)
            {
                return;
            }

            if (savepoint is null)
            {
                await transaction.DisposeAsync();
            }
            else if (!committed)
            {
                await transaction.RollbackToSavepointAsync(savepoint);
            }
        }
    }
}
