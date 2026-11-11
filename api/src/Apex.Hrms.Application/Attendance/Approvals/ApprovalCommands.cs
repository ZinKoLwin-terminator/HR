using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Attendance.Approvals;

/// <summary>A command that approves or rejects a request. The remarks are optional to approve and required to reject.</summary>
public interface IDecisionCommand : IEntityCommand
{
    string? Remarks { get; }

    ApprovalDecision Decision { get; }
}

public sealed record ApproveAdjustmentCommand(int Id, string? RowVersion, string? Remarks) : IDecisionCommand
{
    public ApprovalDecision Decision => ApprovalDecision.Approve;
}

public sealed record RejectAdjustmentCommand(int Id, string? RowVersion, string? Remarks) : IDecisionCommand
{
    public ApprovalDecision Decision => ApprovalDecision.Reject;
}

public sealed record ApproveOvertimeCommand(int Id, string? RowVersion, string? Remarks) : IDecisionCommand
{
    public ApprovalDecision Decision => ApprovalDecision.Approve;
}

public sealed record RejectOvertimeCommand(int Id, string? RowVersion, string? Remarks) : IDecisionCommand
{
    public ApprovalDecision Decision => ApprovalDecision.Reject;
}

/// <summary>Pending to Approved or Rejected by the signed-in user, for a request that changes nothing else when approved.</summary>
internal sealed class DecisionAction<TEntity, TCommand>(ICurrentUser user, IClock clock) : ICommandAction<TEntity, TCommand>
    where TEntity : ApprovalRequest
    where TCommand : IDecisionCommand
{
    public Result Execute(TEntity entity, TCommand command)
    {
        var actor = AttendanceActors.ActorOf(user);
        return actor.IsFailure
            ? actor
            : entity.Decide(command.Decision, actor.Value, command.Remarks, clock.Today);
    }
}

/// <summary>
/// The activity wording of a decision, as the mock services write it: <c>ADJ-0004 approved for 2026-11-09</c> for an
/// adjustment and <c>OT-0004 rejected</c> for an overtime request.
/// </summary>
internal sealed class DecisionActivity<TEntity, TCommand> : ICommandActivity<TEntity, TCommand>
    where TEntity : ApprovalRequest
    where TCommand : IDecisionCommand
{
    public string Describe(TEntity entity, TCommand command)
    {
        var decision = command.Decision == ApprovalDecision.Approve ? "Approved" : "Rejected";
        return entity is AttendanceAdjustment
            ? ActivityMessages.AdjustmentDecided(entity.Label, decision, entity.Date)
            : ActivityMessages.OvertimeDecided(entity.Label, decision);
    }
}

/// <summary>Deletes a pending request. A decided one stays as the record of the decision.</summary>
internal sealed class ApprovalDeleteHandler<TEntity, TDto>(
    EntityLoader<TEntity> loader,
    IRepository<TEntity> repository,
    IEntityDescriptor<TEntity> descriptor,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<TDto>
    where TEntity : ApprovalRequest
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var allowed = loaded.Value.EnsureCanDelete();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        activity.Record(
            descriptor.ActivityCategory,
            ActivityMessages.Deleted(descriptor.Label, descriptor.ActivitySubject(loaded.Value)));
        repository.Remove(loaded.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
