using System.Reflection;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Domain.Tests.Leave;

/// <summary>Builders for leave records; the database would give them their identity.</summary>
internal static class LeaveBuilders
{
    public static T WithId<T>(this T entity, int id)
        where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
        return entity;
    }

    public static LeaveTypeDetails TypeDetails(
        string code = "AL",
        decimal days = 15m,
        short maxConsecutive = 10,
        bool carryForward = false,
        decimal maxCarry = 0m,
        string color = "#3f6fd8",
        Gender? gender = null) => new(
        code,
        "Annual Leave",
        Description: null,
        days,
        IsPaid: true,
        RequiresDocument: false,
        maxConsecutive,
        carryForward,
        maxCarry,
        color,
        gender);

    public static LeaveType Type(int id = 1, decimal days = 15m, Gender? gender = null, RecordStatus status = RecordStatus.Active, string code = "AL") =>
        LeaveType.Create(TypeDetails(code, days, gender: gender), status).Value.WithId(id);

    public static LeavePolicy Policy(
        int id,
        int leaveTypeId = 1,
        int? jobGradeId = null,
        EmploymentType? employmentType = null,
        decimal days = 15m,
        LeaveAccrual accrual = LeaveAccrual.Upfront,
        short minServiceMonths = 0,
        RecordStatus status = RecordStatus.Active) =>
        LeavePolicy.Create(
            new LeavePolicyDetails($"Policy {id}", leaveTypeId, jobGradeId, employmentType, days, accrual, minServiceMonths),
            status).Value.WithId(id);

    public static LeaveApplicationDetails Details(
        DateOnly? start = null,
        DateOnly? end = null,
        bool halfDay = false,
        int employeeId = 7,
        int leaveTypeId = 1) => new(
        employeeId,
        leaveTypeId,
        start ?? new DateOnly(2026, 11, 16),
        end ?? start ?? new DateOnly(2026, 11, 16),
        halfDay,
        "Family matter",
        DocumentName: null);

    public static LeaveApplication Application(
        LeaveStatus status = LeaveStatus.Draft,
        DateOnly? start = null,
        DateOnly? end = null,
        decimal days = 1m,
        int employeeId = 7,
        int leaveTypeId = 1)
    {
        var details = Details(start, end, employeeId: employeeId, leaveTypeId: leaveTypeId);
        var application = LeaveApplication.Create(details, days, "LV-2026-0001", new DateOnly(2026, 11, 9)).Value.WithId(1);
        var actor = new LeaveActor(1, 3, "Approver");
        var today = new DateOnly(2026, 11, 9);
        switch (status)
        {
            case LeaveStatus.Draft:
                break;
            case LeaveStatus.Submitted:
                application.Submit(today);
                break;
            case LeaveStatus.Approved:
                application.Submit(today);
                application.Decide(LeaveDecision.Approve, actor, null, today);
                break;
            case LeaveStatus.Rejected:
                application.Submit(today);
                application.Decide(LeaveDecision.Reject, actor, "No", today);
                break;
            case LeaveStatus.Cancelled:
                application.Submit(today);
                application.Cancel(actor, null, today);
                break;
        }

        Assert.Equal(status, application.Status);
        return application;
    }

    public static EntitlementProfile Profile(int grade = 3, EmploymentType type = EmploymentType.FullTime, DateOnly? joined = null) =>
        new(grade, type, joined ?? new DateOnly(2020, 1, 1));
}

