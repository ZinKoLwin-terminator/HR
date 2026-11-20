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
/// An employee without an account, or whose account is already inactive, changes no login, but the save still runs under the
/// administrator lock so it cannot interleave with linking an account to the employee.
/// </summary>
internal sealed class ExitEmployeeHandler(
    EntityLoader<Employee> loader,
    ICommandAction<Employee, ExitEmployeeCommand> action,
    IEntityDescriptor<Employee> descriptor,
    IEnumerable<ICommandActivity<Employee, ExitEmployeeCommand>> wording,
    IRepository<Employee> employees,
    ICurrentUser currentUser,
    IRepository<AppAccount> accounts,
    IRepository<AppRole> roles,
    IAccountLogins logins,
    UserMapping accountMapping,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    IDtoMapper<Employee, EmployeeDto> mapper) : ICommandHandler<ExitEmployeeCommand, EmployeeDto>
{
    public const string OwnExitMessage = "You cannot record your own exit.";
    public const string AdministratorOnlyMessage = "Only an HR Administrator can record the exit of an HR Administrator.";
    public const string AccountLinkedMessage = "A user account was just linked to this employee. Try again.";

    public async Task<Result<EmployeeDto>> HandleAsync(ExitEmployeeCommand command, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(command.Id, command.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<EmployeeDto>(loaded.Error);
        }

        var employee = loaded.Value;
        if (currentUser.EmployeeId is { } own && own == employee.Id)
        {
            return Result.Failure<EmployeeDto>(AppError.Conflict(OwnExitMessage));
        }

        var executed = action.Execute(employee, command);
        if (executed.IsFailure)
        {
            return Result.Failure<EmployeeDto>(executed.Error);
        }

        var account = await FindActiveAccountAsync(employee.Id, cancellationToken);
        if (account is not null && await IsAdministratorAccountAsync(account, cancellationToken)
            && !await UserRules.IsAdministratorAsync(accounts, roles, currentUser, cancellationToken))
        {
            return Result.Failure<EmployeeDto>(AppError.Forbidden(AdministratorOnlyMessage));
        }

        var result = account is null
            ? await SaveWithoutAccountAsync(employee, command, cancellationToken)
            : await UserRules.ChangeAdministratorAsync(
                unitOfWork,
                accounts,
                employees,
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

    private async Task<bool> IsAdministratorAccountAsync(AppAccount account, CancellationToken cancellationToken) =>
        await roles.FindAsync(account.RoleId, cancellationToken) is { Name: SecurityPolicy.AdministratorRoleName };

    /// <summary>The tracked account of the employee when it is not already inactive; null when there is nothing to switch off.</summary>
    private async Task<AppAccount?> FindActiveAccountAsync(int employeeId, CancellationToken cancellationToken)
    {
        var matches = await accounts.ListAsync(account => account.EmployeeId == employeeId, cancellationToken);
        var linked = matches.Count > 0 ? matches[0] : null;
        var tracked = linked is null ? null : await accounts.FindAsync(linked.Id, cancellationToken);
        return tracked is { Status: not AccountStatus.Inactive } ? tracked : null;
    }

    /// <summary>
    /// The exit of an employee without an active account. It still runs under the administrator lock, which the link of an
    /// account to its employee (<see cref="LinkUserEmployeeHandler"/>) takes too: the two cannot interleave, so an account is
    /// never linked to an employee whose exit is being saved. An active account linked since the check above (the link
    /// committed first) is a Conflict; the retry takes the path that switches that account off.
    /// </summary>
    private async Task<Result> SaveWithoutAccountAsync(Employee employee, ExitEmployeeCommand command, CancellationToken cancellationToken)
    {
        var begun = await unitOfWork.BeginTransactionAsync(SecurityPolicy.AdministratorLock, cancellationToken);
        if (begun.IsFailure)
        {
            return Result.Failure(begun.Error);
        }

        await using var transaction = begun.Value;
        var employeeId = employee.Id;
        if (await accounts.AnyAsync(account => account.EmployeeId == employeeId && account.Status != AccountStatus.Inactive, cancellationToken))
        {
            return Result.Failure(AppError.Conflict(AccountLinkedMessage));
        }

        RecordEmployeeActivity(employee, command);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private void RecordEmployeeActivity(Employee employee, ExitEmployeeCommand command)
    {
        var text = wording.FirstOrDefault()?.Describe(employee, command)
            ?? ActivityMessages.Updated(descriptor.Label, descriptor.ActivitySubject(employee));
        activity.Record(descriptor.ActivityCategory, text);
    }
}
