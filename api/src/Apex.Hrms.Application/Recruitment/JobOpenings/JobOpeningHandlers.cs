using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Recruitment;
using FluentValidation;

namespace Apex.Hrms.Application.Recruitment.JobOpenings;

/// <summary>
/// Makes opening codes such as <c>JOB-2026-007</c>: the year of the posting date and the next number of the database
/// sequence, skipping codes that are already in use (the sequence can lag behind imported data).
/// </summary>
internal sealed class JobOpeningCodeGenerator(IJobOpeningCodeSequence sequence, IRepository<JobOpening> openings)
{
    private const int MaxAttempts = 1000;

    public async Task<Result<string>> NextFreeAsync(int year, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var code = Format(year, await sequence.NextAsync(cancellationToken));
            if (!await openings.AnyAsync(opening => opening.Code == code, cancellationToken))
            {
                return Result.Success(code);
            }
        }

        return Result.Failure<string>(AppError.Conflict("No unused job opening code could be generated. Try again."));
    }

    public static string Format(int year, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"JOB-{year:D4}-{number:D3}");
}

/// <summary>The rules of an opening that need other records: the department exists and the hiring manager is a current employee.</summary>
internal sealed class JobOpeningRules(IRepository<Department> departments, RecruitmentLookup lookup)
{
    public async Task<Result> CheckAsync(JobOpeningDetails details, CancellationToken cancellationToken)
    {
        if (await departments.FindAsync(details.DepartmentId, cancellationToken) is null)
        {
            return Result.Failure(AppError.Validation("Choose a department.", "departmentId", "Choose a department."));
        }

        return await lookup.EnsureCurrentEmployeeAsync(
            details.HiringManagerId,
            "Choose a current employee as hiring manager.",
            "hiringManagerId",
            cancellationToken);
    }
}

internal sealed class JobOpeningCreateHandler(
    IValidator<JobOpeningCreateRequest> validator,
    JobOpeningRules rules,
    JobOpeningCodeGenerator codes,
    IClock clock,
    IRepository<JobOpening> repository,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    JobOpeningMapping mapper) : ICreateHandler<JobOpeningCreateRequest, JobOpeningDto>
{
    public async Task<Result<JobOpeningDto>> HandleAsync(JobOpeningCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(valid.Error);
        }

        var details = request.ToDetails();
        var today = clock.Today;
        var consistent = JobOpening.Validate(details, today);
        if (consistent.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(consistent.Error);
        }

        var allowed = await rules.CheckAsync(details, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(allowed.Error);
        }

        var code = await codes.NextFreeAsync((details.PostedOn ?? today).Year, cancellationToken);
        if (code.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(code.Error);
        }

        var created = JobOpening.Create(details, code.Value, today);
        if (created.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(created.Error);
        }

        repository.Add(created.Value);
        activity.Record(mapper.ActivityCategory, ActivityMessages.Created(mapper.Label, mapper.Describe(created.Value)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<JobOpeningDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}

internal sealed class JobOpeningUpdateHandler(
    IValidator<JobOpeningUpdateRequest> validator,
    EntityLoader<JobOpening> loader,
    JobOpeningRules rules,
    RecruitmentLookup lookup,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    JobOpeningMapping mapper) : IUpdateHandler<JobOpeningUpdateRequest, JobOpeningDto>
{
    public async Task<Result<JobOpeningDto>> HandleAsync(int id, JobOpeningUpdateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(loaded.Error);
        }

        var opening = loaded.Value;
        var editable = opening.EnsureCanEdit();
        if (editable.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(editable.Error);
        }

        var details = request.ToDetails();
        var consistent = JobOpening.Validate(details, clock.Today, opening);
        if (consistent.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(consistent.Error);
        }

        var allowed = await rules.CheckAsync(details, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(allowed.Error);
        }

        var selected = await lookup.SelectedCountAsync(opening.Id, cancellationToken);
        var updated = opening.Update(details, clock.Today, selected);
        if (updated.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(updated.Error);
        }

        activity.Record(mapper.ActivityCategory, ActivityMessages.Updated(mapper.Label, mapper.Describe(opening)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<JobOpeningDto>(saved.Error)
            : Result.Success(mapper.ToDto(opening));
    }
}

/// <summary>
/// Command flow for the status moves of an opening: load against the row version, change the status with the number
/// of selected candidates and today's date, save, and return the opening with its new row version.
/// </summary>
internal sealed class JobOpeningStatusHandler<TCommand>(
    EntityLoader<JobOpening> loader,
    RecruitmentLookup lookup,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    JobOpeningMapping mapper) : ICommandHandler<TCommand, JobOpeningDto>
    where TCommand : IEntityCommand, IJobOpeningStatusCommand
{
    public async Task<Result<JobOpeningDto>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(loaded.Error);
        }

        var selected = await lookup.SelectedCountAsync(loaded.Value.Id, cancellationToken);
        var changed = loaded.Value.ChangeStatus(TCommand.Target, clock.Today, selected);
        if (changed.IsFailure)
        {
            return Result.Failure<JobOpeningDto>(changed.Error);
        }

        activity.Record(
            mapper.ActivityCategory,
            ActivityMessages.JobOpeningStatusSet(mapper.Describe(loaded.Value), JobOpeningStatusText.ToText(TCommand.Target)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<JobOpeningDto>(saved.Error)
            : Result.Success(mapper.ToDto(loaded.Value));
    }
}

/// <summary>A command that moves an opening to a fixed status.</summary>
internal interface IJobOpeningStatusCommand
{
    static abstract JobOpeningStatus Target { get; }
}
