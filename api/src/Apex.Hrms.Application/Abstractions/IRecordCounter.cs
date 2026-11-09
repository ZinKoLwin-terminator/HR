using System.Linq.Expressions;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Counts rows of a record type, including read models that are not aggregates (see
/// <see cref="Employees.EmployeeOrgLink"/>). Used by the reference checks.
/// </summary>
public interface IRecordCounter<TRecord>
    where TRecord : class
{
    Task<int> CountAsync(Expression<Func<TRecord, bool>> predicate, CancellationToken cancellationToken);
}
