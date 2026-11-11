using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Attendance.Views;

public interface IWorkScheduleHandler
{
    Task<Result<WorkScheduleDto>> HandleAsync(WorkScheduleRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The weekly roster: for each current employee (department and branch filters optional) and each of the seven days from
/// the week start, a holiday, approved leave, a shift (its start and end) or a day off, in that order of precedence.
/// An employee without a shift of their own is shown on the first shift.
/// </summary>
internal sealed class WorkScheduleHandler(
    IRepository<Employee> employees,
    IAccessScope<Employee> scope,
    ICurrentUser user,
    IRepository<Department> departments,
    IRepository<Shift> shifts,
    IRepository<Holiday> holidays,
    IRepository<LeaveApplication> leave) : IWorkScheduleHandler
{
    private const int WeekDays = 7;

    public async Task<Result<WorkScheduleDto>> HandleAsync(WorkScheduleRequest request, CancellationToken cancellationToken)
    {
        var weekStart = request.WeekStart;
        var weekEnd = weekStart.AddDays(WeekDays - 1);
        var days = Enumerable.Range(0, WeekDays).Select(weekStart.AddDays).ToList();

        var predicate = scope.PredicateFor(user)
            .And(employee => employee.Status != EmploymentStatus.Resigned && employee.Status != EmploymentStatus.Terminated);
        if (request.DepartmentId is { } departmentId)
        {
            predicate = predicate.And(employee => employee.DepartmentId == departmentId);
        }

        if (request.BranchId is { } branchId)
        {
            predicate = predicate.And(employee => employee.BranchId == branchId);
        }

        var people = await employees.ListAsync(predicate, cancellationToken);
        var shiftList = await shifts.ListAsync(Predicates.Always<Shift>(), cancellationToken);
        var fallback = shiftList.Count == 0 ? null : shiftList[0];
        var shiftById = shiftList.ToDictionary(shift => shift.Id);
        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken))
            .ToDictionary(department => department.Id, department => department.Name);
        var holidayNames = (await holidays.ListAsync(h => h.Date >= weekStart && h.Date <= weekEnd, cancellationToken))
            .GroupBy(holiday => holiday.Date)
            .ToDictionary(group => group.Key, group => group.First().Name);
        var ids = people.Select(employee => employee.Id).ToList();
        var leaves = ids.Count == 0
            ? []
            : await leave.ListAsync(
                a => a.Status == LeaveStatus.Approved && a.StartDate <= weekEnd && a.EndDate >= weekStart && ids.Contains(a.EmployeeId),
                cancellationToken);

        var rows = new List<ScheduleRowDto>();
        foreach (var employee in people)
        {
            var shift = shiftById.GetValueOrDefault(employee.ShiftId ?? -1) ?? fallback;
            if (shift is null)
            {
                continue;
            }

            var cells = days.Select(date => CellFor(employee, shift, date, holidayNames, leaves)).ToList();
            rows.Add(new ScheduleRowDto(
                employee.Id,
                employee.EmployeeCode,
                AttendanceActors.FullName(employee),
                departmentNames.GetValueOrDefault(employee.DepartmentId) ?? "-",
                shift.Name,
                shift.Color,
                cells));
        }

        rows.Sort((a, b) =>
        {
            var byDepartment = string.Compare(a.Department, b.Department, StringComparison.OrdinalIgnoreCase);
            return byDepartment != 0
                ? byDepartment
                : string.Compare(a.EmployeeName, b.EmployeeName, StringComparison.OrdinalIgnoreCase);
        });
        return Result.Success(new WorkScheduleDto(weekStart, days, rows));
    }

    private static ScheduleCellDto CellFor(
        Employee employee,
        Shift shift,
        DateOnly date,
        Dictionary<DateOnly, string> holidayNames,
        IReadOnlyList<LeaveApplication> leaves)
    {
        if (holidayNames.TryGetValue(date, out var holiday))
        {
            return new ScheduleCellDto(date, "Holiday", holiday);
        }

        if (leaves.Any(a => a.EmployeeId == employee.Id && date >= a.StartDate && date <= a.EndDate))
        {
            return new ScheduleCellDto(date, "Leave", "On leave");
        }

        return shift.WorksOn(date)
            ? new ScheduleCellDto(
                date,
                "Shift",
                string.Create(CultureInfo.InvariantCulture, $"{AttendanceRecord.Format(shift.StartTime)}–{AttendanceRecord.Format(shift.EndTime)}"))
            : new ScheduleCellDto(date, "Off", "Off");
    }
}
