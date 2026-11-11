using System.Globalization;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Dashboard;

/// <summary>
/// The dashboard figures, worked out from plain rows so each one can be checked from known fixed data. Ported from the
/// frontend's dashboard mock service: the rules, the windows and the wording are the same. Everything takes the
/// business-time-zone "today" as a parameter.
/// </summary>
public static class DashboardCalculations
{
    public const int PendingLeaveLimit = 5;
    public const int UpcomingWindowDays = 30;
    public const int UpcomingEventLimit = 10;
    public const int GrowthMonths = 12;
    public const int RecentActivityLimit = 6;

    private static readonly string[] FunnelStages = ["Applied", "Screening", "Interview", "Selected"];

    /// <summary>
    /// Headcount counts current employees (Active, Probation, On Leave, Suspended). "New this month" is the joiners of the
    /// last 30 days (as the mock does), exits are this calendar month so far.
    /// </summary>
    public static HeadcountSummaryDto Headcount(IReadOnlyList<EmployeeFigureRow> employees, int onLeaveToday, DateOnly today)
    {
        var current = employees.Where(e => e.IsCurrent).ToList();
        var monthStart = StartOfMonth(today);
        var recentJoinFrom = today.AddDays(-30);
        return new HeadcountSummaryDto(
            current.Count,
            current.Count(e => e.Status == EmploymentStatus.Active),
            current.Count(e => e.Status == EmploymentStatus.Probation),
            employees.Count(e => e.JoiningDate >= recentJoinFrom && e.JoiningDate <= today),
            onLeaveToday,
            employees.Count(e => e.ExitDate is { } exit && exit >= monthStart && exit <= today));
    }

    /// <summary>Current employees per department, largest first (ties keep the department order of first appearance).</summary>
    public static IReadOnlyList<CountByLabelDto> DepartmentDistribution(
        IReadOnlyList<EmployeeFigureRow> employees,
        IReadOnlyDictionary<int, string> departmentNames) =>
        [.. employees
            .Where(e => e.IsCurrent)
            .GroupBy(e => e.DepartmentId)
            .Select(group => new CountByLabelDto(
                departmentNames.GetValueOrDefault(group.Key) ?? $"Department #{group.Key}",
                group.Count()))
            .OrderByDescending(item => item.Count)];

    /// <summary>The last 12 months up to this one: headcount at month end, joiners and exits in the month.</summary>
    public static IReadOnlyList<HeadcountGrowthPointDto> HeadcountGrowth(IReadOnlyList<EmployeeFigureRow> employees, DateOnly today)
    {
        var points = new List<HeadcountGrowthPointDto>();
        for (var offset = -(GrowthMonths - 1); offset <= 0; offset++)
        {
            var from = StartOfMonth(today).AddMonths(offset);
            var to = from.AddMonths(1).AddDays(-1);
            points.Add(new HeadcountGrowthPointDto(
                from.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                employees.Count(e => e.JoiningDate <= to && (e.ExitDate is null || e.ExitDate > to)),
                employees.Count(e => e.JoiningDate >= from && e.JoiningDate <= to),
                employees.Count(e => e.ExitDate is { } exit && exit >= from && exit <= to)));
        }

        return points;
    }

    /// <summary>The submitted applications, earliest start first; the dashboard shows the first five and counts them all.</summary>
    public static IReadOnlyList<PendingLeaveItemDto> PendingLeaves(
        IReadOnlyList<LeaveApplication> submitted,
        IReadOnlyDictionary<int, string> employeeNames,
        IReadOnlyDictionary<int, string> leaveTypeNames) =>
        [.. submitted
            .OrderBy(l => l.StartDate)
            .Select(l => new PendingLeaveItemDto(
                l.Id,
                employeeNames.GetValueOrDefault(l.EmployeeId) ?? $"Employee #{l.EmployeeId}",
                leaveTypeNames.GetValueOrDefault(l.LeaveTypeId) ?? "Leave",
                l.StartDate,
                l.EndDate,
                l.Days,
                l.AppliedOn))];

    public static RecruitmentSummaryDto Recruitment(RecruitmentRows rows)
    {
        int Stage(string name) => rows.StageCounts.GetValueOrDefault(name);
        return new RecruitmentSummaryDto(
            rows.OpenJobs,
            rows.OpenPositions,
            rows.StageCounts.Where(pair => pair.Key is not ("Rejected" or "Selected")).Sum(pair => pair.Value),
            rows.UpcomingInterviews,
            [.. FunnelStages.Select(stage => new CountByLabelDto(stage, Stage(stage)))]);
    }

