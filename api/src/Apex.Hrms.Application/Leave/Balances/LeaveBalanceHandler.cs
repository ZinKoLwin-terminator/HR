using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Leave.Balances;

/// <summary>
/// One leave type's figures for an employee and year. Mirrors the LeaveBalance interface in leave.models.ts.
/// <c>entitled - taken - pending</c> is <c>available</c>, never below zero.
/// </summary>
public sealed record LeaveBalanceDto(
    int LeaveTypeId,
    string Code,
    string Name,
    string Color,
    decimal Entitled,
    decimal Taken,
    decimal Pending,
    decimal Available);

/// <summary><c>GET /api/leave/balances?employeeId&amp;year</c>: balances worked out on request, never stored.</summary>
public interface ILeaveBalanceHandler
{
    /// <param name="employeeId">The employee.</param>
    /// <param name="year">The year; null means the current year of the business time zone.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result<IReadOnlyList<LeaveBalanceDto>>> HandleAsync(int employeeId, int? year, CancellationToken cancellationToken);
}

internal sealed class LeaveBalanceHandler(
    IRepository<Employee> employees,
    IAccessScope<Employee> scope,
    ICurrentUser user,
    LeaveBalanceService balances,
    IClock clock) : ILeaveBalanceHandler
{
    public async Task<Result<IReadOnlyList<LeaveBalanceDto>>> HandleAsync(
        int employeeId,
        int? year,
        CancellationToken cancellationToken)
    {
        var chosen = year ?? clock.Today.Year;
        if (chosen is < LeaveBalanceService.FirstYear or > LeaveBalanceService.LastYear)
        {
            var message = $"Year must be between {LeaveBalanceService.FirstYear} and {LeaveBalanceService.LastYear}.";
            return Result.Failure<IReadOnlyList<LeaveBalanceDto>>(AppError.Validation(message, "year", message));
        }

        var employee = await employees.FindAsync(employeeId, scope.PredicateFor(user), cancellationToken);
        if (employee is null)
        {
            return Result.Failure<IReadOnlyList<LeaveBalanceDto>>(NotFoundError.For("Employee", employeeId));
        }

        var lines = await balances.ForEmployeeAsync(employee, chosen, cancellationToken);
        return Result.Success<IReadOnlyList<LeaveBalanceDto>>([
            .. lines.Select(line => new LeaveBalanceDto(
                line.LeaveTypeId,
                line.Code,
                line.Name,
                line.Color,
                line.Entitled,
                line.Taken,
                line.Pending,
                line.Available)),
        ]);
    }
}
