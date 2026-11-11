using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(HrmsDbContext context, EfActivityRecorder activity) : IUnitOfWork
{
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
            // entry, and commit both together (the transaction rolls back when anything fails).
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            activity.WriteDeferred();
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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
}
