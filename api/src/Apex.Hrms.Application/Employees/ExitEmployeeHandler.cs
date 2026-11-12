using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Employees;

/// <summary>
/// Records that an employee left and, in the same transaction, switches off the sign-in account linked to the employee and
/// changes its Identity security stamp, so an open session ends at its next request. A leaver must not keep access. The
/// last-active-HR-Administrator rule runs first (under the administrator lock, see <see cref="UserRules.ChangeAdministratorAsync"/>):
/// an exit that would remove the last active administrator is refused with a Conflict and nothing changes.
/// An employee without an account, or whose account is already inactive, takes the plain command path.
/// </summary>
internal sealed class ExitEmployeeHandler(
    EntityLoader<Employee> loader,
    ICommandAction<Employee, ExitEmployeeCommand> action,
    IEntityDescriptor<Employee> descriptor,
    IEnumerable<ICommandActivity<Employee, ExitEmployeeCommand>> wording,
    IRepository<AppAccount> accounts,
    IRepository<AppRole> roles,
    IAccountLogins logins,
    UserMapping accountMapping,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    IDtoMapper<Employee, EmployeeDto> mapper) : ICommandHandler<ExitEmployeeCommand, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> HandleAsync(ExitEmployeeCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<EmployeeDto>(loaded.Error);
        }

        var employee = loaded.Value;
        var executed = action.Execute(employee, command);
        if (executed.IsFailure)
        {
            return Result.Failure<EmployeeDto>(executed.Error);
        }

        var account = await FindActiveAccountAsync(employee.Id, cancellationToken);
        var result = account is null
            ? await SaveAsync(employee, command, cancellationToken)
            : await UserRules.ChangeAdministratorAsync(
                unitOfWork,
                accounts,
                roles,
                account,
                () =>
                {
                    RecordEmployeeActivity(employee, command);
                    account.Deactivate();
                    activity.Record(ActivityCategories.System, ActivityMessages.Updated(accountMapping.Label, accountMapping.Describe(account)));
                },
                afterSave: async () =>
                {
                    if (account.ExternalSubject is { } subject)
                    {
                        await logins.InvalidateSessionsAsync(subject, cancellationToken);
                    }
                },
                cancellationToken);

        return result.IsFailure ? Result.Failure<EmployeeDto>(result.Error) : Result.Success(mapper.ToDto(employee));
    }

    /// <summary>The tracked account of the employee when it is not already inactive; null when there is nothing to switch off.</summary>
    private async Task<AppAccount?> FindActiveAccountAsync(int employeeId, CancellationToken cancellationToken)
    {
        var matches = await accounts.ListAsync(account => account.EmployeeId == employeeId, cancellationToken);
        var linked = matches.Count > 0 ? matches[0] : null;
        var tracked = linked is null ? null : await accounts.FindAsync(linked.Id, cancellationToken);
        return tracked is { Status: not AccountStatus.Inactive } ? tracked : null;
    }

    private Task<Result> SaveAsync(Employee employee, ExitEmployeeCommand command, CancellationToken cancellationToken)
    {
        RecordEmployeeActivity(employee, command);
        return unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private void RecordEmployeeActivity(Employee employee, ExitEmployeeCommand command)
    {
        var text = wording.FirstOrDefault()?.Describe(employee, command)
            ?? ActivityMessages.Updated(descriptor.Label, descriptor.ActivitySubject(employee));
        activity.Record(descriptor.ActivityCategory, text);
    }
}
