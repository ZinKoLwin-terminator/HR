using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Returns a run's output to nothing. The triggers of the pay schema refuse to delete the payslips, lines or warnings of an
/// Approved run and accept it once the run is Draft, so the run (already changed to Draft in the change tracker) is saved
/// first and the deletes follow as explicit <c>ExecuteDelete</c> statements: the change tracker could otherwise put a delete
/// in front of the update. Both steps run in one transaction; the lines of a payslip go with it (ON DELETE CASCADE).
/// </summary>
internal sealed class EfPayrollRunOutput(HrmsDbContext context, IUnitOfWork unitOfWork) : IPayrollRunOutput
{
    public async Task<Result> SaveAndClearAsync(int runId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Step 1: the run becomes Draft (with its activity entry). A failure (a stale row version, a constraint) is returned
        // as a result and the transaction is rolled back when it is disposed.
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        try
        {
            // Step 2: the payslips (and their lines), then the warnings. The run is Draft now, so the triggers allow it.
            await context.Set<Payslip>().Where(payslip => payslip.PayrollRunId == runId).ExecuteDeleteAsync(cancellationToken);
            await context.Set<PayrollRunWarning>().Where(warning => warning.PayrollRunId == runId).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (SqlException exception) when (SqlConflicts.Translate(exception.Number, exception.Message) is not null)
        {
            return Result.Failure(SqlConflicts.Translate(exception.Number, exception.Message)!);
        }
    }
}
