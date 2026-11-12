using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Runs;

// The workflow moves of a payroll run. Each one loads the run inside the access scope and against the row version, runs the
// domain move, writes its activity entry (the wording of the frontend's mock; never an amount, a name or a bank number) in the
// same save, and returns the run with its new row version. The user must be signed in: the run keeps who processed and who
// approved it, and nobody approves what they processed.

/// <summary>The signed-in account that acts on a run.</summary>
internal static class PayrollActors
{
    public static Result<PayrollActor> ActorOf(ICurrentUser user) =>
        user is { IsAuthenticated: true, UserId: { } userId }
            ? Result.Success(new PayrollActor(userId, user.DisplayName))
            : Result.Failure<PayrollActor>(AppError.Unauthorized("Sign in to act on a payroll period."));
}

/// <summary>Draft to Pending Approval: earlier periods are finished, the payslips are calculated and kept together with the run.</summary>
internal sealed class ProcessPayrollRunHandler(
    EntityLoader<PayrollRun> loader,
    PayrollProcessor processor,
    IRepository<Payslip> payslips,
    ICurrentUser user,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    PayrollRunMapping mapper) : ICommandHandler<ProcessPayrollRunCommand, PayrollRunDto>
{
    public async Task<Result<PayrollRunDto>> HandleAsync(ProcessPayrollRunCommand command, CancellationToken cancellationToken)
    {
        var actor = PayrollActors.ActorOf(user);
        if (actor.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(actor.Error);
        }

        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(loaded.Error);
        }

        var run = loaded.Value;
        var allowed = run.EnsureCanProcess();
        if (allowed.IsSuccess)
        {
            allowed = await processor.EnsureEarlierPeriodsFinishedAsync(run, cancellationToken);
        }

        if (allowed.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(allowed.Error);
        }

        var processed = await processor.ProcessAsync(run, actor.Value.UserId, cancellationToken);
        if (processed.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(processed.Error);
        }

        var result = run.Process(actor.Value, processed.Value.Totals, processed.Value.Warnings, clock.Today, clock.UtcNow.UtcDateTime);
        if (result.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(result.Error);
        }

        foreach (var payslip in processed.Value.Payslips)
        {
            payslips.Add(payslip);
        }

        activity.Record(
            mapper.ActivityCategory,
            $"Payroll for {run.PeriodName} processed ({processed.Value.Payslips.Count.ToString(CultureInfo.InvariantCulture)} payslips)");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<PayrollRunDto>(saved.Error) : Result.Success(mapper.ToDto(run));
    }
}

/// <summary>Pending Approval to Approved, by a user other than the one who processed the run.</summary>
internal sealed class ApprovePayrollRunHandler(
    EntityLoader<PayrollRun> loader,
    ICurrentUser user,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    PayrollRunMapping mapper) : ICommandHandler<ApprovePayrollRunCommand, PayrollRunDto>
{
    public async Task<Result<PayrollRunDto>> HandleAsync(ApprovePayrollRunCommand command, CancellationToken cancellationToken)
    {
        var actor = PayrollActors.ActorOf(user);
        if (actor.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(actor.Error);
        }

        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(loaded.Error);
        }

        var run = loaded.Value;
        var approved = run.Approve(actor.Value, clock.Today);
        if (approved.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(approved.Error);
        }

        activity.Record(mapper.ActivityCategory, $"Payroll for {run.PeriodName} approved");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<PayrollRunDto>(saved.Error) : Result.Success(mapper.ToDto(run));
    }
}

/// <summary>Approved to Paid. Sample only: nothing is paid and no bank is contacted.</summary>
internal sealed class MarkPayrollRunPaidHandler(
    EntityLoader<PayrollRun> loader,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    PayrollRunMapping mapper) : ICommandHandler<MarkPayrollRunPaidCommand, PayrollRunDto>
{
    public async Task<Result<PayrollRunDto>> HandleAsync(MarkPayrollRunPaidCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(loaded.Error);
        }

        var run = loaded.Value;
        var paid = run.MarkPaid(clock.Today);
        if (paid.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(paid.Error);
        }

        activity.Record(mapper.ActivityCategory, $"Payroll for {run.PeriodName} marked as paid (sample)");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<PayrollRunDto>(saved.Error) : Result.Success(mapper.ToDto(run));
    }
}

/// <summary>
/// Pending Approval or Approved back to Draft. The run is saved as Draft first and its payslips and warnings are deleted
/// afterwards, in one transaction (see <see cref="IPayrollRunOutput"/>).
/// </summary>
internal sealed class ReturnPayrollRunToDraftHandler(
    EntityLoader<PayrollRun> loader,
    IPayrollRunOutput output,
    IActivityRecorder activity,
    PayrollRunMapping mapper) : ICommandHandler<ReturnPayrollRunToDraftCommand, PayrollRunDto>
{
    public const int ReasonMaxLength = 200;

    public async Task<Result<PayrollRunDto>> HandleAsync(ReturnPayrollRunToDraftCommand command, CancellationToken cancellationToken)
    {
        var reason = command.Reason?.Trim() ?? string.Empty;
        if (reason.Length > ReasonMaxLength)
        {
            return Result.Failure<PayrollRunDto>(AppError.Validation(
                $"The reason must be at most {ReasonMaxLength} characters.",
                "reason",
                $"The reason must be at most {ReasonMaxLength} characters."));
        }

        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(loaded.Error);
        }

        var run = loaded.Value;
        var returned = run.ReturnToDraft();
        if (returned.IsFailure)
        {
            return Result.Failure<PayrollRunDto>(returned.Error);
        }

        activity.Record(
            mapper.ActivityCategory,
            $"Payroll for {run.PeriodName} returned to draft{(reason.Length > 0 ? $": {reason}" : string.Empty)}");
        var saved = await output.SaveAndClearAsync(run.Id, cancellationToken);
        return saved.IsFailure ? Result.Failure<PayrollRunDto>(saved.Error) : Result.Success(mapper.ToDto(run));
    }
}