public sealed class WorkingDaysTests
{
    private static readonly IReadOnlySet<DayOfWeek> Weekend = new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    [Fact]
    public void A_full_week_has_five_working_days() =>
        Assert.Equal(5, WorkingDays.Count(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), Weekend, NoHolidays));

    [Fact]
    public void A_single_working_day_counts_one_and_a_single_weekend_day_none()
    {
        Assert.Equal(1, WorkingDays.Count(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 16), Weekend, NoHolidays));
        Assert.Equal(0, WorkingDays.Count(new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 21), Weekend, NoHolidays));
    }

    [Fact]
    public void Holidays_are_left_out_but_a_holiday_on_a_weekend_is_not_counted_twice()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 11, 18), new(2026, 11, 21) };

        Assert.Equal(4, WorkingDays.Count(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), Weekend, holidays));
    }

    [Fact]
    public void The_weekend_can_be_any_days_of_the_week()
    {
        var friday = new HashSet<DayOfWeek> { DayOfWeek.Friday };

        Assert.Equal(6, WorkingDays.Count(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), friday, NoHolidays));
        Assert.Equal(7, WorkingDays.Count(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), new HashSet<DayOfWeek>(), NoHolidays));
    }

    [Fact]
    public void A_range_that_ends_before_it_starts_has_no_days() =>
        Assert.Equal(0, WorkingDays.Count(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 16), Weekend, NoHolidays));

    [Fact]
    public void A_range_across_a_year_end_is_counted_day_by_day() =>
        Assert.Equal(2, WorkingDays.Count(new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 3), Weekend, NoHolidays));
}

public sealed class LeaveEntitlementTests
{
    private static readonly DateOnly AsOf = new(2026, 11, 3);

    private static decimal Resolve(LeaveType type, LeavePolicy[] policies, EntitlementProfile? employee = null, DateOnly? asOf = null) =>
        LeaveEntitlement.Resolve(type, policies, employee ?? LeaveBuilders.Profile(), asOf ?? AsOf);

    [Fact]
    public void Without_a_matching_policy_the_type_default_applies()
    {
        var type = LeaveBuilders.Type(days: 15m);

        Assert.Equal(15m, Resolve(type, []));
        Assert.Equal(15m, Resolve(type, [LeaveBuilders.Policy(1, jobGradeId: 8, days: 22m)]));
        Assert.Equal(15m, Resolve(type, [LeaveBuilders.Policy(1, employmentType: EmploymentType.Intern, days: 6m)]));
    }

    [Fact]
    public void The_most_specific_matching_policy_wins()
    {
        var type = LeaveBuilders.Type();
        var all = LeaveBuilders.Policy(1, days: 15m);
        var byType = LeaveBuilders.Policy(2, employmentType: EmploymentType.FullTime, days: 16m);
        var byGrade = LeaveBuilders.Policy(3, jobGradeId: 3, days: 18m);
        var byBoth = LeaveBuilders.Policy(4, jobGradeId: 3, employmentType: EmploymentType.FullTime, days: 19m);

        Assert.Equal(19m, Resolve(type, [all, byType, byGrade, byBoth]));
        Assert.Equal(18m, Resolve(type, [all, byType, byGrade]));
        Assert.Equal(16m, Resolve(type, [all, byType]));
        Assert.Equal(15m, Resolve(type, [all]));
    }

    [Fact]
    public void A_grade_match_outranks_an_employment_type_match()
    {
        var type = LeaveBuilders.Type();

        Assert.Equal(12m, Resolve(type, [LeaveBuilders.Policy(1, employmentType: EmploymentType.FullTime, days: 20m), LeaveBuilders.Policy(2, jobGradeId: 3, days: 12m)]));
    }

    [Fact]
    public void Inactive_policies_and_policies_of_other_types_are_ignored()
    {
        var type = LeaveBuilders.Type();
        var inactive = LeaveBuilders.Policy(1, jobGradeId: 3, days: 30m, status: RecordStatus.Inactive);
        var otherType = LeaveBuilders.Policy(2, leaveTypeId: 2, jobGradeId: 3, days: 40m);

        Assert.Equal(15m, Resolve(type, [inactive, otherType]));
    }

    [Fact]
    public void A_policy_whose_minimum_service_is_not_reached_is_skipped_for_a_less_specific_one()
    {
        var type = LeaveBuilders.Type();
        var senior = LeaveBuilders.Policy(1, jobGradeId: 3, days: 20m, minServiceMonths: 60);
        var standard = LeaveBuilders.Policy(2, days: 15m);

        // Joined 2020-01-01: 82 months of service on 2026-11-03, so the senior policy is satisfied.
        Assert.Equal(20m, Resolve(type, [senior, standard]));
        Assert.Equal(15m, Resolve(type, [senior, standard], LeaveBuilders.Profile(joined: new DateOnly(2024, 1, 1))));
    }

