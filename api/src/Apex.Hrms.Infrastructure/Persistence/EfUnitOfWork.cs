using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(HrmsDbContext context) : IUnitOfWork
{
    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
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
