using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.Applications;

/// <summary>
/// The rules of a leave application that need other records. On create and update: the employee exists, the leave
/// type is active and open to the employee's gender, leave does not start before the joining date, the days do not
/// exceed the type's consecutive-day limit and the dates do not overlap submitted or approved leave of the same
/// employee. On submit: the overlap check again, the supporting document when the type requires one, and enough balance.
/// </summary>
internal sealed class LeaveApplicationRules(
    IRepository<Employee> employees,
    IRepository<LeaveType> types,
    IRepository<LeaveApplication> applications,
    LeaveBalanceService balances)
{
    public async Task<Result> CheckDetailsAsync(
        LeaveApplicationDetails details,
        decimal days,
        int excludedId,
        CancellationToken cancellationToken)
    {
        var employee = await employees.FindAsync(details.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Result.Failure(AppError.Validation(
                "The selected employee does not exist.", "employeeId", "Choose an employee."));
        }

        var type = await types.FindAsync(details.LeaveTypeId, cancellationToken);
        if (type is null || type.Status != RecordStatus.Active)
        {
            return Result.Failure(AppError.Validation(
                "Choose an active leave type.", "leaveTypeId", "Choose an active leave type."));
        }

        if (type.EligibleGender is { } gender && gender != employee.Gender)
        {
            return Result.Failure(AppError.Validation(
                $"{type.Name} is not available to this employee.", "leaveTypeId", "Not available for this employee."));
        }

        var violations = new Violations();
        if (details.StartDate < employee.JoiningDate)
        {
            violations.Add(
                "startDate",
                $"Must be on or after {Iso(employee.JoiningDate)}.",
                "Leave cannot start before the joining date.");
        }

        if (days > type.MaxConsecutiveDays)
        {
            violations.Add(
                "endDate",
                $"At most {type.MaxConsecutiveDays} days at a time.",
                $"{type.Name} is limited to {type.MaxConsecutiveDays} consecutive days.");
        }

        var clash = await FindOverlapAsync(details.EmployeeId, details.StartDate, details.EndDate, excludedId, cancellationToken);
        if (clash is not null)
        {
            violations.Add("startDate", $"Overlaps {clash.ApplicationNo}.", OverlapMessage(clash));
        }

        return violations.ToResult();
    }

    /// <summary>The rules checked when a draft is submitted. The status rule has been checked by the caller.</summary>
    public async Task<Result> CheckSubmitAsync(LeaveApplication application, CancellationToken cancellationToken)
    {
        var employee = await employees.FindAsync(application.EmployeeId, cancellationToken);
        var type = await types.FindAsync(application.LeaveTypeId, cancellationToken);
        if (employee is null || type is null)
        {
            return Result.Failure(AppError.Conflict("The employee or the leave type of this application no longer exists."));
        }

        var clash = await FindOverlapAsync(
            application.EmployeeId, application.StartDate, application.EndDate, application.Id, cancellationToken);
        if (clash is not null)
        {
            return Result.Failure(AppError.Validation(OverlapMessage(clash), "startDate", $"Overlaps {clash.ApplicationNo}."));
        }

        if (type.RequiresDocument && string.IsNullOrWhiteSpace(application.DocumentName))
        {
            return Result.Failure(AppError.Validation(
                $"{type.Name} needs a supporting document before it can be submitted.",
                "documentName",
                "A supporting document is required."));
        }

        // The balance of the year the leave starts in; the application is a draft, so it is not counted yet.
        var lines = await balances.ForEmployeeAsync(employee, application.StartDate.Year, cancellationToken);
        var available = lines.FirstOrDefault(line => line.LeaveTypeId == type.Id)?.Available ?? 0;
        return application.Days > available
            ? Result.Failure(AppError.Conflict(
                $"Insufficient {type.Name} balance: {Days(application.Days)} day(s) requested, {Days(available)} available."))
            : Result.Success();
    }

    /// <summary>The first submitted or approved application of the employee that shares a day with the range.</summary>
    private async Task<LeaveApplication?> FindOverlapAsync(
        int employeeId,
        DateOnly start,
        DateOnly end,
        int excludedId,
        CancellationToken cancellationToken)
    {
        var overlapping = await applications.ListAsync(
            application => application.Id != excludedId
                && application.EmployeeId == employeeId
                && (application.Status == LeaveStatus.Submitted || application.Status == LeaveStatus.Approved)
                && application.StartDate <= end
                && application.EndDate >= start,
            cancellationToken);
        return overlapping.Count > 0 ? overlapping[0] : null;
    }

    private static string OverlapMessage(LeaveApplication clash) =>
        $"These dates overlap {clash.ApplicationNo} ({Iso(clash.StartDate)} – {Iso(clash.EndDate)}).";

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>2 and 2.0 are shown as "2"; 0.5 as "0.5".</summary>
    private static string Days(decimal days) => days.ToString("0.#", CultureInfo.InvariantCulture);
}