    [Fact]
    public void When_candidates_exist_but_none_is_satisfied_the_entitlement_is_zero()
    {
        var type = LeaveBuilders.Type();
        var policy = LeaveBuilders.Policy(1, days: 15m, minServiceMonths: 3);

        Assert.Equal(0m, Resolve(type, [policy], LeaveBuilders.Profile(joined: new DateOnly(2026, 9, 1))));
        Assert.Equal(15m, Resolve(type, [policy], LeaveBuilders.Profile(joined: new DateOnly(2026, 8, 1))));
    }

    [Theory]
    [InlineData(2026, 11, 3, 2026, 8, 3, 3)]
    [InlineData(2026, 11, 2, 2026, 8, 3, 2)]
    [InlineData(2026, 11, 3, 2026, 11, 3, 0)]
    [InlineData(2026, 11, 3, 2026, 12, 1, 0)]
    [InlineData(2027, 1, 1, 2026, 12, 31, 0)]
    [InlineData(2027, 1, 31, 2026, 12, 31, 1)]
    [InlineData(2026, 11, 3, 2020, 1, 1, 82)]
    public void Service_is_counted_in_whole_months_and_never_negative(int toYear, int toMonth, int toDay, int fromYear, int fromMonth, int fromDay, int expected) =>
        Assert.Equal(expected, LeaveEntitlement.MonthsBetween(new DateOnly(fromYear, fromMonth, fromDay), new DateOnly(toYear, toMonth, toDay)));

    [Theory]
    [InlineData(6, 1, 0.5)]
    [InlineData(6, 3, 1.5)]
    [InlineData(6, 6, 3)]
    [InlineData(6, 11, 5.5)]
    [InlineData(6, 12, 6)]
    [InlineData(7, 11, 6.5)]
    [InlineData(7, 3, 2)]
    [InlineData(10, 1, 1)]
    [InlineData(10, 5, 4)]
    [InlineData(15, 7, 9)]
    public void Monthly_accrual_is_a_twelfth_per_month_rounded_to_half_days(int daysPerYear, int month, double expected)
    {
        var type = LeaveBuilders.Type();
        var policy = LeaveBuilders.Policy(1, days: daysPerYear, accrual: LeaveAccrual.Monthly);

        Assert.Equal((decimal)expected, Resolve(type, [policy], asOf: new DateOnly(2026, month, 15)));
    }

