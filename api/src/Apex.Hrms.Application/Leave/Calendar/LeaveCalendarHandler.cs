using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.Calendar;

/// <summary>The query of <c>GET /api/leave/calendar</c>.</summary>
/// <param name="From">First day, inclusive.</param>
/// <param name="To">Last day, inclusive.</param>
/// <param name="DepartmentId">Only leave of employees of this department.</param>
/// <param name="LeaveTypeId">Only leave of this type.</param>
/// <param name="IncludePending">Also show submitted (not yet decided) leave. Default true.</param>
public sealed record LeaveCalendarRequest(
    DateOnly From,
    DateOnly To,
    int? DepartmentId = null,
    int? LeaveTypeId = null,
    bool IncludePending = true);

/// <summary>One person's leave on one day. Mirrors LeaveCalendarEntry in leave.models.ts.</summary>
public sealed record LeaveCalendarEntryDto(
    int ApplicationId,
    int EmployeeId,
    string EmployeeName,
    int LeaveTypeId,
    string LeaveTypeName,
    string Color,
    string Status,
    bool HalfDay);

/// <summary>One day of the range. Mirrors LeaveCalendarDay in leave.models.ts without <c>inMonth</c> (the client knows its month).</summary>
public sealed record LeaveCalendarDayDto(
    DateOnly Date,
    bool IsWeekend,
    string? Holiday,
    IReadOnlyList<LeaveCalendarEntryDto> Entries);

public sealed record LeaveCalendarDto(DateOnly From, DateOnly To, IReadOnlyList<LeaveCalendarDayDto> Days);

public interface ILeaveCalendarHandler
{
    Task<Result<LeaveCalendarDto>> HandleAsync(LeaveCalendarRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Approved leave (and submitted leave when asked) of every day in the range, with the weekends and holidays. Names
/// are those of the employees; no other personal data is read into the response.
/// </summary>
internal sealed class LeaveCalendarHandler(
    IRepository<LeaveApplication> applications,
    IRepository<LeaveType> types,
    IRepository<Employee> employees,
    IRepository<Holiday> holidays,
    IAccessScope<LeaveApplication> scope,
    ICurrentUser user,
    IBusinessWeek week) : ILeaveCalendarHandler
{
    public const int MaxRangeDays = 366;

    public async Task<Result<LeaveCalendarDto>> HandleAsync(LeaveCalendarRequest request, CancellationToken cancellationToken)
    {
        var invalid = Validate(request);
        if (invalid is not null)
        {
            return Result.Failure<LeaveCalendarDto>(invalid);
        }

        var from = request.From;
        var to = request.To;
        var predicate = scope.PredicateFor(user).And(RangeAndStatus(from, to, request.IncludePending));
        if (request.LeaveTypeId is { } typeId)
        {
            predicate = predicate.And(application => application.LeaveTypeId == typeId);
        }

        if (request.DepartmentId is { } departmentId)
        {
            var inDepartment = await employees.ListAsync(employee => employee.DepartmentId == departmentId, cancellationToken);
            var ids = inDepartment.Select(employee => employee.Id).ToList();
            predicate = predicate.And(application => ids.Contains(application.EmployeeId));
        }

        var relevant = await applications.ListAsync(predicate, cancellationToken);
        var employeeIds = relevant.Select(application => application.EmployeeId).Distinct().ToList();
        var people = employeeIds.Count == 0
            ? []
            : (await employees.ListAsync(employee => employeeIds.Contains(employee.Id), cancellationToken)).ToDictionary(employee => employee.Id);
        var leaveTypes = (await types.ListAsync(Predicates.Always<LeaveType>(), cancellationToken)).ToDictionary(type => type.Id);
        var holidayNames = (await holidays.ListAsync(holiday => holiday.Date >= from && holiday.Date <= to, cancellationToken))
            .GroupBy(holiday => holiday.Date)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var days = new List<LeaveCalendarDayDto>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var day = date;
            var entries = relevant
                .Where(application => day >= application.StartDate && day <= application.EndDate)
                .Select(application => ToEntry(application, people, leaveTypes))
                .OrderBy(entry => entry.EmployeeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.ApplicationId)
                .ToList();
            days.Add(new LeaveCalendarDayDto(
                date,
                week.WeekendDays.Contains(date.DayOfWeek),
                holidayNames.GetValueOrDefault(date),
                entries));
        }

        return Result.Success(new LeaveCalendarDto(from, to, days));
    }

    private static AppError? Validate(LeaveCalendarRequest request)
    {
        if (request.To < request.From)
        {
            return AppError.Validation("The end of the range must not be before its start.", "to", "Must be on or after the start.");
        }

        return request.To.DayNumber - request.From.DayNumber >= MaxRangeDays
            ? AppError.Validation(
                $"The calendar covers at most {MaxRangeDays} days at a time.",
                "to",
                $"Choose a range of at most {MaxRangeDays} days.")
            : null;
    }

    private static System.Linq.Expressions.Expression<Func<LeaveApplication, bool>> RangeAndStatus(
        DateOnly from,
        DateOnly to,
        bool includePending) =>
        includePending
            ? application => application.StartDate <= to && application.EndDate >= from
                && (application.Status == LeaveStatus.Approved || application.Status == LeaveStatus.Submitted)
            : application => application.StartDate <= to && application.EndDate >= from
                && application.Status == LeaveStatus.Approved;

    private static LeaveCalendarEntryDto ToEntry(
        LeaveApplication application,
        Dictionary<int, Employee> people,
        Dictionary<int, LeaveType> leaveTypes)
    {
        var name = people.TryGetValue(application.EmployeeId, out var employee)
            ? $"{employee.FirstName} {employee.LastName}"
            : $"#{application.EmployeeId}";
        var type = leaveTypes.GetValueOrDefault(application.LeaveTypeId);
        return new LeaveCalendarEntryDto(
            application.Id,
            application.EmployeeId,
            name,
            application.LeaveTypeId,
            type?.Name ?? "Leave",
            type?.Color ?? "#6b7280",
            LeaveStatusText.ToText(application.Status),
            application.HalfDay);
    }
}
