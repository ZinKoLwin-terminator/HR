using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;
using FluentValidation;

namespace Apex.Hrms.Application.Leave.Applications;

// A leave application needs its own create, update and delete flows: it has no code and no Active/Inactive status, the
// days and the number are worked out by the server, only drafts may change or go, and its rules read other records.
// Reads (get, query) use the generic handlers; the workflow moves are commands (see LeaveCommandActions).

internal sealed class LeaveApplicationCreateHandler(
    IValidator<LeaveApplicationCreateRequest> validator,
    WorkingDayCalculator workingDays,
    LeaveApplicationRules rules,
    LeaveApplicationNumberGenerator numbers,
    IClock clock,
    IRepository<LeaveApplication> repository,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    LeaveApplicationMapping mapper) : ICreateHandler<LeaveApplicationCreateRequest, LeaveApplicationDto>
{
    public async Task<Result<LeaveApplicationDto>> HandleAsync(
        LeaveApplicationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(valid.Error);
        }

        var details = request.ToDetails();
        var consistent = LeaveApplication.Validate(details);
        if (consistent.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(consistent.Error);
        }

        var days = LeaveApplication.DaysFor(details, await workingDays.CountAsync(details.StartDate, details.EndDate, cancellationToken));
        var allowed = await rules.CheckDetailsAsync(details, days, excludedId: 0, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(allowed.Error);
        }

        var number = await numbers.NextFreeAsync(clock.Today, cancellationToken);
        if (number.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(number.Error);
        }

        var created = LeaveApplication.Create(details, days, number.Value, clock.Today);
        if (created.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(created.Error);
        }

        repository.Add(created.Value);
        activity.Record(mapper.ActivityCategory, ActivityMessages.Created(mapper.Label, mapper.Describe(created.Value)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<LeaveApplicationDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}

internal sealed class LeaveApplicationUpdateHandler(
    IValidator<LeaveApplicationUpdateRequest> validator,
    EntityLoader<LeaveApplication> loader,
    WorkingDayCalculator workingDays,
    LeaveApplicationRules rules,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    LeaveApplicationMapping mapper) : IUpdateHandler<LeaveApplicationUpdateRequest, LeaveApplicationDto>
{
    public async Task<Result<LeaveApplicationDto>> HandleAsync(
        int id,
        LeaveApplicationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(loaded.Error);
        }

        var application = loaded.Value;
        var editable = application.EnsureCanUpdate();
        if (editable.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(editable.Error);
        }

        var details = request.ToDetails();
        var consistent = LeaveApplication.Validate(details);
        if (consistent.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(consistent.Error);
        }

        var days = LeaveApplication.DaysFor(details, await workingDays.CountAsync(details.StartDate, details.EndDate, cancellationToken));
        var allowed = await rules.CheckDetailsAsync(details, days, application.Id, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(allowed.Error);
        }

        var updated = application.Update(details, days);
        if (updated.IsFailure)
        {
            return Result.Failure<LeaveApplicationDto>(updated.Error);
        }

        activity.Record(mapper.ActivityCategory, ActivityMessages.Updated(mapper.Label, mapper.Describe(application)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<LeaveApplicationDto>(saved.Error)
            : Result.Success(mapper.ToDto(application));
    }
}

/// <summary>Deletes a draft. Anything that has been submitted is cancelled instead; nothing points at an application.</summary>
internal sealed class LeaveApplicationDeleteHandler(
    EntityLoader<LeaveApplication> loader,
    IRepository<LeaveApplication> repository,
    LeaveApplicationMapping mapper,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<LeaveApplicationDto>
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

        activity.Record(mapper.ActivityCategory, ActivityMessages.Deleted(mapper.Label, mapper.Describe(loaded.Value)));
        repository.Remove(loaded.Value);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
