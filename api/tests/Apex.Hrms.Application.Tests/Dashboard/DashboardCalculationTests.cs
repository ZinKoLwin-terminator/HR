using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Application.Dashboard;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Tests.Dashboard;

/// <summary>
/// Each dashboard figure from known fixed rows. Today is Monday 2026-11-09. Everything is fictional.
/// Employees: E1 active, E2 on probation (joined 20 Oct), E3 on leave (anniversary 15 Nov), E4 resigned this month,
/// E5 terminated last year, E6 suspended (joined 2 Nov). Departments 1 and 2 are named, 3 is not.
/// </summary>
public sealed class DashboardCalculationTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static DateOnly D(int year, int month, int day) => new(year, month, day);

    private static EmployeeFigureRow Row(
        int id,
        int department,
        EmploymentStatus status,
        DateOnly joined,
        DateOnly? exit = null,
        DateOnly? born = null,
        DateOnly? probationEnd = null,
        string first = "Test",
        string last = "Person") =>
        new(id, department, status, joined, exit, born ?? D(1990, 1, 1), probationEnd, first, last);

    private static readonly EmployeeFigureRow[] Employees =
    [
        Row(1, 1, EmploymentStatus.Active, D(2024, 3, 1), born: D(1990, 11, 20), first: "Mia", last: "Hartley"),
        Row(2, 1, EmploymentStatus.Probation, D(2026, 10, 20), born: D(1996, 2, 29), probationEnd: D(2026, 11, 25), first: "Noel", last: "Pryce"),
        Row(3, 2, EmploymentStatus.OnLeave, D(2020, 11, 15), first: "Sana", last: "Okoye"),
        Row(4, 2, EmploymentStatus.Resigned, D(2022, 1, 10), exit: D(2026, 11, 3), first: "Ravi", last: "Menon"),
        Row(5, 2, EmploymentStatus.Terminated, D(2019, 5, 1), exit: D(2025, 12, 15), first: "Lena", last: "Voss"),
        Row(6, 3, EmploymentStatus.Suspended, D(2026, 11, 2), first: "Omar", last: "Reyes"),
    ];

    private static readonly Dictionary<int, string> DepartmentNames = new() { [1] = "Engineering", [2] = "Finance" };

    private static readonly Dictionary<int, string> EmployeeNames = Employees.ToDictionary(e => e.Id, e => e.FullName);

    private static readonly Dictionary<int, string> LeaveTypeNames = new() { [1] = "Annual Leave" };

    // ---- Headcount ----

    [Fact]
    public void Headcount_counts_current_employees_by_status()
    {
        var headcount = DashboardCalculations.Headcount(Employees, onLeaveToday: 3, Today);

        Assert.Equal(4, headcount.Total);
        Assert.Equal(1, headcount.Active);
        Assert.Equal(1, headcount.Probation);
        Assert.Equal(3, headcount.OnLeaveToday);
    }

    [Fact]
    public void New_this_month_is_the_joiners_of_the_last_thirty_days_and_exits_are_this_calendar_month()
    {
        var headcount = DashboardCalculations.Headcount(Employees, 0, Today);

        Assert.Equal(2, headcount.NewThisMonth);
        Assert.Equal(1, headcount.ExitsThisMonth);
    }

    [Fact]
    public void The_thirty_day_window_includes_its_first_day_and_today_and_excludes_tomorrow()
    {
        EmployeeFigureRow[] joiners =
        [
            Row(1, 1, EmploymentStatus.Active, Today.AddDays(-30)),
            Row(2, 1, EmploymentStatus.Active, Today.AddDays(-31)),
            Row(3, 1, EmploymentStatus.Active, Today),
            Row(4, 1, EmploymentStatus.Active, Today.AddDays(1)),
        ];

        Assert.Equal(2, DashboardCalculations.Headcount(joiners, 0, Today).NewThisMonth);
    }

    [Fact]
    public void An_exit_today_counts_and_an_exit_before_the_first_of_the_month_does_not()
    {
        EmployeeFigureRow[] leavers =
        [
            Row(1, 1, EmploymentStatus.Resigned, D(2020, 1, 1), exit: Today),
            Row(2, 1, EmploymentStatus.Resigned, D(2020, 1, 1), exit: D(2026, 10, 31)),
        ];

        Assert.Equal(1, DashboardCalculations.Headcount(leavers, 0, Today).ExitsThisMonth);
    }

    [Fact]
    public void An_empty_workforce_gives_zeros()
    {
        var headcount = DashboardCalculations.Headcount([], 0, Today);

        Assert.Equal(new HeadcountSummaryDto(0, 0, 0, 0, 0, 0), headcount);
    }

    // ---- Department distribution ----

    [Fact]
    public void The_department_distribution_counts_current_employees_largest_first_and_names_unknown_departments()
    {
        var distribution = DashboardCalculations.DepartmentDistribution(Employees, DepartmentNames);

        Assert.Equal(
            [new CountByLabelDto("Engineering", 2), new CountByLabelDto("Finance", 1), new CountByLabelDto("Department #3", 1)],
            distribution);
    }

    // ---- Headcount growth ----

    [Fact]
    public void Headcount_growth_covers_the_last_twelve_months_ending_this_one()
    {
        var growth = DashboardCalculations.HeadcountGrowth(Employees, Today);

        Assert.Equal(12, growth.Count);
        Assert.Equal("2025-12", growth[0].Month);
        Assert.Equal("2026-11", growth[^1].Month);
    }

    [Fact]
    public void Each_growth_point_has_the_headcount_at_month_end_and_the_joiners_and_exits_of_the_month()
    {
        var growth = DashboardCalculations.HeadcountGrowth(Employees, Today).ToDictionary(point => point.Month);

        // December 2025: E1, E3 and E4 are employed at month end; E5 left on 15 December.
        Assert.Equal(new HeadcountGrowthPointDto("2025-12", 3, 0, 1), growth["2025-12"]);
        // October 2026: E2 joined; E4 is still employed at the end of October.
        Assert.Equal(new HeadcountGrowthPointDto("2026-10", 4, 1, 0), growth["2026-10"]);
        // November 2026: E6 joined, E4 left on the 3rd.
        Assert.Equal(new HeadcountGrowthPointDto("2026-11", 4, 1, 1), growth["2026-11"]);
    }

    [Fact]
    public void An_employee_who_leaves_on_the_last_day_of_a_month_is_not_counted_at_that_month_end()
    {
        EmployeeFigureRow[] one = [Row(1, 1, EmploymentStatus.Resigned, D(2020, 1, 1), exit: D(2026, 10, 31))];

        var growth = DashboardCalculations.HeadcountGrowth(one, Today).ToDictionary(point => point.Month);

        Assert.Equal(0, growth["2026-10"].Headcount);
        Assert.Equal(1, growth["2026-09"].Headcount);
    }

    // ---- Pending leave ----

    private static LeaveApplication Leave(int id, int employeeId, DateOnly start, DateOnly? end = null, bool approved = false, decimal days = 1m, int typeId = 1)
    {
        var details = new LeaveApplicationDetails(employeeId, typeId, start, end ?? start, false, "Family matter", null);
        var application = LeaveApplication.Create(details, days, $"LV-2026-{id:0000}", D(2026, 11, 2)).Value;
        typeof(Apex.Hrms.Domain.Common.Entity).GetProperty(nameof(Apex.Hrms.Domain.Common.Entity.Id))!.SetValue(application, id);
        Assert.True(application.Submit(D(2026, 11, 3)).IsSuccess);
        if (approved)
        {
            Assert.True(application.Decide(LeaveDecision.Approve, new LeaveActor(99, null, "Approver"), null, D(2026, 11, 4)).IsSuccess);
        }

        return application;
    }

    [Fact]
    public void Pending_leave_is_listed_by_earliest_start_with_names_and_falls_back_for_unknown_ids()
    {
        var pending = DashboardCalculations.PendingLeaves(
            [Leave(1, 3, D(2026, 12, 1)), Leave(2, 1, D(2026, 11, 16), D(2026, 11, 17), days: 2m), Leave(3, 77, D(2026, 11, 20), typeId: 42)],
            EmployeeNames,
            LeaveTypeNames);

        Assert.Equal([2, 3, 1], pending.Select(p => p.Id));
        Assert.Equal(
            new PendingLeaveItemDto(2, "Mia Hartley", "Annual Leave", D(2026, 11, 16), D(2026, 11, 17), 2m, D(2026, 11, 3)),
            pending[0]);
        Assert.Equal("Employee #77", pending[1].EmployeeName);
        Assert.Equal("Leave", pending[1].LeaveTypeName);
    }

    // ---- Recruitment ----

    [Fact]
    public void Recruitment_counts_open_jobs_active_candidates_and_the_funnel()
    {
        var rows = new RecruitmentRows(
            3,
            7,
            new Dictionary<string, int> { ["Applied"] = 5, ["Screening"] = 3, ["Interview"] = 2, ["Selected"] = 1, ["Rejected"] = 4 },
            2);

        var summary = DashboardCalculations.Recruitment(rows);

        Assert.Equal(3, summary.OpenJobs);
        Assert.Equal(7, summary.OpenPositions);
        Assert.Equal(10, summary.ActiveCandidates);
        Assert.Equal(2, summary.UpcomingInterviews);
        Assert.Equal(
            [new CountByLabelDto("Applied", 5), new CountByLabelDto("Screening", 3), new CountByLabelDto("Interview", 2), new CountByLabelDto("Selected", 1)],
            summary.Funnel);
    }

    [Fact]
    public void A_stage_with_no_applications_counts_zero()
    {
        var summary = DashboardCalculations.Recruitment(new RecruitmentRows(0, 0, new Dictionary<string, int>(), 0));

        Assert.Equal(0, summary.ActiveCandidates);
        Assert.Equal(["Applied", "Screening", "Interview", "Selected"], summary.Funnel.Select(f => f.Label));
        Assert.All(summary.Funnel, f => Assert.Equal(0, f.Count));
    }

    // ---- Payroll (demonstration figures) ----

    private static PayrollRunRow Run(string period, decimal gross, decimal deductions, string status = "Approved") =>
        new(period, status, D(2026, 11, 28), 40, gross, deductions, gross - deductions);

    [Fact]
    public void Payroll_is_the_latest_run_with_the_net_change_against_the_one_before()
    {
        var summary = DashboardCalculations.Payroll([Run("November 2026", 1_300_000m, 200_000m), Run("October 2026", 1_150_000m, 150_000m)]);

        Assert.NotNull(summary);
        Assert.Equal("November 2026", summary.PeriodName);
        Assert.Equal(1_300_000m, summary.TotalGross);
        Assert.Equal(200_000m, summary.TotalDeductions);
        Assert.Equal(1_100_000m, summary.TotalNet);
        Assert.Equal(10.0m, summary.NetChangePercent);
        Assert.Equal(40, summary.EmployeeCount);
    }

    [Fact]
    public void The_net_change_is_rounded_to_one_decimal()
    {
        var summary = DashboardCalculations.Payroll([Run("B", 1_334_567m, 100_000m), Run("A", 1_100_000m, 100_000m)]);

        Assert.Equal(23.5m, summary!.NetChangePercent);
    }

    [Fact]
    public void A_single_run_or_a_previous_run_without_net_has_no_change()
    {
        Assert.Equal(0m, DashboardCalculations.Payroll([Run("A", 1_000_000m, 100_000m)])!.NetChangePercent);
        Assert.Equal(0m, DashboardCalculations.Payroll([Run("B", 1_000_000m, 100_000m), Run("A", 0m, 0m)])!.NetChangePercent);
    }

    [Fact]
    public void Without_a_processed_run_there_is_no_payroll_summary()
    {
        Assert.Null(DashboardCalculations.Payroll([]));
    }

    // ---- Attendance ----

    [Fact]
    public void Attendance_figures_map_the_overview_day_and_trend()
    {
        var day = DashboardCalculations.Attendance(new AttendanceDaySummaryDto(Today, 20, 12, 3, 1, 2, 2, 80.0m));
        var trend = DashboardCalculations.AttendanceTrend([new AttendanceTrendDayDto(Today, 12, 3, 2, 2)]);

        Assert.Equal(new TodayAttendanceSummaryDto(Today, 20, 12, 3, 2, 2, 1, 80.0m), day);
        Assert.Equal(new AttendanceTrendPointDto(Today, 12, 3, 2, 2), Assert.Single(trend));
    }

    // ---- Upcoming events ----

    private static Holiday HolidayOn(DateOnly date, string name = "Thadingyut", string type = "Public") =>
        Holiday.Create(new HolidayDetails(date, name, type)).Value;

    private static IReadOnlyList<UpcomingEventDto> Events(
        IReadOnlyList<Holiday>? holidays = null,
        IReadOnlyList<LeaveApplication>? leave = null,
        IReadOnlyList<EmployeeFigureRow>? employees = null) =>
        DashboardCalculations.UpcomingEvents(Today, employees ?? Employees, holidays ?? [], leave ?? [], EmployeeNames, LeaveTypeNames);

    [Fact]
    public void Upcoming_events_cover_holidays_birthdays_anniversaries_probation_and_approved_leave_in_date_order()
    {
        var events = Events(
            [HolidayOn(D(2026, 11, 13))],
            [Leave(1, 1, D(2026, 11, 23), D(2026, 11, 24), approved: true, days: 2m)]);

        Assert.Equal(
            [
                new UpcomingEventDto(D(2026, 11, 13), "Thadingyut", "Public holiday", "Holiday"),
                new UpcomingEventDto(D(2026, 11, 15), "Sana Okoye", "6 years with the company", "Work Anniversary"),
                new UpcomingEventDto(D(2026, 11, 20), "Mia Hartley", "Birthday", "Birthday"),
                new UpcomingEventDto(D(2026, 11, 23), "Mia Hartley", "Annual Leave · 2 day(s)", "Leave"),
                new UpcomingEventDto(D(2026, 11, 25), "Noel Pryce", "Probation review due", "Probation"),
            ],
            events);
    }

    [Fact]
    public void Events_outside_the_thirty_day_window_and_in_the_past_are_left_out()
    {
        var events = Events(
            [HolidayOn(D(2026, 11, 8), "Yesterday"), HolidayOn(D(2026, 11, 9), "Today"), HolidayOn(D(2026, 12, 9), "Last day"), HolidayOn(D(2026, 12, 10), "Too late")],
            employees: []);

        Assert.Equal(["Today", "Last day"], events.Select(e => e.Title));
    }

    [Fact]
    public void Leave_that_is_not_approved_or_starts_today_or_later_than_the_window_is_not_an_event()
    {
        var events = Events(
            leave:
            [
                Leave(1, 1, D(2026, 11, 20)),
                Leave(2, 1, Today, approved: true),
                Leave(3, 1, D(2026, 12, 20), approved: true),
                Leave(4, 77, D(2026, 11, 30), approved: true, typeId: 42),
            ],
            employees: []);

        var only = Assert.Single(events);
        Assert.Equal(new UpcomingEventDto(D(2026, 11, 30), "Employee #77", "Leave · 1 day(s)", "Leave"), only);
    }

    [Fact]
    public void Employees_who_have_left_have_no_birthday_or_anniversary_and_a_first_year_has_no_anniversary()
    {
        EmployeeFigureRow[] people =
        [
            Row(1, 1, EmploymentStatus.Resigned, D(2020, 11, 12), exit: D(2026, 11, 3), born: D(1990, 11, 12)),
            Row(2, 1, EmploymentStatus.Active, D(2026, 5, 12), born: D(1990, 1, 12)),
            Row(3, 1, EmploymentStatus.Active, D(2025, 11, 12), born: D(1990, 1, 12), first: "One", last: "Year"),
        ];

        var events = Events(employees: people);

        var only = Assert.Single(events);
        Assert.Equal("One Year", only.Title);
        Assert.Equal("1 year with the company", only.Subtitle);
    }

    [Fact]
    public void Only_employees_on_probation_have_a_probation_review()
    {
        EmployeeFigureRow[] people =
        [
            Row(1, 1, EmploymentStatus.Active, D(2026, 1, 5), probationEnd: D(2026, 11, 12)),
            Row(2, 1, EmploymentStatus.Probation, D(2026, 8, 5), probationEnd: D(2026, 11, 12), first: "On", last: "Probation"),
        ];

        var only = Assert.Single(Events(employees: people));

        Assert.Equal("On Probation", only.Title);
    }

    [Fact]
    public void At_most_ten_events_are_returned_earliest_first_and_events_of_one_day_keep_holidays_first()
    {
        var holidays = Enumerable.Range(0, 12).Select(i => HolidayOn(D(2026, 11, 10).AddDays(i), $"Holiday {i}")).ToList();

        var events = Events(holidays, employees: []);

        Assert.Equal(10, events.Count);
        Assert.Equal("Holiday 0", events[0].Title);
        Assert.Equal("Holiday 9", events[^1].Title);
    }

    [Theory]
    [InlineData(2026, 11, 9, 2026, 11, 9)]
    [InlineData(2026, 11, 8, 2027, 11, 8)]
    [InlineData(1996, 2, 29, 2027, 2, 28)]
    public void The_next_occurrence_of_a_month_and_day_is_this_year_or_next_and_a_leap_day_falls_back_to_the_28th(
        int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        var next = DashboardCalculations.NextOccurrence(new DateOnly(year, month, day), Today);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), next);
    }

    [Fact]
    public void A_leap_day_birthday_is_celebrated_on_the_29th_in_a_leap_year()
    {
        var next = DashboardCalculations.NextOccurrence(D(1996, 2, 29), D(2028, 1, 10));

        Assert.Equal(D(2028, 2, 29), next);
    }
}
