using System.Globalization;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Domain.Payroll;

/// <summary>
/// DEMONSTRATION pay rules, ported from the frontend (payroll-calc.ts). They are NOT real tax, social security or labour
/// law calculations: the percentages come from the salary components an administrator configures, and no jurisdiction
/// has signed them off. All money is <see cref="decimal"/>.
/// </summary>
public static class PayrollRules
{
    /// <summary>Overtime is paid at this multiple of the hourly rate.</summary>
    public const decimal OvertimeMultiplier = 1.5m;

    public const int HoursPerWorkingDay = 8;

    public const string OvertimeCode = "OT";

    public const string UnpaidLeaveCode = "UL";

    /// <summary>Two decimals, halves rounded away from zero (as the frontend's <c>round2</c>).</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>"10% of basic", "2% of gross" or "Fixed 50".</summary>
    public static string Describe(CalculationMethod calculation, decimal value) => calculation switch
    {
        CalculationMethod.PercentOfBasic => $"{Plain(value)}% of basic",
        CalculationMethod.PercentOfGross => $"{Plain(value)}% of gross",
        _ => $"Fixed {Plain(value)}",
    };

    /// <summary>A number without trailing zeros, as JavaScript prints it: 50, 6.25, 0.5.</summary>
    internal static string Plain(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>What the calculation of one employee's pay for one period needs.</summary>
/// <param name="BasicSalary">The full-month basic salary.</param>
/// <param name="Components">The components of the employee's structure, in display order. Inactive ones are ignored.</param>
/// <param name="WorkingDays">Working days of the period (weekends and holidays left out).</param>
/// <param name="UnpaidLeaveDays">Approved unpaid leave inside the period, in days.</param>
/// <param name="OvertimeMinutes">Approved overtime inside the period.</param>
public sealed record PayInput(
    decimal BasicSalary,
    IReadOnlyList<SalaryComponent> Components,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly JoiningDate,
    DateOnly? ExitDate,
    int WorkingDays,
    decimal UnpaidLeaveDays,
    int OvertimeMinutes);

/// <summary>One line of a payslip as calculated; <see cref="Basis"/> says how the amount was worked out.</summary>
public sealed record PayLineResult(string Code, string Name, decimal Amount, string Basis);

public sealed record PayResult(
    decimal Basic,
    IReadOnlyList<PayLineResult> Earnings,
    IReadOnlyList<PayLineResult> Deductions,
    decimal Gross,
    decimal TotalDeductions,
    decimal Net,
    int PeriodDays,
    int DaysEmployed);

/// <summary>
/// The demonstration pay calculation for one employee and period:
/// <list type="number">
/// <item>Part-month employment (joiners and leavers) prorates basic and fixed components by calendar days.</item>
/// <item>Earnings: fixed amounts (prorated) or a percentage of the prorated basic. Approved overtime is added at
/// hourly rate x 1.5, where hourly = full basic / (working days x 8).</item>
/// <item>Gross = basic + earnings.</item>
/// <item>Deductions: fixed amounts (prorated), a percentage of basic or a percentage of gross, plus unpaid leave at the
/// daily rate (full basic / working days).</item>
/// <item>Net = gross - deductions.</item>
/// </list>
/// Every amount is rounded to two decimals.
/// </summary>
public static class PayCalculator
{
    /// <summary>The pay of the employee, or null when the employee was not employed on any day of the period.</summary>
    public static PayResult? Calculate(PayInput input)
    {
        var periodDays = input.PeriodEnd.DayNumber - input.PeriodStart.DayNumber + 1;
        var from = input.JoiningDate > input.PeriodStart ? input.JoiningDate : input.PeriodStart;
        var to = input.ExitDate is { } exit && exit < input.PeriodEnd ? exit : input.PeriodEnd;
        var daysEmployed = Math.Max(0, to.DayNumber - from.DayNumber + 1);
        if (daysEmployed == 0)
        {
            return null;
        }

        decimal Prorate(decimal amount) => amount * daysEmployed / periodDays;

        var basic = PayrollRules.Round2(Prorate(input.BasicSalary));

        var earnings = new List<PayLineResult>();
        foreach (var component in input.Components.Where(c => c.Type == ComponentType.Earning && c.IsActive))
        {
            var amount = component.Calculation == CalculationMethod.Fixed
                ? Prorate(component.Value)
                : basic * component.Value / 100;
            earnings.Add(new PayLineResult(
                component.Code,
                component.Name,
                PayrollRules.Round2(amount),
                PayrollRules.Describe(component.Calculation, component.Value)));
        }

        if (input.OvertimeMinutes > 0 && input.WorkingDays > 0)
        {
            var hours = input.OvertimeMinutes / 60m;
            var hourly = input.BasicSalary / (input.WorkingDays * PayrollRules.HoursPerWorkingDay);
            var amount = input.BasicSalary * input.OvertimeMinutes * PayrollRules.OvertimeMultiplier
                / (input.WorkingDays * PayrollRules.HoursPerWorkingDay * 60m);
            earnings.Add(new PayLineResult(
                PayrollRules.OvertimeCode,
                "Overtime pay",
                PayrollRules.Round2(amount),
                $"{PayrollRules.Plain(PayrollRules.Round2(hours))} h × {PayrollRules.Plain(PayrollRules.Round2(hourly))} × {PayrollRules.Plain(PayrollRules.OvertimeMultiplier)}"));
        }

        var gross = PayrollRules.Round2(basic + earnings.Sum(line => line.Amount));

        var deductions = new List<PayLineResult>();
        foreach (var component in input.Components.Where(c => c.Type == ComponentType.Deduction && c.IsActive))
        {
            var amount = component.Calculation switch
            {
                CalculationMethod.Fixed => Prorate(component.Value),
                CalculationMethod.PercentOfGross => gross * component.Value / 100,
                _ => basic * component.Value / 100,
            };
            deductions.Add(new PayLineResult(
                component.Code,
                component.Name,
                PayrollRules.Round2(amount),
                PayrollRules.Describe(component.Calculation, component.Value)));
        }

        if (input.UnpaidLeaveDays > 0 && input.WorkingDays > 0)
        {
            var daily = input.BasicSalary / input.WorkingDays;
            deductions.Add(new PayLineResult(
                PayrollRules.UnpaidLeaveCode,
                "Unpaid leave",
                PayrollRules.Round2(input.UnpaidLeaveDays * input.BasicSalary / input.WorkingDays),
                $"{PayrollRules.Plain(input.UnpaidLeaveDays)} day(s) × {PayrollRules.Plain(PayrollRules.Round2(daily))}"));
        }

        var totalDeductions = PayrollRules.Round2(deductions.Sum(line => line.Amount));
        return new PayResult(
            basic,
            earnings,
            deductions,
            gross,
            totalDeductions,
            PayrollRules.Round2(gross - totalDeductions),
            periodDays,
            daysEmployed);
    }
}

/// <summary>Unpaid-leave days and approved overtime of one employee inside a period (collectPayrollAdjustments in the frontend).</summary>
public sealed record PayrollAdjustments(decimal UnpaidLeaveDays, int OvertimeMinutes)
{
    /// <summary>
    /// Only approved leave of an unpaid type counts, clamped to the period: a half day is 0.5, otherwise the working days
    /// between the clamped dates (weekends and holidays left out). Only approved overtime dated inside the period counts.
    /// </summary>
    public static PayrollAdjustments Collect(
        int employeeId,
        DateOnly periodStart,
        DateOnly periodEnd,
        IEnumerable<LeaveApplication> leaves,
        IReadOnlySet<int> unpaidLeaveTypeIds,
        IEnumerable<OvertimeRecord> overtime,
        IReadOnlySet<DateOnly> holidays,
        IReadOnlySet<DayOfWeek> weekendDays)
    {
        var unpaidLeaveDays = 0m;
        foreach (var leave in leaves)
        {
            if (leave.EmployeeId != employeeId
                || leave.Status != LeaveStatus.Approved
                || !unpaidLeaveTypeIds.Contains(leave.LeaveTypeId))
            {
                continue;
            }

            var start = leave.StartDate > periodStart ? leave.StartDate : periodStart;
            var end = leave.EndDate < periodEnd ? leave.EndDate : periodEnd;
            if (start <= end)
            {
                unpaidLeaveDays += leave.HalfDay ? LeaveApplication.HalfDayDays : WorkingDays.Count(start, end, weekendDays, holidays);
            }
        }

        var overtimeMinutes = overtime
            .Where(entry => entry.EmployeeId == employeeId
                && entry.Status == ApprovalStatus.Approved
                && entry.Date >= periodStart
                && entry.Date <= periodEnd)
            .Sum(entry => entry.Minutes);
        return new PayrollAdjustments(unpaidLeaveDays, overtimeMinutes);
    }
}
