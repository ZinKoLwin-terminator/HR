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
        var joined = context.Database.CurrentTransaction is not null;
        var transaction = joined ? null : await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (exclusiveLock is not null && !await TryTakeLockAsync(exclusiveLock, cancellationToken))
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
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

        return Result.Success<IUnitOfWorkTransaction>(new EfTransaction(transaction));
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

    /// <summary>The transaction this unit of work opened, or nothing to do when it joined one the caller owns.</summary>
    private sealed class EfTransaction(IDbContextTransaction? transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction is null ? ValueTask.CompletedTask : transaction.DisposeAsync();
    }
}
