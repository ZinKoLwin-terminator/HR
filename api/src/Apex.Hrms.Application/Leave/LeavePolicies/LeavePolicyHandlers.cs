using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;
using FluentValidation;

namespace Apex.Hrms.Application.Leave.LeavePolicies;

/// <summary>
/// Create needs its own flow because the "one active policy per group" rule depends on the status the new policy
/// starts with, which the shared create flow does not pass to the rules.
/// </summary>
internal sealed class LeavePolicyCreateHandler(
    IValidator<LeavePolicyCreateRequest> validator,
    LeavePolicyRules rules,
    IRepository<LeavePolicy> repository,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    LeavePolicyMapping mapper) : ICreateHandler<LeavePolicyCreateRequest, LeavePolicyDto>
{
    public async Task<Result<LeavePolicyDto>> HandleAsync(LeavePolicyCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<LeavePolicyDto>(valid.Error);
        }

        var details = request.ToDetails();
        var consistent = LeavePolicy.Validate(details);
        if (consistent.IsFailure)
        {
            return Result.Failure<LeavePolicyDto>(consistent.Error);
        }

        var status = request.ParseStatus();
        var allowed = await rules.CheckCreateAsync(details, status, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<LeavePolicyDto>(allowed.Error);
        }

        var created = LeavePolicy.Create(details, status);
        if (created.IsFailure)
        {
            return Result.Failure<LeavePolicyDto>(created.Error);
        }

        repository.Add(created.Value);
        activity.Record(mapper.ActivityCategory, ActivityMessages.Created(mapper.Label, mapper.Describe(created.Value)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<LeavePolicyDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}

/// <summary>Activates a policy unless another active policy already covers its group. Activating an active policy is a no-op.</summary>
internal sealed class LeavePolicyActivateHandler(
    EntityLoader<LeavePolicy> loader,
    LeavePolicyRules rules,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    LeavePolicyMapping mapper) : ICommandHandler<ActivateCommand, LeavePolicyDto>
{
    public async Task<Result<LeavePolicyDto>> HandleAsync(ActivateCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<LeavePolicyDto>(loaded.Error);
        }

        var policy = loaded.Value;
        var wasActive = policy.IsActive;
        if (!wasActive)
        {
            var allowed = await rules.CheckActivationAsync(policy, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result.Failure<LeavePolicyDto>(allowed.Error);
            }
        }

        policy.Activate();
        if (!wasActive)
        {
            activity.Record(mapper.ActivityCategory, ActivityMessages.Updated(mapper.Label, mapper.Describe(policy)));
        }
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure ? Result.Failure<LeavePolicyDto>(saved.Error) : Result.Success(mapper.ToDto(policy));
    }
}

internal sealed class DeactivateLeavePolicyAction : ICommandAction<LeavePolicy, DeactivateCommand>
{
    public Result Execute(LeavePolicy entity, DeactivateCommand command) => entity.Deactivate();
}