    /// <summary>
    /// The latest processed run (a draft has no figures yet) with the change of the net total against the one before it.
    /// <paramref name="runsNewestFirst"/> holds the non-draft runs, newest period first. Null when there is none.
    /// </summary>
    public static PayrollSummaryDto? Payroll(IReadOnlyList<PayrollRunRow> runsNewestFirst)
    {
        if (runsNewestFirst.Count == 0)
        {
            return null;
        }

        var latest = runsNewestFirst[0];
        var previous = runsNewestFirst.Count > 1 ? runsNewestFirst[1] : null;
        var change = previous is { TotalNet: not 0m }
            ? (latest.TotalNet - previous.TotalNet) / previous.TotalNet * 100m
            : 0m;
        return new PayrollSummaryDto(
            latest.PeriodName,
            latest.Status,
            latest.PayDate,
            latest.EmployeeCount,
            latest.TotalGross,
            latest.TotalDeductions,
            latest.TotalNet,
            Math.Round(change, 1, MidpointRounding.AwayFromZero));
    }

    public static TodayAttendanceSummaryDto Attendance(AttendanceDaySummaryDto summary) => new(
        summary.Date,
        summary.Scheduled,
        summary.Present,
        summary.Late,
        summary.Absent,
        summary.OnLeave,
        summary.HalfDay,
        summary.AttendanceRate);

    public static IReadOnlyList<AttendanceTrendPointDto> AttendanceTrend(IReadOnlyList<AttendanceTrendDayDto> trend) =>
        [.. trend.Select(day => new AttendanceTrendPointDto(day.Date, day.Present, day.Late, day.Absent, day.OnLeave))];

    /// <summary>
    /// The next 30 days: holidays, birthdays, work anniversaries (a year or more), probation reviews of employees on
    /// probation, and approved leave that starts after today. Earliest first, ten at most.
    /// </summary>
    public static IReadOnlyList<UpcomingEventDto> UpcomingEvents(
        DateOnly today,
        IReadOnlyList<EmployeeFigureRow> employees,
        IReadOnlyList<Holiday> holidays,
        IReadOnlyList<LeaveApplication> approvedLeave,
        IReadOnlyDictionary<int, string> employeeNames,
        IReadOnlyDictionary<int, string> leaveTypeNames)
    {
        var until = today.AddDays(UpcomingWindowDays);
        bool InWindow(DateOnly date) => date >= today && date <= until;
        var events = new List<UpcomingEventDto>();

        foreach (var holiday in holidays)
        {
            if (InWindow(holiday.Date))
            {
                events.Add(new UpcomingEventDto(holiday.Date, holiday.Name, $"{holiday.Type} holiday", "Holiday"));
            }
        }

        foreach (var employee in employees.Where(e => e.IsCurrent))
        {
            var birthday = NextOccurrence(employee.DateOfBirth, today);
            if (InWindow(birthday))
            {
                events.Add(new UpcomingEventDto(birthday, employee.FullName, "Birthday", "Birthday"));
            }

            var anniversary = NextOccurrence(employee.JoiningDate, today);
            var years = anniversary.Year - employee.JoiningDate.Year;
            if (years >= 1 && InWindow(anniversary))
            {
                events.Add(new UpcomingEventDto(
                    anniversary,
                    employee.FullName,
                    $"{years.ToString(CultureInfo.InvariantCulture)} year{(years > 1 ? "s" : string.Empty)} with the company",
                    "Work Anniversary"));
            }

            if (employee.Status == EmploymentStatus.Probation && employee.ProbationEndDate is { } probationEnd && InWindow(probationEnd))
            {
                events.Add(new UpcomingEventDto(probationEnd, employee.FullName, "Probation review due", "Probation"));
            }
        }

        foreach (var leave in approvedLeave)
        {
            if (leave.Status == LeaveStatus.Approved && leave.StartDate > today && InWindow(leave.StartDate))
            {
                var name = employeeNames.GetValueOrDefault(leave.EmployeeId) ?? $"Employee #{leave.EmployeeId}";
                var type = leaveTypeNames.GetValueOrDefault(leave.LeaveTypeId) ?? "Leave";
                events.Add(new UpcomingEventDto(
                    leave.StartDate,
                    name,
                    $"{type} · {leave.Days.ToString("0.#", CultureInfo.InvariantCulture)} day(s)",
                    "Leave"));
            }
        }

        // OrderBy is stable, so events of one day keep the order they were added in (holidays first).
        return [.. events.OrderBy(e => e.Date).Take(UpcomingEventLimit)];
    }

    /// <summary>The next time a recurring month and day falls on or after today (this year or next). 29 February becomes 28 February in a common year.</summary>
    public static DateOnly NextOccurrence(DateOnly date, DateOnly today)
    {
        var thisYear = InYear(date, today.Year);
        return thisYear >= today ? thisYear : InYear(date, today.Year + 1);
    }

    public static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly InYear(DateOnly date, int year) =>
        new(year, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month)));
}
