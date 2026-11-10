using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.Applications;

/// <summary>The domain operation of one leave command. Unlike <see cref="ICommandAction{TEntity, TCommand}"/> it may read other records.</summary>
internal interface ILeaveCommandAction<in TCommand>
{
    Task<Result> ExecuteAsync(LeaveApplication application, TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Command flow for leave: load inside the access scope and against the row version, run the action, save, and return
/// the application with its new row version.
/// </summary>
internal sealed class LeaveCommandHandler<TCommand>(
    EntityLoader<LeaveApplication> loader,
    ILeaveCommandAction<TCommand> action,
    IUnitOfWork unitOfWork,
    LeaveApplicationMapping mapper) : ICommandHandler<TCommand, LeaveApplicationDto>
    where TCommand : IEntityCommand
{
    public async Task<Result<LeaveApplicationDto>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(loaded.Error);
        }

        var executed = await action.ExecuteAsync(loaded.Value, command, cancellationToken);
        if (executed.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(executed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<LeaveApplicationDto>(saved.Error)
            : Result.Success(mapper.ToDto(loaded.Value));
    }
}

/// <summary>Draft to Submitted: status, overlap, supporting document and balance are checked, then the application date becomes today.</summary>
internal sealed class SubmitLeaveAction(LeaveApplicationRules rules, IClock clock) : ILeaveCommandAction<SubmitLeaveCommand>
{
    public async Task<Result> ExecuteAsync(LeaveApplication application, SubmitLeaveCommand command, CancellationToken cancellationToken)
    {
        var allowed = application.EnsureCanSubmit();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var checkedRules = await rules.CheckSubmitAsync(application, cancellationToken);
        return checkedRules.IsFailure ? checkedRules : application.Submit(clock.Today);
    }
}

/// <summary>Submitted to Approved. Nobody approves their own request; the built-in administrator may.</summary>
internal sealed class ApproveLeaveAction(ICurrentUser user, IClock clock) : ILeaveCommandAction<ApproveLeaveCommand>
{
    public Task<Result> ExecuteAsync(LeaveApplication application, ApproveLeaveCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(LeaveActions.Decide(application, LeaveDecision.Approve, command.Remarks, user, clock));
}

/// <summary>Submitted to Rejected; the remarks are the reason. Nobody rejects their own request.</summary>
internal sealed class RejectLeaveAction(ICurrentUser user, IClock clock) : ILeaveCommandAction<RejectLeaveCommand>
{
    public Task<Result> ExecuteAsync(LeaveApplication application, RejectLeaveCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(LeaveActions.Decide(application, LeaveDecision.Reject, command.Remarks, user, clock));
}

/// <summary>Submitted, or approved leave that has not started, to Cancelled.</summary>
internal sealed class CancelLeaveAction(ICurrentUser user, IClock clock) : ILeaveCommandAction<CancelLeaveCommand>
{
    public Task<Result> ExecuteAsync(LeaveApplication application, CancelLeaveCommand command, CancellationToken cancellationToken)
    {
        var actor = LeaveActions.ActorOf(user);
        if (actor.IsFailure)
        {
            return Task.FromResult<Result>(actor);
        }

        var remarks = LeaveActions.CheckRemarks(command.Remarks);
        return Task.FromResult(remarks.IsFailure ? remarks : application.Cancel(actor.Value, command.Remarks, clock.Today));
    }
}

internal static class LeaveActions
{
    public static Result<LeaveActor> ActorOf(ICurrentUser user) =>
        user is { IsAuthenticated: true, UserId: not null }
            ? Result.Success(new LeaveActor(user.UserId, user.EmployeeId, user.DisplayName))
            : Result.Failure<LeaveActor>(AppError.Unauthorized("Sign in to act on a leave request."));

    /// <summary>The remarks are kept in a column of 250 characters.</summary>
    public static Result CheckRemarks(string? remarks) =>
        remarks is not null && remarks.Trim().Length > LeaveApplicationLimits.RemarksMaxLength
            ? Result.Failure(AppError.Validation(
                $"Remarks must be at most {LeaveApplicationLimits.RemarksMaxLength} characters.",
                "remarks",
                $"Remarks must be at most {LeaveApplicationLimits.RemarksMaxLength} characters."))
            : Result.Success();

    public static Result Decide(
        LeaveApplication application,
        LeaveDecision decision,
        string? remarks,
        ICurrentUser user,
        IClock clock)
    {
        var actor = ActorOf(user);
        if (actor.IsFailure)
        {
            return actor;
        }

        var checkedRemarks = CheckRemarks(remarks);
        return checkedRemarks.IsFailure
            ? checkedRemarks
            : application.Decide(decision, actor.Value, remarks, clock.Today);
    }
}
