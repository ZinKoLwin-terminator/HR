using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

internal sealed class EfRecordCounter<TRecord>(HrmsDbContext context) : IRecordCounter<TRecord>
    where TRecord : class
{
    public Task<int> CountAsync(Expression<Func<TRecord, bool>> predicate, CancellationToken cancellationToken) =>
        context.Set<TRecord>().CountAsync(predicate, cancellationToken);
}
