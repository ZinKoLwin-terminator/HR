using System.Linq.Expressions;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Counts rows of a record type, including types that are not ported to the domain yet
/// (see <see cref="Organization.EmployeeReference"/>). Used by the reference checks.
/// </summary>
public interface IRecordCounter<TRecord>
    where TRecord : class
{
    Task<int> CountAsync(Expression<Func<TRecord, bool>> predicate, CancellationToken cancellationToken);
}
