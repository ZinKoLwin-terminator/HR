using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Employees;

// The domain operations behind the employee commands. Status never changes through PUT: leaving, coming back and the
// other status moves each have their own command, and each one leaves a history entry.

internal sealed class ExitEmployeeAction(IClock clock, ICurrentUser user) : ICommandAction<Employee, ExitEmployeeCommand>
{
    public Result Execute(Employee entity, ExitEmployeeCommand command)
    {
        if (command.ExitDate is not { } exitDate)
        {
            return EmployeeCommandInput.Invalid("exitDate", "Exit date is required.");
        }

        var status = EmployeeCommandInput.ParseStatus(command.Status, "Status is required: Resigned or Terminated.");
        return status.IsFailure
            ? status
            : entity.Exit(exitDate, status.Value, new ChangeContext(clock.Today, user.DisplayName));
    }
}

internal sealed class ChangeEmployeeStatusAction(IClock clock, ICurrentUser user) : ICommandAction<Employee, ChangeEmployeeStatusCommand>
{
    public Result Execute(Employee entity, ChangeEmployeeStatusCommand command)
    {
        var status = EmployeeCommandInput.ParseStatus(command.Status, "Status is required.");
        return status.IsFailure
            ? status
            : entity.ChangeStatus(status.Value, new ChangeContext(clock.Today, user.DisplayName));
    }
}

/// <summary>Brings a former employee back. The status defaults to Active.</summary>
internal sealed class ReinstateEmployeeAction(IClock clock, ICurrentUser user) : ICommandAction<Employee, ReinstateEmployeeCommand>
{
    public Result Execute(Employee entity, ReinstateEmployeeCommand command)
    {
        var status = string.IsNullOrWhiteSpace(command.Status)
            ? Result.Success(EmploymentStatus.Active)
            : EmployeeCommandInput.ParseStatus(command.Status, "Status is required.");
        return status.IsFailure
            ? status
            : entity.Reinstate(status.Value, new ChangeContext(clock.Today, user.DisplayName));
    }
}

internal static class EmployeeCommandInput
{
    public static Result Invalid(string field, string message) =>
        Result.Failure(AppError.Validation(message, field, message));

    public static Result<EmploymentStatus> ParseStatus(string? text, string requiredMessage)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result.Failure<EmploymentStatus>(AppError.Validation(requiredMessage, "status", requiredMessage));
        }

        if (!EmploymentStatusText.TryParse(text.Trim(), out var status))
        {
            var message = $"Status must be {EmploymentStatusText.Map.OneOf}.";
            return Result.Failure<EmploymentStatus>(AppError.Validation(message, "status", message));
        }

        return Result.Success(status);
    }
}