    [Fact]
    public void Upfront_accrual_grants_the_whole_year_in_any_month()
    {
        var type = LeaveBuilders.Type();
        var policy = LeaveBuilders.Policy(1, days: 12m, accrual: LeaveAccrual.Upfront);

        Assert.Equal(12m, Resolve(type, [policy], asOf: new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void A_policy_with_a_minimum_service_and_monthly_accrual_applies_both()
    {
        var type = LeaveBuilders.Type();
        var policy = LeaveBuilders.Policy(1, days: 12m, accrual: LeaveAccrual.Monthly, minServiceMonths: 6);

        Assert.Equal(0m, Resolve(type, [policy], LeaveBuilders.Profile(joined: new DateOnly(2026, 6, 1)), new DateOnly(2026, 11, 3)));
        Assert.Equal(11m, Resolve(type, [policy], LeaveBuilders.Profile(joined: new DateOnly(2026, 4, 1)), new DateOnly(2026, 11, 3)));
    }
}

public sealed class LeaveBalancesTests
{
    private static LeaveApplication Application(int typeId, LeaveStatus status, DateOnly start, decimal days)
    {
        var details = LeaveBuilders.Details(start, start.AddDays((int)Math.Ceiling(days) - 1), leaveTypeId: typeId);
        var application = LeaveApplication.Create(details, days, "LV-2026-0001", new DateOnly(2026, 1, 1)).Value;
        var actor = new LeaveActor(1, null, "Admin");
        var today = new DateOnly(2026, 1, 1);
        if (status != LeaveStatus.Draft)
        {
            application.Submit(today);
        }

        switch (status)
        {
            case LeaveStatus.Approved:
                application.Decide(LeaveDecision.Approve, actor, null, today);
                break;
            case LeaveStatus.Rejected:
                application.Decide(LeaveDecision.Reject, actor, "No", today);
                break;
            case LeaveStatus.Cancelled:
                application.Cancel(actor, null, today);
                break;
        }

        return application;
    }

    [Fact]
    public void Approved_days_are_taken_submitted_days_pending_and_the_rest_available()
    {
        var type = LeaveBuilders.Type(days: 15m);
        var applications = new[]
        {
            Application(1, LeaveStatus.Approved, new DateOnly(2026, 3, 2), 2m),
            Application(1, LeaveStatus.Approved, new DateOnly(2026, 4, 6), 1m),
            Application(1, LeaveStatus.Submitted, new DateOnly(2026, 5, 4), 3m),
            Application(1, LeaveStatus.Draft, new DateOnly(2026, 6, 1), 4m),
            Application(1, LeaveStatus.Rejected, new DateOnly(2026, 7, 6), 5m),
            Application(1, LeaveStatus.Cancelled, new DateOnly(2026, 8, 3), 5m),
        };

        var line = Assert.Single(LeaveBalances.Compute([type], applications, Gender.Female, 2026, t => t.DefaultDaysPerYear));

        Assert.Equal(15m, line.Entitled);
        Assert.Equal(3m, line.Taken);
        Assert.Equal(3m, line.Pending);
        Assert.Equal(9m, line.Available);
        Assert.Equal("AL", line.Code);
        Assert.Equal("#3f6fd8", line.Color);
    }

    [Fact]
    public void Leave_counts_in_the_year_it_starts()
    {
        var type = LeaveBuilders.Type();
        var applications = new[]
        {
            Application(1, LeaveStatus.Approved, new DateOnly(2025, 12, 29), 3m),
            Application(1, LeaveStatus.Approved, new DateOnly(2026, 1, 5), 1m),
        };

        Assert.Equal(1m, LeaveBalances.Compute([type], applications, Gender.Male, 2026, t => 15m).Single().Taken);
        Assert.Equal(3m, LeaveBalances.Compute([type], applications, Gender.Male, 2025, t => 15m).Single().Taken);
        Assert.Equal(0m, LeaveBalances.Compute([type], applications, Gender.Male, 2027, t => 15m).Single().Taken);
    }

    [Fact]
    public void Available_is_never_negative()
    {
        var type = LeaveBuilders.Type();
        var applications = new[] { Application(1, LeaveStatus.Approved, new DateOnly(2026, 3, 2), 5m) };

        Assert.Equal(0m, LeaveBalances.Compute([type], applications, Gender.Male, 2026, t => 2m).Single().Available);
    }

    [Fact]
    public void Only_active_types_open_to_the_gender_are_listed()
    {
        var open = LeaveBuilders.Type(1);
        var inactive = LeaveBuilders.Type(2, status: RecordStatus.Inactive, code: "OLD");
        var forMen = LeaveBuilders.Type(3, gender: Gender.Male, code: "PAT");
        var forWomen = LeaveBuilders.Type(4, gender: Gender.Female, code: "MAT");

        var ids = LeaveBalances.Compute([open, inactive, forMen, forWomen], [], Gender.Female, 2026, t => 1m).Select(line => line.LeaveTypeId);

        Assert.Equal([1, 4], ids);
    }

    [Fact]
    public void Each_type_uses_its_own_applications_and_its_own_entitlement()
    {
        var annual = LeaveBuilders.Type(1);
        var casual = LeaveBuilders.Type(2, code: "CL");
        var applications = new[] { Application(2, LeaveStatus.Approved, new DateOnly(2026, 3, 2), 2m) };

        var lines = LeaveBalances.Compute([annual, casual], applications, Gender.Male, 2026, t => t.Id * 10m);

        Assert.Equal((10m, 0m, 10m), (lines[0].Entitled, lines[0].Taken, lines[0].Available));
        Assert.Equal((20m, 2m, 18m), (lines[1].Entitled, lines[1].Taken, lines[1].Available));
    }
}

/// <summary>The transition table of a leave application and the rules of each move.</summary>
public sealed class LeaveApplicationTransitionTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly LeaveActor Admin = new(1, null, "Admin");
    private static readonly LeaveActor Manager = new(2, 3, "Manager");

    private static LeaveStatus[] All => Enum.GetValues<LeaveStatus>();

    // ---- Submit: Draft only ----

    [Theory]
    [InlineData(LeaveStatus.Draft, true)]
    [InlineData(LeaveStatus.Submitted, false)]
    [InlineData(LeaveStatus.Approved, false)]
    [InlineData(LeaveStatus.Rejected, false)]
    [InlineData(LeaveStatus.Cancelled, false)]
    public void Submit_is_allowed_from_draft_only(LeaveStatus from, bool allowed)
    {
        var application = LeaveBuilders.Application(from);

        var result = application.Submit(Today);

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? LeaveStatus.Submitted : from, application.Status);
        if (!allowed)
        {
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Equal($"Only a draft can be submitted (this one is {LeaveStatusText.ToText(from)}).", result.Error.Message);
        }
    }

    [Fact]
    public void Submitting_sets_the_application_date_to_today()
    {
        var application = LeaveBuilders.Application();

        application.Submit(new DateOnly(2026, 11, 12));

        Assert.Equal(new DateOnly(2026, 11, 12), application.AppliedOn);
    }

    // ---- Approve and reject: Submitted only ----

    [Theory]
    [InlineData(LeaveDecision.Approve)]
    [InlineData(LeaveDecision.Reject)]
    public void A_decision_is_allowed_from_submitted_only(LeaveDecision decision)
    {
        foreach (var from in All)
        {
            var application = LeaveBuilders.Application(from);

            var result = application.Decide(decision, Manager, "Remarks", Today);

            Assert.Equal(from == LeaveStatus.Submitted, result.IsSuccess);
            if (from != LeaveStatus.Submitted)
            {
                Assert.Equal(ErrorType.Conflict, result.Error.Type);
                Assert.Equal($"Only submitted applications can be decided (this one is {LeaveStatusText.ToText(from)}).", result.Error.Message);
                Assert.Equal(from, application.Status);
            }
        }
    }

    [Fact]
    public void Approving_records_the_approver_the_name_the_day_and_default_remarks()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted);

