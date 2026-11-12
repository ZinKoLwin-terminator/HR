using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Domain.Tests.Payroll;

/// <summary>
/// Golden tests of the DEMONSTRATION pay calculation: every case of the frontend's payroll-calc.spec.ts, with the same
/// figures, so the API and the mock service agree to the cent. None of it is a statutory calculation.
/// </summary>
public sealed class PayCalculatorTests
{
    private static SalaryComponent Component(
        string code,
        string name,
        ComponentType type,
        CalculationMethod calculation,
        decimal value,
        RecordStatus status = RecordStatus.Active) =>
        SalaryComponent.Create(new SalaryComponentDetails(code, name, type, calculation, value, null), status).Value;

    private static readonly SalaryComponent Housing = Component("HOUSING", "Housing", ComponentType.Earning, CalculationMethod.PercentOfBasic, 10m);
    private static readonly SalaryComponent Transport = Component("TRANSPORT", "Transport", ComponentType.Earning, CalculationMethod.Fixed, 50m);
    private static readonly SalaryComponent Tax = Component("TAX", "Tax", ComponentType.Deduction, CalculationMethod.PercentOfGross, 5m);
    private static readonly SalaryComponent Social = Component("SOCIAL", "Social", ComponentType.Deduction, CalculationMethod.PercentOfGross, 2m);
    private static readonly SalaryComponent Pension = Component("PENSION", "Pension", ComponentType.Deduction, CalculationMethod.PercentOfBasic, 3m);

    private static PayInput November(
        IReadOnlyList<SalaryComponent>? components = null,
        DateOnly? joined = null,
        DateOnly? exit = null,
        decimal unpaidLeaveDays = 0m,
        int overtimeMinutes = 0) => new(
        1000m,
        components ?? [Housing, Transport, Tax, Social],
        new DateOnly(2026, 11, 1),
        new DateOnly(2026, 11, 30),
        joined ?? new DateOnly(2020, 1, 1),
        exit,
        WorkingDays: 20,
        unpaidLeaveDays,
        overtimeMinutes);

    [Fact]
    public void A_full_month_has_earnings_gross_deductions_on_gross_and_net()
    {
        var pay = PayCalculator.Calculate(November())!;

        Assert.Equal(1000m, pay.Basic);
        Assert.Equal([("HOUSING", 100m), ("TRANSPORT", 50m)], pay.Earnings.Select(line => (line.Code, line.Amount)));
        Assert.Equal(1150m, pay.Gross);
        Assert.Equal([("TAX", 57.5m), ("SOCIAL", 23m)], pay.Deductions.Select(line => (line.Code, line.Amount)));
        Assert.Equal(80.5m, pay.TotalDeductions);
        Assert.Equal(1069.5m, pay.Net);
        Assert.Equal(30, pay.DaysEmployed);
        Assert.Equal(30, pay.PeriodDays);
        Assert.Equal("10% of basic", pay.Earnings[0].Basis);
        Assert.Equal("Fixed 50", pay.Earnings[1].Basis);
        Assert.Equal("5% of gross", pay.Deductions[0].Basis);
    }

    [Fact]
    public void A_joiner_is_prorated_by_calendar_days_basic_and_fixed_amounts_alike()
    {
        // Joined 16 Nov: 15 of 30 days.
        var joiner = PayCalculator.Calculate(November(joined: new DateOnly(2026, 11, 16)))!;

        Assert.Equal(15, joiner.DaysEmployed);
        Assert.Equal(500m, joiner.Basic);
        Assert.Equal([50m, 25m], joiner.Earnings.Select(line => line.Amount)); // 10% of the prorated basic, half the fixed amount
        Assert.Equal(575m, joiner.Gross);
    }

    [Fact]
    public void A_leaver_is_prorated_by_calendar_days()
    {
        // Left on 10 Nov: 10 of 30 days.
        var leaver = PayCalculator.Calculate(November(exit: new DateOnly(2026, 11, 10)))!;

        Assert.Equal(10, leaver.DaysEmployed);
        Assert.Equal(333.33m, leaver.Basic);
    }

