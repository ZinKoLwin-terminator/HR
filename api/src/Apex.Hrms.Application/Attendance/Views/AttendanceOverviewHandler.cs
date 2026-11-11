using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Attendance.Views;

public interface IAttendanceOverviewHandler
{
    Task<Result<AttendanceOverviewDto>> HandleAsync(DateOnly overviewDay, int? branchId, CancellationToken cancellationToken);
}

/// <summary>
/// The day-level attendance dashboard. Records exist only for days someone punched, so the day is built from the
/// employees expected to work, not from the records alone:
/// <list type="bullet">
/// <item>Expected: employed on the date (joined, not yet exited), in the branch when one is given, on a working weekday of
/// their shift, and the date is not a holiday.</item>
/// <item>The status of an employee with a record is the record's status (a Weekend or Holiday record that nobody worked is not counted).</item>
/// <item>Without a record: On Leave when approved whole-day leave covers the date, otherwise Absent. A future date has no absences:
/// nothing is counted for it. For today, "absent" means "has not checked in yet".</item>
/// <item>The trend looks at the last 28 days up to the date and keeps the last 14 days with anyone scheduled.</item>
/// </list>
/// </summary>
internal sealed class AttendanceOverviewHandler(
    IRepository<Employee> employees,
    IAccessScope<Employee> scope,
    ICurrentUser user,
    IRepository<Department> departments,
    IRepository<Shift> shifts,
    IRepository<AttendanceRecord> records,
    IRepository<Holiday> holidays,
    IRepository<LeaveApplication> leave,
    IClock clock) : IAttendanceOverviewHandler
{
    public const int TrendDays = 14;
    public const int LookbackDays = 28;
    public const int ExceptionLimit = 8;

    private enum Outcome
    {
        None,
        Present,
        Late,
        HalfDay,
        Absent,
        OnLeave,
    }

    public async Task<Result<AttendanceOverviewDto>> HandleAsync(DateOnly overviewDay, int? branchId, CancellationToken cancellationToken)
    {
        var date = overviewDay;
        var from = date.AddDays(-(LookbackDays - 1));
        var predicate = scope.PredicateFor(user)
            .And(employee => employee.JoiningDate <= date && (employee.ExitDate == null || employee.ExitDate >= date));
        if (branchId is { } branch)
        {
            predicate = predicate.And(employee => employee.BranchId == branch);
        }

        var people = await employees.ListAsync(predicate, cancellationToken);
        var shiftList = await shifts.ListAsync(Predicates.Always<Shift>(), cancellationToken);
        var fallback = shiftList.Count == 0 ? null : shiftList[0];
        var shiftById = shiftList.ToDictionary(shift => shift.Id);
        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken))
            .ToDictionary(department => department.Id, department => department.Name);

        var ids = people.Select(employee => employee.Id).ToList();
        var recordList = ids.Count == 0
            ? []
            : await records.ListAsync(r => r.Date >= from && r.Date <= date && ids.Contains(r.EmployeeId), cancellationToken);
        var recordByDay = recordList.ToDictionary(record => (record.EmployeeId, record.Date));
        var holidayDates = (await holidays.ListAsync(h => h.Date >= from && h.Date <= date, cancellationToken))
            .Select(holiday => holiday.Date)
            .ToHashSet();
        var leaves = ids.Count == 0
            ? []
            : await leave.ListAsync(
                a => a.Status == LeaveStatus.Approved && !a.HalfDay && a.StartDate <= date && a.EndDate >= from && ids.Contains(a.EmployeeId),
                cancellationToken);

        var today = clock.Today;
        Outcome OutcomeOn(Employee employee, Shift shift, DateOnly day)
        {
            if (recordByDay.TryGetValue((employee.Id, day), out var record))
            {
                return record.Status switch
                {
                    AttendanceStatus.Present => Outcome.Present,
                    AttendanceStatus.Late => Outcome.Late,
                    AttendanceStatus.HalfDay => Outcome.HalfDay,
                    AttendanceStatus.Absent => Outcome.Absent,
                    AttendanceStatus.OnLeave => Outcome.OnLeave,
                    _ => Outcome.None,
                };
            }

            if (employee.JoiningDate > day || (employee.ExitDate is { } exit && exit < day)
                || holidayDates.Contains(day) || !shift.WorksOn(day))
            {
                return Outcome.None;
            }

            if (leaves.Any(a => a.EmployeeId == employee.Id && a.StartDate <= day && a.EndDate >= day))
            {
                return Outcome.OnLeave;
            }

            return day <= today ? Outcome.Absent : Outcome.None;
        }

        var scheduled = new List<(Employee Employee, Shift Shift)>();
        foreach (var employee in people)
        {
            var shift = shiftById.GetValueOrDefault(employee.ShiftId ?? -1) ?? fallback;
            if (shift is not null)
            {
                scheduled.Add((employee, shift));
            }
        }

        var day = scheduled
            .Select(entry => (entry.Employee, Outcome: OutcomeOn(entry.Employee, entry.Shift, date)))
            .Where(entry => entry.Outcome != Outcome.None)
            .ToList();
        int Count(Outcome outcome) => day.Count(entry => entry.Outcome == outcome);
        var present = Count(Outcome.Present);
        var late = Count(Outcome.Late);
        var halfDay = Count(Outcome.HalfDay);

        var summary = new AttendanceDaySummaryDto(
            date,
            day.Count,
            present,
            late,
            halfDay,
            Count(Outcome.Absent),
            Count(Outcome.OnLeave),
            day.Count == 0
                ? 0m
                : Math.Round((present + late + halfDay) / (decimal)day.Count * 1000m, MidpointRounding.AwayFromZero) / 10m);

        string DepartmentName(int id) => departmentNames.GetValueOrDefault(id) ?? $"Department #{id}";

        var byDepartment = day
            .GroupBy(entry => entry.Employee.DepartmentId)
            .Select(group => new DepartmentAttendanceDto(
                group.Key,
                DepartmentName(group.Key),
                group.Count(e => e.Outcome == Outcome.Present),
                group.Count(e => e.Outcome == Outcome.Late),
                group.Count(e => e.Outcome == Outcome.HalfDay),
                group.Count(e => e.Outcome == Outcome.Absent),
                group.Count(e => e.Outcome == Outcome.OnLeave),
                group.Count()))
            .OrderByDescending(row => row.Total)
            .ThenBy(row => row.Department, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AttendanceExceptionDto Exception(Employee employee, string detail) => new(
            employee.Id,
            employee.EmployeeCode,
            AttendanceActors.FullName(employee),
            departmentNames.GetValueOrDefault(employee.DepartmentId) ?? "-",
            detail);

        var lateList = day
            .Where(entry => entry.Outcome == Outcome.Late)
            .Select(entry => (entry.Employee, Record: recordByDay[(entry.Employee.Id, date)]))
            .OrderByDescending(entry => entry.Record.LateMinutes)
            .ThenBy(entry => AttendanceActors.FullName(entry.Employee), StringComparer.OrdinalIgnoreCase)
            .Take(ExceptionLimit)
            .Select(entry => Exception(
                entry.Employee,
                $"Checked in {(entry.Record.CheckIn is { } checkIn ? AttendanceRecord.Format(checkIn) : "-")} · {entry.Record.LateMinutes.ToString(CultureInfo.InvariantCulture)} min late"))
            .ToList();
        var absentList = day
            .Where(entry => entry.Outcome == Outcome.Absent)
            .OrderBy(entry => AttendanceActors.FullName(entry.Employee), StringComparer.OrdinalIgnoreCase)
            .Take(ExceptionLimit)
            .Select(entry => Exception(entry.Employee, "No check-in recorded"))
            .ToList();

        var trend = new List<AttendanceTrendDayDto>();
        for (var d = from; d <= date; d = d.AddDays(1))
        {
            var outcomes = scheduled.Select(entry => OutcomeOn(entry.Employee, entry.Shift, d)).Where(o => o != Outcome.None).ToList();
            if (outcomes.Count == 0)
            {
                continue;
            }

            trend.Add(new AttendanceTrendDayDto(
                d,
                outcomes.Count(o => o is Outcome.Present or Outcome.HalfDay),
                outcomes.Count(o => o == Outcome.Late),
                outcomes.Count(o => o == Outcome.Absent),
                outcomes.Count(o => o == Outcome.OnLeave)));
        }

        return Result.Success(new AttendanceOverviewDto(
            summary,
            byDepartment,
            lateList,
            absentList,
            [.. trend.TakeLast(TrendDays)]));
    }
}
