using Apex.Hrms.Application.Dashboard;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using static Apex.Hrms.Application.Tests.Fakes.AttendanceFixtures;

namespace Apex.Hrms.Application.Tests.Dashboard;

/// <summary>
/// The dashboard use case wired to the real attendance, leave and organization handlers over fakes: today comes from the
/// clock (Monday 2026-11-09), attendance from the attendance overview rules, the rest from the source rows.
/// </summary>
public sealed class DashboardHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private Task<Result<DashboardSummaryDto>> SummaryAsync() => host.Get<IDashboardHandler>().HandleAsync(default);

    private async Task<(OrgSeed Org, EmployeeDto Employee)> SeedWorkerAsync()
    {
        var org = await host.SeedOrgAsync();
        var shift = await host.CreateShiftAsync();
        var employee = await host.CreateWorkerAsync(org, "mia@example.test", shift.Id);
        host.DashboardSource.Employees.Add(new EmployeeFigureRow(
            employee.Id,
            org.Department.Id,
            EmploymentStatus.Active,
            new DateOnly(2024, 3, 1),
            null,
            new DateOnly(1994, 5, 17),
            null,
            "Mia",
            "Hartley"));
        return (org, employee);
    }

    [Fact]
    public async Task An_empty_system_returns_a_zero_summary_with_no_payroll()
    {
        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(0, summary.Headcount.Total);
        Assert.Equal(0, summary.AttendanceToday.Scheduled);
        Assert.Equal(0, summary.PendingLeaveApprovals);
        Assert.Null(summary.Payroll);
        Assert.Empty(summary.PendingLeaves);
        Assert.Empty(summary.UpcomingEvents);
        Assert.Equal(12, summary.HeadcountGrowth.Count);
        Assert.Equal(host.Clock.UtcNow, summary.GeneratedAt);
    }

    [Fact]
    public async Task Today_is_the_clock_date_and_the_attendance_comes_from_the_overview_rules()
    {
        var (_, employee) = await SeedWorkerAsync();
        await host.PunchAsync(employee.Id, "CheckIn", 9, 40);

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(Monday, summary.AttendanceToday.Date);
        Assert.Equal(1, summary.AttendanceToday.Scheduled);
        Assert.Equal(1, summary.AttendanceToday.Late);
        Assert.Equal(100m, summary.AttendanceToday.AttendanceRate);
        Assert.Equal(1, summary.Headcount.Total);
        Assert.Equal(Monday, summary.AttendanceTrend[^1].Date);
        Assert.Equal(1, summary.AttendanceTrend[^1].Late);
    }

    [Fact]
    public async Task On_a_day_off_the_attendance_figures_are_those_of_the_latest_working_day()
    {
        await SeedWorkerAsync();
        host.Clock.Today = Saturday;

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(new DateOnly(2026, 11, 13), summary.AttendanceToday.Date);
        Assert.Equal(1, summary.AttendanceToday.Scheduled);
        Assert.Equal(1, summary.AttendanceToday.Absent);
    }

    [Fact]
    public async Task Submitted_leave_is_counted_and_listed_with_the_employee_and_type_names()
    {
        var (org, employee) = await SeedWorkerAsync();
        var type = await host.CreateTypeAsync();
        var application = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, new DateOnly(2026, 11, 16)));
        _ = org;

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(1, summary.PendingLeaveApprovals);
        var item = Assert.Single(summary.PendingLeaves);
        Assert.Equal(application.Id, item.Id);
        Assert.Equal("Mia Hartley", item.EmployeeName);
        Assert.Equal("Annual Leave", item.LeaveTypeName);
        Assert.Equal(new DateOnly(2026, 11, 16), item.StartDate);
    }

    [Fact]
    public async Task Only_five_pending_leaves_are_listed_but_all_are_counted()
    {
        var (_, employee) = await SeedWorkerAsync();
        var type = await host.CreateTypeAsync();
        for (var week = 0; week < 7; week++)
        {
            await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, new DateOnly(2026, 11, 16).AddDays(7 * week)));
        }

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(7, summary.PendingLeaveApprovals);
        Assert.Equal(5, summary.PendingLeaves.Count);
        Assert.Equal(new DateOnly(2026, 11, 16), summary.PendingLeaves[0].StartDate);
    }

    [Fact]
    public async Task Approved_leave_that_starts_soon_and_holidays_are_upcoming_events()
    {
        var (_, employee) = await SeedWorkerAsync();
        var type = await host.CreateTypeAsync();
        var submitted = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, new DateOnly(2026, 11, 16)));
        ResultAssert.Succeeded(await host.Approve(submitted));
        await host.CreateHolidayAsync(new DateOnly(2026, 11, 12), "Test Festival");

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(
            [("Holiday", "Test Festival"), ("Leave", "Mia Hartley")],
            summary.UpcomingEvents.Select(e => (e.Type, e.Title)));
        Assert.Equal(0, summary.PendingLeaveApprovals);
    }

    [Fact]
    public async Task Source_figures_pass_through_the_calculations()
    {
        await SeedWorkerAsync();
        host.DashboardSource.Recruitment = new RecruitmentRows(2, 5, new Dictionary<string, int> { ["Applied"] = 4, ["Interview"] = 1 }, 3);
        host.DashboardSource.PayrollRuns.Add(new PayrollRunRow("October 2026", "Paid", new DateOnly(2026, 10, 28), 1, 2000m, 200m, 1800m));
        host.DashboardSource.PayrollRuns.Add(new PayrollRunRow("September 2026", "Paid", new DateOnly(2026, 9, 28), 1, 1000m, 100m, 900m));

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(5, summary.Recruitment!.OpenPositions);
        Assert.Equal(5, summary.Recruitment.ActiveCandidates);
        Assert.Equal(100m, summary.Payroll!.NetChangePercent);
        Assert.Equal(1800m, summary.Payroll.TotalNet);
        Assert.Equal([("Engineering", 1)], summary.DepartmentDistribution.Select(d => (d.Label, d.Count)));
    }

    private static readonly string[] EmployeeGrants = ["dashboard:View", "attendance:View", "attendance:Create", "leave:View", "leave:Create"];

    private async Task SeedCompanyFiguresAsync()
    {
        var (org, employee) = await SeedWorkerAsync();
        host.DashboardSource.Employees.Add(new EmployeeFigureRow(
            employee.Id + 1, org.Department.Id, EmploymentStatus.Active, new DateOnly(2024, 3, 1), null, new DateOnly(1990, 11, 20), null, "Sana", "Okoye"));
        host.DashboardSource.Recruitment = new RecruitmentRows(2, 5, new Dictionary<string, int> { ["Applied"] = 4 }, 3);
        host.DashboardSource.PayrollRuns.Add(new PayrollRunRow("October 2026", "Paid", new DateOnly(2026, 10, 28), 1, 2000m, 200m, 1800m));
        host.ActivityLog.Append("system", "Entry 1", null, "System", new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task An_employee_role_gets_no_payroll_recruitment_activity_or_birthdays()
    {
        await SeedCompanyFiguresAsync();
        host.Permissions.Only(EmployeeGrants);

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Null(summary.Payroll);
        Assert.Null(summary.Recruitment);
        Assert.Empty(summary.RecentActivities);
        Assert.DoesNotContain(summary.UpcomingEvents, e => e.Type == "Birthday");
    }

    [Fact]
    public async Task An_hr_role_gets_every_part()
    {
        await SeedCompanyFiguresAsync();

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(1800m, summary.Payroll!.TotalNet);
        Assert.Equal(5, summary.Recruitment!.OpenPositions);
        Assert.NotEmpty(summary.RecentActivities);
        Assert.Contains(summary.UpcomingEvents, e => e is { Type: "Birthday", Title: "Sana Okoye" });
    }

    [Theory]
    [InlineData("payroll:View")]
    [InlineData("recruitment:View")]
    [InlineData("activity:View")]
    [InlineData("employees:ViewSensitive")]
    public async Task Each_part_needs_only_its_own_permission(string grant)
    {
        await SeedCompanyFiguresAsync();
        host.Permissions.Only([.. EmployeeGrants, "employees:Edit", grant]);

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(grant == "payroll:View", summary.Payroll is not null);
        Assert.Equal(grant == "recruitment:View", summary.Recruitment is not null);
        Assert.Equal(grant == "activity:View", summary.RecentActivities.Count > 0);
        Assert.Equal(grant == "employees:ViewSensitive", summary.UpcomingEvents.Any(e => e.Type == "Birthday"));
    }

    [Fact]
    public async Task Recent_activity_is_the_latest_six_entries_newest_first()
    {
        for (var i = 1; i <= 8; i++)
        {
            host.ActivityLog.Append("system", $"Entry {i}", null, "System", new DateTime(2026, 11, 1, i, 0, 0, DateTimeKind.Utc));
        }

        var summary = ResultAssert.Succeeded(await SummaryAsync());

        Assert.Equal(["Entry 8", "Entry 7", "Entry 6", "Entry 5", "Entry 4", "Entry 3"], summary.RecentActivities.Select(a => a.Message));
    }
}