        var result = application.Decide(LeaveDecision.Approve, Manager, "   ", Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(LeaveStatus.Approved, application.Status);
        Assert.Equal(3, application.ApproverEmployeeId);
        Assert.Equal(2, application.ActionedByUserId);
        Assert.Equal("Manager", application.ActionedByName);
        Assert.Equal(Today, application.ActionedOn);
        Assert.Equal("Approved.", application.ApproverRemarks);
    }

    [Fact]
    public void Approving_does_not_need_remarks_and_keeps_those_given()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted);

        application.Decide(LeaveDecision.Approve, Manager, "  Enjoy  ", Today);

        Assert.Equal("Enjoy", application.ApproverRemarks);
    }

    [Fact]
    public void The_built_in_administrator_may_decide_and_leaves_no_approver_employee()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted, employeeId: 3);

        var result = application.Decide(LeaveDecision.Approve, Admin, null, Today);

        Assert.True(result.IsSuccess);
        Assert.Null(application.ApproverEmployeeId);
        Assert.Equal(1, application.ActionedByUserId);
        Assert.Equal("Admin", application.ActionedByName);
    }

    [Theory]
    [InlineData(LeaveDecision.Approve)]
    [InlineData(LeaveDecision.Reject)]
    public void Nobody_decides_their_own_request(LeaveDecision decision)
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted, employeeId: 3);

        var result = application.Decide(decision, Manager, "Remarks", Today);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("You cannot decide your own leave request.", result.Error.Message);
        Assert.Equal(LeaveStatus.Submitted, application.Status);
        Assert.Null(application.ApproverEmployeeId);
        Assert.Null(application.ActionedOn);
    }

    [Fact]
    public void Another_employee_may_decide()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted, employeeId: 4);

        Assert.True(application.Decide(LeaveDecision.Reject, Manager, "Busy week", Today).IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejecting_needs_a_reason(string? remarks)
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted);

        var result = application.Decide(LeaveDecision.Reject, Manager, remarks, Today);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Give a reason for rejecting the request.", result.Error.Message);
        Assert.Equal(["A reason is required."], result.Error.FieldErrors!["remarks"]);
        Assert.Equal(LeaveStatus.Submitted, application.Status);
    }

    [Fact]
    public void Rejecting_records_the_reason()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted);

        application.Decide(LeaveDecision.Reject, Manager, "  Team is short  ", Today);

        Assert.Equal(LeaveStatus.Rejected, application.Status);
        Assert.Equal("Team is short", application.ApproverRemarks);
        Assert.Equal(Today, application.ActionedOn);
    }

    // ---- Cancel: Submitted, or Approved that has not started ----

    [Theory]
    [InlineData(LeaveStatus.Draft, false)]
    [InlineData(LeaveStatus.Submitted, true)]
    [InlineData(LeaveStatus.Approved, true)]
    [InlineData(LeaveStatus.Rejected, false)]
    [InlineData(LeaveStatus.Cancelled, false)]
    public void Cancel_is_allowed_from_submitted_and_approved_leave_that_has_not_started(LeaveStatus from, bool allowed)
    {
        var application = LeaveBuilders.Application(from);

        var result = application.Cancel(Manager, null, Today);

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? LeaveStatus.Cancelled : from, application.Status);
        if (!allowed)
        {
            Assert.Equal($"Only submitted or approved leave can be cancelled (this one is {LeaveStatusText.ToText(from)}).", result.Error.Message);
        }
    }

    [Theory]
    [InlineData(2026, 11, 16, true)]
    [InlineData(2026, 11, 10, true)]
    [InlineData(2026, 11, 9, false)]
    [InlineData(2026, 11, 2, false)]
    public void Approved_leave_can_be_cancelled_only_before_its_start_date(int year, int month, int day, bool allowed)
    {
        var application = LeaveBuilders.Application(LeaveStatus.Approved, start: new DateOnly(year, month, day));

        var result = application.Cancel(Manager, null, Today);

        Assert.Equal(allowed, result.IsSuccess);
        if (!allowed)
        {
            Assert.Equal("Leave that has already started cannot be cancelled.", result.Error.Message);
        }
    }

    [Fact]
    public void Submitted_leave_can_be_cancelled_even_when_it_has_started()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted, start: new DateOnly(2026, 11, 2));

        Assert.True(application.Cancel(Manager, null, Today).IsSuccess);
    }

    [Fact]
    public void Cancelling_records_who_and_when_with_default_remarks_and_no_approver_employee()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Submitted);

        application.Cancel(Manager, null, Today);

        Assert.Equal("Cancelled.", application.ApproverRemarks);
        Assert.Equal(Today, application.ActionedOn);
        Assert.Equal("Manager", application.ActionedByName);
        Assert.Null(application.ApproverEmployeeId);
    }

    [Fact]
    public void Cancelling_keeps_the_remarks_given()
    {
        var application = LeaveBuilders.Application(LeaveStatus.Approved);

        application.Cancel(Manager, "Plans changed", Today);

        Assert.Equal("Plans changed", application.ApproverRemarks);
    }

    // ---- Update and delete: Draft only ----

    [Fact]
    public void Only_a_draft_can_be_changed_or_deleted()
    {
        foreach (var status in All)
        {
            var application = LeaveBuilders.Application(status);

            var update = application.Update(LeaveBuilders.Details(), 1m);
            var delete = application.EnsureCanDelete();

            Assert.Equal(status == LeaveStatus.Draft, update.IsSuccess);
            Assert.Equal(status == LeaveStatus.Draft, delete.IsSuccess);
            if (status != LeaveStatus.Draft)
            {
                Assert.Equal(ErrorType.Conflict, update.Error.Type);
                Assert.Equal("Only draft applications can be changed. Cancel a submitted one instead.", update.Error.Message);
                Assert.Equal("Only draft applications can be deleted. Cancel a submitted one instead.", delete.Error.Message);
            }
        }
    }

    // ---- Details ----

    [Fact]
    public void A_half_day_is_half_a_day_and_otherwise_the_working_days_count()
    {
        Assert.Equal(0.5m, LeaveApplication.DaysFor(LeaveBuilders.Details(halfDay: true), 1));
        Assert.Equal(3m, LeaveApplication.DaysFor(LeaveBuilders.Details(), 3));
    }

    [Fact]
    public void A_half_day_must_be_a_single_date()
    {
        var result = LeaveApplication.Validate(LeaveBuilders.Details(new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 17), halfDay: true));

        Assert.Equal("A half day must start and end on the same date.", result.Error.Message);
        Assert.Equal(["A half day covers a single date."], result.Error.FieldErrors!["endDate"]);
        Assert.True(LeaveApplication.Validate(LeaveBuilders.Details(halfDay: true)).IsSuccess);
    }

    [Fact]
    public void The_end_cannot_be_before_the_start()
    {
        var result = LeaveApplication.Validate(LeaveBuilders.Details(new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 16)));

        Assert.Equal("End date must be on or after the start date.", result.Error.Message);
        Assert.Equal(["Must be on or after the start date."], result.Error.FieldErrors!["endDate"]);
    }

    [Fact]
    public void A_request_with_no_days_is_refused()
    {
        var result = LeaveApplication.Create(LeaveBuilders.Details(), 0m, "LV-2026-0001", Today);

        Assert.Equal("The selected dates contain no working days.", result.Error.Message);
        Assert.Equal(["Choose dates that include a working day."], result.Error.FieldErrors!["endDate"]);
    }

    [Fact]
    public void A_new_application_is_a_draft_applied_today_with_nobody_actioning_it()
    {
        var application = LeaveApplication.Create(LeaveBuilders.Details(), 1m, "LV-2026-0001", Today).Value;

        Assert.Equal(LeaveStatus.Draft, application.Status);
        Assert.Equal(Today, application.AppliedOn);
        Assert.Null(application.ActionedOn);
        Assert.Null(application.ApproverEmployeeId);
        Assert.Null(application.ApproverRemarks);
        Assert.False(application.IsCommitted);
    }

    [Theory]
    [InlineData(LeaveStatus.Draft, false)]
    [InlineData(LeaveStatus.Submitted, true)]
    [InlineData(LeaveStatus.Approved, true)]
    [InlineData(LeaveStatus.Rejected, false)]
    [InlineData(LeaveStatus.Cancelled, false)]
    public void Only_submitted_and_approved_leave_is_committed(LeaveStatus status, bool committed) =>
        Assert.Equal(committed, LeaveBuilders.Application(status).IsCommitted);

    [Theory]
    [InlineData(16, 18, 18, 20, true)]
    [InlineData(16, 18, 19, 20, false)]
    [InlineData(16, 18, 14, 16, true)]
    [InlineData(16, 18, 10, 15, false)]
    [InlineData(16, 18, 17, 17, true)]
    public void Overlap_means_sharing_at_least_one_day(int start, int end, int otherStart, int otherEnd, bool overlaps)
    {
        var application = LeaveBuilders.Application(start: new DateOnly(2026, 11, start), end: new DateOnly(2026, 11, end), days: 3m);

        Assert.Equal(overlaps, application.Overlaps(new DateOnly(2026, 11, otherStart), new DateOnly(2026, 11, otherEnd)));
    }
}

