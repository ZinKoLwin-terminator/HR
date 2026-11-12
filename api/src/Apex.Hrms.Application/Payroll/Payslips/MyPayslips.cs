using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Payslips;

/// <summary>
/// Employee self-service payslips (<c>/api/me/payslips</c>): only the signed-in caller's own payslips, and only those of
/// runs that are Approved or Paid (never a Draft or Pending Approval run, whose figures can still change). The caller needs
/// no payroll permission; a caller with no linked employee gets an empty page and a 404 on any id. The shape is the usual
/// <see cref="PayslipDto"/> (masked bank account only).
/// </summary>
public interface IMyPayslipsHandler
{
    Task<Result<PagedResult<PayslipDto>>> QueryAsync(int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<Result<PayslipDto>> GetAsync(int id, CancellationToken cancellationToken);
}

internal sealed class MyPayslipsHandler(
    IRepository<Payslip> payslips,
    IRepository<PayrollRun> runs,
    ICurrentUser user,
    PayslipMapping mapper) : IMyPayslipsHandler
{
    public async Task<Result<PagedResult<PayslipDto>>> QueryAsync(int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var size = Paging.ClampPageSize(pageSize);
        if (user.EmployeeId is not { } own)
        {
            return Result.Success(new PagedResult<PayslipDto>([], 0, 0, size));
        }

        var runIds = await ReleasedRunIdsAsync(cancellationToken);
        var page = await payslips.QueryAsync(
            new EntityQuery<Payslip>(
                payslip => payslip.EmployeeId == own && runIds.Contains(payslip.PayrollRunId),
                new SortCriterion<Payslip>(payslip => payslip.PayrollRunId, true),
                Math.Max(0, pageIndex),
                size),
            cancellationToken);
        return Result.Success(page.Map(mapper.ToDto));
    }

    public async Task<Result<PayslipDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        if (user.EmployeeId is not { } own)
        {
            return Result.Failure<PayslipDto>(NotFoundError.For("Payslip", id));
        }

        var runIds = await ReleasedRunIdsAsync(cancellationToken);
        var payslip = await payslips.FindAsync(
            id,
            candidate => candidate.EmployeeId == own && runIds.Contains(candidate.PayrollRunId),
            cancellationToken);
        return payslip is null
            ? Result.Failure<PayslipDto>(NotFoundError.For("Payslip", id))
            : Result.Success(mapper.ToDto(payslip));
    }

    private async Task<int[]> ReleasedRunIdsAsync(CancellationToken cancellationToken) =>
        [.. (await runs.ListAsync(run => run.Status == PayrollRunStatus.Approved || run.Status == PayrollRunStatus.Paid, cancellationToken))
            .Select(run => run.Id)];
}
