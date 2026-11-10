using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave;

/// <summary>Counts working days: weekends (configuration) and the holidays in att.Holiday are left out.</summary>
internal sealed class WorkingDayCalculator(IBusinessWeek week, IRepository<Holiday> holidays)
{
    public async Task<int> CountAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return 0;
        }

        var inRange = await holidays.ListAsync(holiday => holiday.Date >= from && holiday.Date <= to, cancellationToken);
        return WorkingDays.Count(from, to, week.WeekendDays, inRange.Select(holiday => holiday.Date).ToHashSet());
    }
}

/// <summary>
/// Works out an employee's leave balances for a year. Nothing is stored: entitlement comes from the policies and the
/// leave type (see <see cref="LeaveEntitlement"/>), taken and pending days from the leave applications. The entitlement
/// is measured as of today for the current year and as of 31 December for any other year.
/// </summary>
internal sealed class LeaveBalanceService(
    IRepository<LeaveType> types,
    IRepository<LeavePolicy> policies,
    IRepository<LeaveApplication> applications,
    IClock clock)
{
    public const int FirstYear = 2000;
    public const int LastYear = 2100;

    public async Task<IReadOnlyList<LeaveBalanceLine>> ForEmployeeAsync(
        Employee employee,
        int year,
        CancellationToken cancellationToken)
    {
        var employeeId = employee.Id;
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var allTypes = await types.ListAsync(Predicates.Always<LeaveType>(), cancellationToken);
        var allPolicies = await policies.ListAsync(policy => policy.Status == RecordStatus.Active, cancellationToken);
        var mine = await applications.ListAsync(
            application => application.EmployeeId == employeeId && application.StartDate >= from && application.StartDate <= to,
            cancellationToken);

        var today = clock.Today;
        var asOf = year == today.Year ? today : to;
        var profile = new EntitlementProfile(employee.JobGradeId, employee.EmploymentType, employee.JoiningDate);
        return LeaveBalances.Compute(
            allTypes,
            mine,
            employee.Gender,
            year,
            type => LeaveEntitlement.Resolve(type, allPolicies, profile, asOf));
    }
}