/// <summary>The rules of a leave type and a leave policy that need no other record.</summary>
public sealed class LeaveTypeAndPolicyDomainTests
{
    [Fact]
    public void A_valid_type_is_created_and_trimmed()
    {
        var result = LeaveType.Create(LeaveBuilders.TypeDetails() with { Code = "  AL ", Description = "  " }, RecordStatus.Active);

        Assert.Equal("AL", result.Value.Code);
        Assert.Null(result.Value.Description);
    }

    [Theory]
    [InlineData("al")]
    [InlineData("A_L")]
    [InlineData("A L")]
    [InlineData("-A")]
    [InlineData("")]
    public void The_code_must_be_upper_case_letters_digits_and_dashes(string code) =>
        Assert.Contains("code", LeaveType.Validate(LeaveBuilders.TypeDetails(code)).Error.FieldErrors!.Keys);

    [Fact]
    public void At_least_one_consecutive_day_is_required() =>
        Assert.Equal(
            "Maximum consecutive days must be at least 1.",
            LeaveType.Validate(LeaveBuilders.TypeDetails(maxConsecutive: 0)).Error.Message);

    [Fact]
    public void Carry_forward_cannot_exceed_the_yearly_entitlement_when_it_is_on()
    {
        var refused = LeaveType.Validate(LeaveBuilders.TypeDetails(days: 10m, carryForward: true, maxCarry: 11m));

        Assert.Equal("Carry-forward days cannot exceed the yearly entitlement.", refused.Error.Message);
        Assert.Equal(["Must not exceed the yearly entitlement."], refused.Error.FieldErrors!["maxCarryForwardDays"]);
        Assert.True(LeaveType.Validate(LeaveBuilders.TypeDetails(days: 10m, carryForward: true, maxCarry: 10m)).IsSuccess);
        Assert.True(LeaveType.Validate(LeaveBuilders.TypeDetails(days: 10m, carryForward: false, maxCarry: 11m)).IsSuccess);
    }