    [Fact]
    public void Someone_not_employed_on_any_day_of_the_period_has_no_pay()
    {
        Assert.Null(PayCalculator.Calculate(November(joined: new DateOnly(2026, 12, 1))));
        Assert.Null(PayCalculator.Calculate(November(exit: new DateOnly(2026, 10, 31))));
    }

    [Fact]
    public void Overtime_is_paid_at_one_and_a_half_times_the_hourly_rate_and_unpaid_leave_is_deducted_at_the_daily_rate()
    {
        var pay = PayCalculator.Calculate(November(overtimeMinutes: 120, unpaidLeaveDays: 2m))!;

        // hourly = 1000 / (20 x 8) = 6.25; 2 h x 6.25 x 1.5 = 18.75
        var overtime = pay.Earnings.Single(line => line.Code == "OT");
        Assert.Equal(18.75m, overtime.Amount);
        Assert.Equal("Overtime pay", overtime.Name);
        Assert.Equal("2 h × 6.25 × 1.5", overtime.Basis);

        // daily = 1000 / 20 = 50; 2 days = 100
        var unpaid = pay.Deductions.Single(line => line.Code == "UL");
        Assert.Equal(100m, unpaid.Amount);
        Assert.Equal("Unpaid leave", unpaid.Name);
        Assert.Equal("2 day(s) × 50", unpaid.Basis);

        Assert.Equal(1168.75m, pay.Gross); // overtime is part of gross, so tax is taken on it
        Assert.Equal(58.44m, pay.Deductions.Single(line => line.Code == "TAX").Amount);
        Assert.Equal(pay.Gross - pay.TotalDeductions, pay.Net);
    }

    [Fact]
    public void Inactive_components_are_ignored_and_a_deduction_can_be_a_percentage_of_basic()
    {
        var inactiveTransport = Component("TRANSPORT", "Transport", ComponentType.Earning, CalculationMethod.Fixed, 50m, RecordStatus.Inactive);

        var pay = PayCalculator.Calculate(November([Housing, inactiveTransport, Pension]))!;

        Assert.Equal(["HOUSING"], pay.Earnings.Select(line => line.Code));
        Assert.Equal([new PayLineResult("PENSION", "Pension", 30m, "3% of basic")], pay.Deductions);
    }

    [Fact]
    public void Components_are_described_as_the_mock_describes_them()
    {
        Assert.Equal("10% of basic", PayrollRules.Describe(Housing.Calculation, Housing.Value));
        Assert.Equal("5% of gross", PayrollRules.Describe(Tax.Calculation, Tax.Value));
        Assert.Equal("Fixed 50", PayrollRules.Describe(Transport.Calculation, Transport.Value));
        Assert.Equal("2.5% of basic", PayrollRules.Describe(CalculationMethod.PercentOfBasic, 2.5m));
    }

    [Fact]
    public void Halves_round_away_from_zero_and_money_stays_decimal()
    {
        Assert.Equal(58.44m, PayrollRules.Round2(58.4375m));
        Assert.Equal(0.01m, PayrollRules.Round2(0.005m));
        Assert.Equal(333.33m, PayrollRules.Round2(1000m * 10 / 30));
        Assert.Equal(666.67m, PayrollRules.Round2(1000m * 20 / 30));
    }

    [Fact]
    public void A_period_without_working_days_has_no_overtime_or_unpaid_leave_line()
    {
        var pay = PayCalculator.Calculate(November() with { WorkingDays = 0, OvertimeMinutes = 120, UnpaidLeaveDays = 2m })!;

        Assert.DoesNotContain(pay.Earnings, line => line.Code == "OT");
        Assert.DoesNotContain(pay.Deductions, line => line.Code == "UL");
    }

    [Fact]
    public void The_net_can_be_below_zero_when_unpaid_leave_exceeds_the_pay()
    {
        var pay = PayCalculator.Calculate(November([Housing]) with { UnpaidLeaveDays = 25m })!;

        Assert.Equal(1100m - 1250m, pay.Net);
        Assert.Equal(1250m, pay.TotalDeductions);
    }

    // ---- collectPayrollAdjustments ----

