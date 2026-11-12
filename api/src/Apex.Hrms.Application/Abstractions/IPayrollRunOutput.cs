using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Removes what a payroll run produced when the run returns to Draft. The database locks the payslips, their lines and the
/// warnings of an approved run, and allows deleting them only once the run itself is Draft, so the order matters: the run
/// is saved as Draft first and the payslips and warnings are deleted afterwards, in the same transaction.
/// </summary>
public interface IPayrollRunOutput
{
    /// <summary>
    /// Saves the pending changes (the run, now in Draft, and its activity entry) and then, in the same transaction, deletes
    /// the payslips (their lines go with them) and the warnings of the run with explicit statements, so no later change
    /// tracking can put the delete in front of the update. Like <see cref="IUnitOfWork.SaveChangesAsync"/>, concurrency and
    /// constraint failures come back as a failed result and nothing is kept.
    /// </summary>
    Task<Result> SaveAndClearAsync(int runId, CancellationToken cancellationToken);
}