    [Theory]
    [InlineData("#3f6fd8", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("3f6fd8", false)]
    [InlineData("#3f6fd", false)]
    [InlineData("#3f6fdg", false)]
    [InlineData("#3f6fd88", false)]
    [InlineData("", false)]
    public void The_colour_is_a_hash_and_six_hex_digits(string color, bool valid) =>
        Assert.Equal(valid, LeaveType.IsValidColor(color));

    [Fact]
    public void Status_changes_only_through_activate_and_deactivate()
    {
        var type = LeaveType.Create(LeaveBuilders.TypeDetails(), RecordStatus.Active).Value;

        type.Deactivate();
        Assert.Equal(RecordStatus.Inactive, type.Status);
        type.Update(LeaveBuilders.TypeDetails(days: 20m));
        Assert.Equal(RecordStatus.Inactive, type.Status);
        type.Activate();
        Assert.Equal(RecordStatus.Active, type.Status);
    }

    [Fact]
    public void A_policy_needs_a_name_and_non_negative_numbers()
    {
        var result = LeavePolicy.Validate(new LeavePolicyDetails(" ", 1, null, null, -1m, LeaveAccrual.Upfront, -1));

        Assert.Contains("name", result.Error.FieldErrors!.Keys);
        Assert.Contains("daysPerYear", result.Error.FieldErrors.Keys);
        Assert.Contains("minServiceMonths", result.Error.FieldErrors.Keys);
    }

    [Fact]
    public void A_policy_can_be_activated_and_deactivated_again_and_again()
    {
        var policy = LeaveBuilders.Policy(1, status: RecordStatus.Inactive);

        policy.Activate();
        policy.Activate();
        Assert.True(policy.IsActive);
        policy.Deactivate();
        policy.Deactivate();
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void The_status_names_are_the_ones_the_database_checks()
    {
        Assert.Equal(["Draft", "Submitted", "Approved", "Rejected", "Cancelled"], Enum.GetValues<LeaveStatus>().Select(LeaveStatusText.ToText));
        Assert.Equal(["Upfront", "Monthly"], Enum.GetValues<LeaveAccrual>().Select(LeaveAccrualText.ToText));
        Assert.False(LeaveStatusText.TryParse("draft", out _));
        Assert.False(LeaveStatusText.TryParse("1", out _));
    }

    [Fact]
    public void Holiday_has_no_public_constructor_so_the_leave_module_cannot_create_one() =>
        Assert.Empty(typeof(Domain.Attendance.Holiday).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
}
