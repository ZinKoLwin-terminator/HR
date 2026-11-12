using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Payroll;
using FluentValidation;

namespace Apex.Hrms.Application.Payroll.Assignments;

// A salary assignment has its own create, update and delete flows: it has no code and no status, its rules read the
// employee, the job grade and the structure, the activity entries name the employee by code (never the name or the amount),
// and the last assignment of an employee cannot be deleted. Reads use the generic handlers.

internal sealed class SalaryAssignmentCreateHandler(
    IValidator<SalaryAssignmentCreateRequest> validator,
    SalaryAssignmentRules rules,
    IRepository<SalaryAssignment> repository,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    SalaryAssignmentMapping mapper) : ICreateHandler<SalaryAssignmentCreateRequest, SalaryAssignmentDto>
{
    public async Task<Result<SalaryAssignmentDto>> HandleAsync(
        SalaryAssignmentCreateRequest request,
        CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(valid.Error);
        }

        var details = request.ToDetails();
        var consistent = SalaryAssignment.Validate(details);
        if (consistent.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(consistent.Error);
        }

        var allowed = await rules.CheckAsync(details, excludedId: 0, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(allowed.Error);
        }

        var created = SalaryAssignment.Create(details);
        if (created.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(created.Error);
        }

        repository.Add(created.Value);
        activity.Record(
            mapper.ActivityCategory,
            ActivityMessages.Created(mapper.Label, SalaryAssignmentMapping.Subject(allowed.Value.EmployeeCode, details.EffectiveFrom)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<SalaryAssignmentDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}

internal sealed class SalaryAssignmentUpdateHandler(
    IValidator<SalaryAssignmentUpdateRequest> validator,
    EntityLoader<SalaryAssignment> loader,
    SalaryAssignmentRules rules,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    SalaryAssignmentMapping mapper) : IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto>
{
    public async Task<Result<SalaryAssignmentDto>> HandleAsync(
        int id,
        SalaryAssignmentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(loaded.Error);
        }

        var assignment = loaded.Value;
        var details = request.ToDetails();
        var consistent = SalaryAssignment.Validate(details);
        if (consistent.IsSuccess)
        {
            consistent = assignment.EnsureSameEmployee(details.EmployeeId);
        }

        if (consistent.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(consistent.Error);
        }

        var allowed = await rules.CheckAsync(details, assignment.Id, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(allowed.Error);
        }

        var updated = assignment.Update(details);
        if (updated.IsFailure)
        {
            return Result.Failure<SalaryAssignmentDto>(updated.Error);
        }

        activity.Record(
            mapper.ActivityCategory,
            ActivityMessages.Updated(mapper.Label, SalaryAssignmentMapping.Subject(allowed.Value.EmployeeCode, details.EffectiveFrom)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<SalaryAssignmentDto>(saved.Error)
            : Result.Success(mapper.ToDto(assignment));
    }
}

/// <summary>Deletes an assignment unless it is the last one of its employee. The payroll permission to do this is Edit (the seed has no payroll Delete).</summary>
internal sealed class SalaryAssignmentDeleteHandler(
    EntityLoader<SalaryAssignment> loader,
    IRepository<SalaryAssignment> repository,
    IRepository<Employee> employees,
    SalaryAssignmentMapping mapper,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IDeleteHandler<SalaryAssignmentDto>
{
    public async Task<Result> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, rowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error);
        }

        var assignment = loaded.Value;
        var employeeId = assignment.EmployeeId;
        if (!await repository.AnyAsync(other => other.EmployeeId == employeeId && other.Id != id, cancellationToken))
        {
            return Result.Failure(AppError.Conflict(
                "An employee needs at least one salary assignment. Add a new one instead."));
        }

        var employee = await employees.FindAsync(employeeId, cancellationToken);
        var subject = employee is null
            ? mapper.Describe(assignment)
            : SalaryAssignmentMapping.Subject(employee.EmployeeCode, assignment.EffectiveFrom);
        activity.Record(mapper.ActivityCategory, ActivityMessages.Deleted(mapper.Label, subject));
        repository.Remove(assignment);
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
