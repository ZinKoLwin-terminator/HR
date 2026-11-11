using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using FluentValidation;

namespace Apex.Hrms.Application.Employees;

/// <summary>The suggested code for a new employee. Consumes a number of the database sequence.</summary>
public interface INextEmployeeCodeHandler
{
    Task<Result<NextEmployeeCodeDto>> HandleAsync(CancellationToken cancellationToken);
}

/// <summary>The history log of one employee, oldest first.</summary>
public interface IEmployeeHistoryHandler
{
    Task<Result<IReadOnlyList<EmploymentHistoryEntryDto>>> HandleAsync(int employeeId, CancellationToken cancellationToken);
}

// The employee needs its own create and update flows (the generic ones assume a code and an Active/Inactive status):
// the code can be generated, the starting status is an employment status, and every change leaves history entries.
// Everything else (get, query, delete, commands) uses the generic handlers.

internal sealed class EmployeeCreateHandler(
    IValidator<EmployeeCreateRequest> validator,
    EmployeeRules rules,
    EmployeeCodeGenerator codes,
    IClock clock,
    ICurrentUser user,
    IRepository<Employee> repository,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    EmployeeMapping mapper) : ICreateHandler<EmployeeCreateRequest, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> HandleAsync(EmployeeCreateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<EmployeeDto>(valid.Error);
        }

        var details = request.ToDetails();
        var personal = request.ToPersonal();
        var status = request.ParseStatus();
        var consistent = Employee.Validate(details);
        if (consistent.IsSuccess)
        {
            consistent = Employee.ValidatePersonal(personal, clock.Today);
        }

        if (consistent.IsFailure)
        {
            return Result.Failure<EmployeeDto>(consistent.Error);
        }

        var allowed = await rules.CheckAsync(details, existing: null, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<EmployeeDto>(allowed.Error);
        }

        var code = details.EmployeeCode is { } given ? Result.Success(given) : await codes.NextFreeAsync(cancellationToken);
        if (code.IsFailure)
        {
            return Result.Failure<EmployeeDto>(code.Error);
        }

        var created = Employee.Create(
            code.Value,
            details,
            personal,
            status,
            new ChangeContext(clock.Today, user.DisplayName),
            allowed.Value.NewPositionTitle);
        if (created.IsFailure)
        {
            return Result.Failure<EmployeeDto>(created.Error);
        }

        repository.Add(created.Value);
        activity.Record(mapper.ActivityCategory, ActivityMessages.Created(mapper.Label, mapper.ActivitySubject(created.Value)));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<EmployeeDto>(saved.Error)
            : Result.Success(mapper.ToDto(created.Value));
    }
}

internal sealed class EmployeeUpdateHandler(
    IValidator<EmployeeUpdateRequest> validator,
    EmployeeRules rules,
    EntityLoader<Employee> loader,
    IClock clock,
    ICurrentUser user,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    EmployeeMapping mapper) : IUpdateHandler<EmployeeUpdateRequest, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> HandleAsync(int id, EmployeeUpdateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<EmployeeDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<EmployeeDto>(loaded.Error);
        }

        var employee = loaded.Value;
        var details = request.ToDetails();
        var consistent = Employee.Validate(details);
        if (consistent.IsFailure)
        {
            return Result.Failure<EmployeeDto>(consistent.Error);
        }

        var allowed = await rules.CheckAsync(details, employee, cancellationToken);
        if (allowed.IsFailure)
        {
            return Result.Failure<EmployeeDto>(allowed.Error);
        }

        var updated = employee.Update(details, new ChangeContext(clock.Today, user.DisplayName), allowed.Value);
        if (updated.IsFailure)
        {
            return Result.Failure<EmployeeDto>(updated.Error);
        }

        activity.Record(mapper.ActivityCategory, ActivityMessages.Updated(mapper.Label, mapper.ActivitySubject(employee)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<EmployeeDto>(saved.Error)
            : Result.Success(mapper.ToDto(employee));
    }
}

