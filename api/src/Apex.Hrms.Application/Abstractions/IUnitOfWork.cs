using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists pending changes. Concurrency, unique-key and reference-constraint violations are returned
    /// as <see cref="ErrorType.Conflict"/> failures rather than thrown.
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
