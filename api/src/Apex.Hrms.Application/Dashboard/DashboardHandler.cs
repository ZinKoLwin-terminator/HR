using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Dashboard;

/// <summary>
/// Read port for the dashboard figures that need slim rows or tables with no module of their own yet (recruitment and
/// payroll). Read-only: the dashboard changes nothing.
/// </summary>
public interface IDashboardSource
{
    /// <summary>Every employee in the caller's scope, reduced to the columns the figures use.</summary>
    Task<IReadOnlyList<EmployeeFigureRow>> EmployeesAsync(Expression<Func<Employee, bool>> scope, CancellationToken cancellationToken);

    /// <summary>Open openings and their vacancies, applications by stage, and the interviews still scheduled from <paramref name="today"/> on.</summary>
    Task<RecruitmentRows> RecruitmentAsync(DateOnly today, CancellationToken cancellationToken);

    /// <summary>The newest <paramref name="count"/> runs that have been processed (not drafts), newest period first.</summary>
    Task<IReadOnlyList<PayrollRunRow>> ProcessedPayrollRunsAsync(int count, CancellationToken cancellationToken);
}

public interface IDashboardHandler
{
    Task<Result<DashboardSummaryDto>> HandleAsync(CancellationToken cancellationToken);
}

/// <summary>
/// GET /api/dashboard/summary. Employees and leave applications are read inside the caller's access scope; "today" is the
/// date in the business time zone (<see cref="IClock.Today"/>). The attendance figures come from the attendance overview
/// (the same rules as the attendance screen); when today is a day off the figures are those of the latest working day.
/// </summary>
internal sealed class DashboardHandler(
    IDashboardSource source,
    IAttendanceOverviewHandler attendance,
    IActivityLogStore activity,
    IRepository<Department> departments,
    IRepository<Holiday> holidays,
    IRepository<LeaveApplication> leave,
    IRepository<LeaveType> leaveTypes,
    IAccessScope<Employee> employeeScope,
    IAccessScope<LeaveApplication> leaveScope,
    ICurrentUser user,
    IClock clock) : IDashboardHandler
{
    public async Task<Result<DashboardSummaryDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var until = today.AddDays(DashboardCalculations.UpcomingWindowDays);

        var overview = await attendance.HandleAsync(today, null, cancellationToken);
        if (overview.IsFailure)
        {
            return Result.Failure<DashboardSummaryDto>(overview.Error);
        }

        var summary = overview.Value.Summary;
        if (summary.Scheduled == 0 && overview.Value.Trend.Count > 0)
        {
            // A day off: show the latest working day, like the mock does.
            var latest = overview.Value.Trend[^1].Date;
            var fallback = await attendance.HandleAsync(latest, null, cancellationToken);
            if (fallback.IsFailure)
            {
                return Result.Failure<DashboardSummaryDto>(fallback.Error);
            }

            summary = fallback.Value.Summary;
        }

        var employees = await source.EmployeesAsync(employeeScope.PredicateFor(user), cancellationToken);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken))
            .ToDictionary(d => d.Id, d => d.Name);
        var leaveTypeNames = (await leaveTypes.ListAsync(Predicates.Always<LeaveType>(), cancellationToken))
            .ToDictionary(t => t.Id, t => t.Name);

        var leaveRows = await leave.ListAsync(
            leaveScope.PredicateFor(user).And(application =>
                application.Status == LeaveStatus.Submitted
                || (application.Status == LeaveStatus.Approved && application.StartDate > today && application.StartDate <= until)),
            cancellationToken);
        var submitted = leaveRows.Where(application => application.Status == LeaveStatus.Submitted).ToList();
        var approved = leaveRows.Where(application => application.Status == LeaveStatus.Approved).ToList();

        var holidayRows = await holidays.ListAsync(holiday => holiday.Date >= today && holiday.Date <= until, cancellationToken);
        var recruitment = await source.RecruitmentAsync(today, cancellationToken);
        var payrollRuns = await source.ProcessedPayrollRunsAsync(2, cancellationToken);
        var recent = await activity.RecentAsync(DashboardCalculations.RecentActivityLimit, cancellationToken);

        return Result.Success(new DashboardSummaryDto(
            clock.UtcNow,
            DashboardCalculations.Headcount(employees, summary.OnLeave, today),
            DashboardCalculations.Attendance(summary),
            submitted.Count,
            [.. DashboardCalculations.PendingLeaves(submitted, employeeNames, leaveTypeNames).Take(DashboardCalculations.PendingLeaveLimit)],
            DashboardCalculations.Payroll(payrollRuns),
            DashboardCalculations.Recruitment(recruitment),
            DashboardCalculations.DepartmentDistribution(employees, departmentNames),
            DashboardCalculations.AttendanceTrend(overview.Value.Trend),
            DashboardCalculations.HeadcountGrowth(employees, today),
            [.. recent.Select(ActivityEntryDto.From)],
            DashboardCalculations.UpcomingEvents(today, employees, holidayRows, approved, employeeNames, leaveTypeNames)));
    }
}