    private static DateOnly Day(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateOnly End = new(2026, 11, 30);
    private static readonly IReadOnlySet<DayOfWeek> Weekend = new HashSet<DayOfWeek> { DayOfWeek.Saturday, DayOfWeek.Sunday };
    private static readonly IReadOnlySet<int> UnpaidTypes = new HashSet<int> { 6 };

    private static LeaveApplication Leave(
        int employeeId = 1,
        int typeId = 6,
        string start = "2026-11-02",
        string end = "2026-11-03",
        bool halfDay = false,
        bool approved = true)
    {
        var details = new LeaveApplicationDetails(employeeId, typeId, Day(start), Day(end), halfDay, "Family matter", null);
        var application = LeaveApplication.Create(details, 1m, "LV-1", Day(start)).Value;
        Assert.True(application.Submit(Day(start)).IsSuccess);
        if (approved)
        {
            Assert.True(application.Decide(LeaveDecision.Approve, new LeaveActor(9, null, "Approver"), null, Day(start)).IsSuccess);
        }

        return application;
    }

    private static OvertimeRecord Overtime(int employeeId = 1, string date = "2026-11-05", int minutes = 90, bool approved = true)
    {
        var day = Day(date);
        var overtime = OvertimeRecord.Create(
            new OvertimeDetails(employeeId, day, new TimeOnly(18, 0), new TimeOnly(18, 0).AddMinutes(minutes), "Month end close"),
            day).Value;
        if (approved)
        {
            Assert.True(overtime.Decide(ApprovalDecision.Approve, new ApprovalActor(9, null, "Approver"), null, day).IsSuccess);
        }

        return overtime;
    }

    private static PayrollAdjustments Collect(
        IEnumerable<LeaveApplication> leaves,
        IEnumerable<OvertimeRecord> overtime,
        IReadOnlySet<DateOnly>? holidays = null) =>
        PayrollAdjustments.Collect(1, Start, End, leaves, UnpaidTypes, overtime, holidays ?? new HashSet<DateOnly>(), Weekend);

    [Fact]
    public void Only_approved_unpaid_leave_of_the_employee_counts()
    {
        Assert.Equal(new PayrollAdjustments(2m, 0), Collect([Leave()], []));
        Assert.Equal(0m, Collect([Leave(typeId: 1)], []).UnpaidLeaveDays); // paid leave
        Assert.Equal(0m, Collect([Leave(approved: false)], []).UnpaidLeaveDays); // only submitted
        Assert.Equal(0m, Collect([Leave(employeeId: 2)], []).UnpaidLeaveDays); // somebody else
    }

    [Fact]
    public void Unpaid_leave_is_clamped_to_the_period_and_leaves_out_weekends_and_holidays()
    {
        // 26 Oct to 4 Nov: only 2, 3 and 4 Nov fall in the period (Monday to Wednesday).
        Assert.Equal(3m, Collect([Leave(start: "2026-10-26", end: "2026-11-04")], []).UnpaidLeaveDays);

        // 2 to 8 Nov: five working days, minus the holiday on 4 Nov.
        var holidays = new HashSet<DateOnly> { new(2026, 11, 4) };
        Assert.Equal(4m, Collect([Leave(start: "2026-11-02", end: "2026-11-08")], [], holidays).UnpaidLeaveDays);

        // Leave that ends before the period starts has no days in it.
        Assert.Equal(0m, Collect([Leave(start: "2026-10-12", end: "2026-10-14")], []).UnpaidLeaveDays);
    }

    [Fact]
    public void A_half_day_is_half_a_day()
    {
        Assert.Equal(0.5m, Collect([Leave(halfDay: true, start: "2026-11-02", end: "2026-11-02")], []).UnpaidLeaveDays);
    }

    [Fact]
    public void Only_approved_overtime_inside_the_period_is_summed()
    {
        var records = new[]
        {
            Overtime(),
            Overtime(date: "2026-11-12", minutes: 60),
            Overtime(date: "2026-10-31", minutes: 200),
            Overtime(approved: false, date: "2026-11-06", minutes: 500),
            Overtime(employeeId: 2, minutes: 700),
        };

        Assert.Equal(150, Collect([], records).OvertimeMinutes);
    }
}
