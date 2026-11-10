using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Balances;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Leave;

/// <summary>
/// Balances (worked out on request, never stored) and the leave calendar. Today is Monday 2026-11-09 in the fake
/// clock; the employees joined on 2024-03-01 unless a test says otherwise.
/// </summary>
public sealed class LeaveBalanceAndCalendarTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private async Task<(OrgSeed Org, EmployeeDto Employee, LeaveTypeDto Type)> SeedAsync(decimal days = 15m)
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test");
        var type = await host.CreateTypeAsync(LeaveFixtures.NewType(days: days));
        return (org, employee, type);
    }

    private async Task<LeaveBalanceDto> BalanceAsync(int employeeId, int typeId, int? year = null) =>
        ResultAssert.Succeeded(await host.Balances(employeeId, year)).Single(line => line.LeaveTypeId == typeId);

    // ---- Balances ----

    [Fact]
    public async Task A_new_employee_has_the_default_days_and_nothing_taken()
    {
        var (_, employee, type) = await SeedAsync();

        var balance = await BalanceAsync(employee.Id, type.Id);

        Assert.Equal("AL", balance.Code);
        Assert.Equal("Annual Leave", balance.Name);
        Assert.Equal("#3f6fd8", balance.Color);
        Assert.Equal(15m, balance.Entitled);
        Assert.Equal(0m, balance.Taken);
        Assert.Equal(0m, balance.Pending);
        Assert.Equal(15m, balance.Available);
    }

    [Fact]
    public async Task Approved_days_are_taken_submitted_days_are_pending_and_the_rest_is_available()
    {
        var (_, employee, type) = await SeedAsync();
        var approved = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));
        ResultAssert.Succeeded(await host.Approve(approved));
        await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 23), D(11, 24)));
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 30)));
        var rejected = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(12, 1)));
        ResultAssert.Succeeded(await host.Reject(rejected));

        var balance = await BalanceAsync(employee.Id, type.Id);

        Assert.Equal(15m, balance.Entitled);
        Assert.Equal(3m, balance.Taken);
        Assert.Equal(2m, balance.Pending);
        Assert.Equal(10m, balance.Available);
    }

    [Fact]
    public async Task Leave_counts_in_the_year_it_starts_and_other_years_are_untouched()
    {
        var (_, employee, type) = await SeedAsync();
        await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 5)));

        Assert.Equal(0m, (await BalanceAsync(employee.Id, type.Id, 2026)).Pending);
        Assert.Equal(2m, (await BalanceAsync(employee.Id, type.Id, 2027)).Pending);
        Assert.Equal(0m, (await BalanceAsync(employee.Id, type.Id, 2025)).Pending);
    }

    [Fact]
    public async Task The_available_days_never_go_below_zero()
    {
        var (_, employee, type) = await SeedAsync(days: 5m);
        var approved = await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)));
        ResultAssert.Succeeded(await host.Approve(approved));

        // Lowering the entitlement afterwards leaves more days taken than entitled.
        ResultAssert.Succeeded(await host.Get<IUpdateHandler<LeaveTypeUpdateRequest, LeaveTypeDto>>()
            .HandleAsync(type.Id, LeaveFixtures.TypeUpdateOf(type) with { DefaultDaysPerYear = 1m }, default));
        var balance = await BalanceAsync(employee.Id, type.Id);

        Assert.Equal(1m, balance.Entitled);
        Assert.Equal(3m, balance.Taken);
        Assert.Equal(0m, balance.Available);
    }

    [Fact]
    public async Task The_most_specific_matching_policy_decides_the_entitlement()
    {
        var (org, employee, type) = await SeedAsync();
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "All staff", days: 16m));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Full-time", employmentType: "Full-time", days: 17m));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Grade G1", jobGradeId: org.Grade.Id, days: 18m));
        var both = await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "G1 full-time", jobGradeId: org.Grade.Id, employmentType: "Full-time", days: 19m));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Interns", employmentType: "Intern", days: 30m));

        Assert.Equal(19m, (await BalanceAsync(employee.Id, type.Id)).Entitled);

        ResultAssert.Succeeded(await host.Command<DeactivateCommand, LeavePolicyDto>(
            new DeactivateCommand(both.Id, Requests.Version(both.RowVersion))));
        Assert.Equal(18m, (await BalanceAsync(employee.Id, type.Id)).Entitled);
    }

    [Fact]
    public async Task A_policy_for_another_grade_or_type_does_not_apply()
    {
        var (_, employee, type) = await SeedAsync();
        var other = await host.CreateJobGradeAsync("G2");
        var otherType = await host.CreateTypeAsync(LeaveFixtures.NewType("CL", "Casual Leave", days: 6m));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "G2 only", jobGradeId: other.Id, days: 30m));
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(otherType.Id, "Casual", days: 9m));

        Assert.Equal(15m, (await BalanceAsync(employee.Id, type.Id)).Entitled);
        Assert.Equal(9m, (await BalanceAsync(employee.Id, otherType.Id)).Entitled);
    }

    [Fact]
    public async Task Too_little_service_for_every_matching_policy_means_no_entitlement()
    {
        var org = await host.SeedOrgAsync();
        var employee = await host.CreateLeaveEmployeeAsync(org, "mia@example.test", joined: new DateOnly(2026, 8, 20));
        var type = await host.CreateTypeAsync();
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "After 3 months", days: 15m, minServiceMonths: 3));

        // 20 August to 9 November is two whole months.
        Assert.Equal(0m, (await BalanceAsync(employee.Id, type.Id)).Entitled);

        host.Clock.Today = D(11, 20);
        Assert.Equal(15m, (await BalanceAsync(employee.Id, type.Id)).Entitled);
    }

    [Fact]
    public async Task Monthly_accrual_grants_a_twelfth_per_month_rounded_to_half_days_as_of_today()
    {
        var (_, employee, type) = await SeedAsync();
        await host.CreatePolicyAsync(LeaveFixtures.NewPolicy(type.Id, "Monthly", days: 7m, accrual: "Monthly"));

        // November is month 11: 7 x 11 / 12 = 6.42 days, which rounds to 6.5.
        Assert.Equal(6.5m, (await BalanceAsync(employee.Id, type.Id)).Entitled);

        // March is month 3: 7 x 3 / 12 = 1.75, which rounds to 2.
        host.Clock.Today = D(3, 15);
        Assert.Equal(2m, (await BalanceAsync(employee.Id, type.Id)).Entitled);

        // Any other year grants the whole year.
        Assert.Equal(7m, (await BalanceAsync(employee.Id, type.Id, 2025)).Entitled);
    }

    [Fact]
    public async Task Types_for_the_other_gender_and_inactive_types_are_not_listed()
    {
        var (_, employee, type) = await SeedAsync();
        var paternity = await host.CreateTypeAsync(LeaveFixtures.NewType("PAT", "Paternity", gender: "Male"));
        var maternity = await host.CreateTypeAsync(LeaveFixtures.NewType("MAT", "Maternity", gender: "Female"));
        var old = await host.CreateTypeAsync(LeaveFixtures.NewType("OLD", "Old", status: "Inactive"));

        var ids = ResultAssert.Succeeded(await host.Balances(employee.Id)).Select(line => line.LeaveTypeId).ToList();

        Assert.Contains(type.Id, ids);
        Assert.Contains(maternity.Id, ids);
        Assert.DoesNotContain(paternity.Id, ids);
        Assert.DoesNotContain(old.Id, ids);
    }

    [Fact]
    public async Task The_balance_of_an_unknown_employee_or_an_absurd_year_is_refused()
    {
        var (_, employee, _) = await SeedAsync();

        ResultAssert.Failed(await host.Balances(9999), ErrorType.NotFound);
        ResultAssert.FailedOnField(await host.Balances(employee.Id, 1899), "year");
        ResultAssert.FailedOnField(await host.Balances(employee.Id, 2101), "year");
    }

    // ---- Calendar ----

    private async Task<(LeaveApplicationDto Approved, LeaveApplicationDto Pending, EmployeeDto Employee, EmployeeDto Colleague, LeaveTypeDto Type)> CalendarDataAsync()
    {
        var (org, employee, type) = await SeedAsync();
        var colleague = await host.CreateLeaveEmployeeAsync(org, "ben@example.test", "Male");
        var approved = ResultAssert.Succeeded(await host.Approve(
            await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16), D(11, 18)))));
        var pending = await host.SubmittedAsync(LeaveFixtures.NewApplication(colleague.Id, type.Id, D(11, 17), D(11, 17), halfDay: true));
        await host.CreateApplicationAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 23)));
        ResultAssert.Succeeded(await host.Reject(await host.SubmittedAsync(LeaveFixtures.NewApplication(colleague.Id, type.Id, D(11, 24)))));
        return (approved, pending, employee, colleague, type);
    }

    [Fact]
    public async Task The_calendar_lists_approved_and_submitted_leave_for_each_day_of_the_range()
    {
        var (approved, pending, employee, colleague, type) = await CalendarDataAsync();

        var calendar = ResultAssert.Succeeded(await host.Calendar(new LeaveCalendarRequest(D(11, 14), D(11, 22))));

        Assert.Equal(9, calendar.Days.Count);
        Assert.Equal(D(11, 14), calendar.From);
        Assert.Equal(D(11, 22), calendar.To);
        Assert.Equal(D(11, 14), calendar.Days[0].Date);
        Assert.Empty(calendar.Days[0].Entries);
        var tuesday = calendar.Days.Single(day => day.Date == D(11, 17));
        Assert.Equal(2, tuesday.Entries.Count);
        var mia = tuesday.Entries.Single(entry => entry.EmployeeId == employee.Id);
        var ben = tuesday.Entries.Single(entry => entry.EmployeeId == colleague.Id);
        Assert.Equal(approved.Id, mia.ApplicationId);
        Assert.Equal("Approved", mia.Status);
        Assert.False(mia.HalfDay);
        Assert.Equal("Mia Hartley", mia.EmployeeName);
        Assert.Equal("Annual Leave", mia.LeaveTypeName);
        Assert.Equal("#3f6fd8", mia.Color);
        Assert.Equal(type.Id, mia.LeaveTypeId);
        Assert.Equal(pending.Id, ben.ApplicationId);
        Assert.Equal("Submitted", ben.Status);
        Assert.True(ben.HalfDay);
        Assert.DoesNotContain(calendar.Days, day => day.Date >= D(11, 19) && day.Entries.Count > 0);
    }

    [Fact]
    public async Task Pending_leave_can_be_left_out_and_drafts_and_rejected_leave_never_show()
    {
        await CalendarDataAsync();

        var calendar = ResultAssert.Succeeded(await host.Calendar(new LeaveCalendarRequest(D(11, 16), D(11, 30), IncludePending: false)));

        Assert.All(calendar.Days.SelectMany(day => day.Entries), entry => Assert.Equal("Approved", entry.Status));
        Assert.Equal(3, calendar.Days.Count(day => day.Entries.Count > 0));
    }

    [Fact]
    public async Task The_calendar_filters_by_leave_type_and_department()
    {
        var (org, employee, type) = await SeedAsync();
        var casual = await host.CreateTypeAsync(LeaveFixtures.NewType("CL", "Casual Leave", maxConsecutive: 3));
        await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, 16)));
        await host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, casual.Id, D(11, 17)));
        var range = new LeaveCalendarRequest(D(11, 16), D(11, 17));

        var onlyCasual = ResultAssert.Succeeded(await host.Calendar(range with { LeaveTypeId = casual.Id }));
        var department = ResultAssert.Succeeded(await host.Calendar(range with { DepartmentId = org.Department.Id }));
        var otherDepartment = ResultAssert.Succeeded(await host.Calendar(range with { DepartmentId = 9999 }));

        Assert.Equal(1, onlyCasual.Days.Sum(day => day.Entries.Count));
        Assert.Equal(2, department.Days.Sum(day => day.Entries.Count));
        Assert.Empty(otherDepartment.Days.SelectMany(day => day.Entries));
    }

    [Fact]
    public async Task The_calendar_marks_weekends_and_names_holidays()
    {
        await SeedAsync();
        host.AddHoliday(D(11, 18), "Test holiday");

        var calendar = ResultAssert.Succeeded(await host.Calendar(new LeaveCalendarRequest(D(11, 16), D(11, 22))));

        Assert.Equal(
            [false, false, false, false, false, true, true],
            calendar.Days.Select(day => day.IsWeekend).ToArray());
        Assert.Equal("Test holiday", calendar.Days.Single(day => day.Date == D(11, 18)).Holiday);
        Assert.Null(calendar.Days.Single(day => day.Date == D(11, 17)).Holiday);
    }

    [Fact]
    public async Task The_calendar_range_must_be_in_order_and_at_most_a_year()
    {
        ResultAssert.FailedOnField(await host.Calendar(new LeaveCalendarRequest(D(11, 20), D(11, 19))), "to");
        ResultAssert.FailedOnField(await host.Calendar(new LeaveCalendarRequest(D(1, 1), new DateOnly(2027, 1, 2))), "to");
        Assert.True((await host.Calendar(new LeaveCalendarRequest(D(11, 9), D(11, 9)))).IsSuccess);
        Assert.True((await host.Calendar(new LeaveCalendarRequest(D(1, 1), new DateOnly(2027, 1, 1)))).IsSuccess);
    }
}