/// <summary>PUT /personal: changes only the personal data, so a caller without that permission never has to send it.</summary>
internal sealed class EmployeePersonalUpdateHandler(
    IValidator<EmployeePersonalUpdateRequest> validator,
    EntityLoader<Employee> loader,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    EmployeeMapping mapper) : IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto>
{
    public async Task<Result<EmployeePersonalDto>> HandleAsync(
        int id,
        EmployeePersonalUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<EmployeePersonalDto>(valid.Error);
        }

        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<EmployeePersonalDto>(loaded.Error);
        }

        var updated = loaded.Value.UpdatePersonal(request.ToDetails(), clock.Today);
        if (updated.IsFailure)
        {
            return Result.Failure<EmployeePersonalDto>(updated.Error);
        }

        activity.Record(mapper.ActivityCategory, ActivityMessages.Updated(mapper.Label, mapper.ActivitySubject(loaded.Value)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<EmployeePersonalDto>(saved.Error)
            : Result.Success(((IDtoMapper<Employee, EmployeePersonalDto>)mapper).ToDto(loaded.Value));
    }
}

/// <summary>GET /bank-account: the account in full. The id is the employee id; an employee without an account is NotFound.</summary>
internal sealed class BankAccountGetHandler(EntityLoader<Employee> loader) : IGetByIdHandler<BankAccountDto>
{
    public async Task<Result<BankAccountDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<BankAccountDto>(loaded.Error);
        }

        return loaded.Value.BankAccount is { } account
            ? Result.Success(EmployeeMapping.ToBankDto(loaded.Value, account))
            : Result.Failure<BankAccountDto>(AppError.NotFound($"Employee #{id} has no bank account."));
    }
}

/// <summary>
/// PUT /bank-account: sets or replaces the account. An existing account needs its row version (409 when stale);
/// the first account of an employee needs none.
/// </summary>
internal sealed class BankAccountUpdateHandler(
    IValidator<BankAccountUpdateRequest> validator,
    EntityLoader<Employee> loader,
    IRepository<EmployeeBankAccount> accounts,
    EmployeeMapping mapper,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IUpdateHandler<BankAccountUpdateRequest, BankAccountDto>
{
    public async Task<Result<BankAccountDto>> HandleAsync(int id, BankAccountUpdateRequest request, CancellationToken cancellationToken)
    {
        var valid = await validator.CheckAsync(request, cancellationToken);
        if (valid.IsFailure)
        {
            return Result.Failure<BankAccountDto>(valid.Error);
        }

        var loaded = await loader.LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<BankAccountDto>(loaded.Error);
        }

        var employee = loaded.Value;
        var existing = employee.BankAccount;
        if (existing is not null)
        {
            var expected = RowVersionToken.Decode(request.RowVersion);
            if (expected.IsFailure)
            {
                return Result.Failure<BankAccountDto>(expected.Error);
            }

            if (!existing.RowVersion.AsSpan().SequenceEqual(expected.Value))
            {
                return Result.Failure<BankAccountDto>(RowVersionToken.Stale);
            }

            accounts.ExpectRowVersion(existing, expected.Value);
        }

        var set = employee.SetBankAccount(request.ToDetails());
        if (set.IsFailure)
        {
            return Result.Failure<BankAccountDto>(set.Error);
        }

        if (existing is null)
        {
            accounts.Add(set.Value);
        }

        // The entry names the employee by code and says nothing about the account.
        activity.Record(mapper.ActivityCategory, ActivityMessages.Updated(mapper.Label, mapper.ActivitySubject(employee)));

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<BankAccountDto>(saved.Error)
            : Result.Success(EmployeeMapping.ToBankDto(employee, set.Value));
    }
}

internal sealed class EmployeeHistoryHandler(
    EntityLoader<Employee> loader,
    IRepository<EmploymentHistoryEntry> history) : IEmployeeHistoryHandler
{
    public async Task<Result<IReadOnlyList<EmploymentHistoryEntryDto>>> HandleAsync(int employeeId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(employeeId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<IReadOnlyList<EmploymentHistoryEntryDto>>(loaded.Error);
        }

        var entries = await history.ListAsync(entry => entry.EmployeeId == employeeId, cancellationToken);
        return Result.Success<IReadOnlyList<EmploymentHistoryEntryDto>>([.. entries.Select(EmployeeMapping.ToHistoryDto)]);
    }
}

internal sealed class NextEmployeeCodeHandler(EmployeeCodeGenerator codes) : INextEmployeeCodeHandler
{
    public async Task<Result<NextEmployeeCodeDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var code = await codes.NextFreeAsync(cancellationToken);
        return code.IsFailure
            ? Result.Failure<NextEmployeeCodeDto>(code.Error)
            : Result.Success(new NextEmployeeCodeDto(code.Value));
    }
}
