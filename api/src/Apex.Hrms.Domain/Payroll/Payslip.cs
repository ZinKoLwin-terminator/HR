using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Payroll;

/// <summary>
/// What a payslip copies from the employee and the organization at processing time, so that later changes do not alter it.
/// <see cref="AccountMasked"/> is the masked bank account (see <see cref="Payslip.MaskAccount"/>): the full number is never copied.
/// </summary>
public sealed record PayslipSnapshot(
    int EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    int DepartmentId,
    string DepartmentName,
    string PositionTitle,
    string CompanyName,
    string BankName,
    string AccountMasked);

/// <summary>One earning or deduction line of a payslip (pay.PayslipLine), a snapshot of how an amount was worked out.</summary>
public sealed class PayslipLine : Entity
{
    private PayslipLine()
    {
    }

    internal PayslipLine(PayLineType type, short sortOrder, PayLineResult line)
    {
        LineType = type;
        SortOrder = sortOrder;
        Code = line.Code;
        Name = line.Name;
        Amount = line.Amount;
        Basis = line.Basis;
    }

    public int PayslipId { get; private set; }

    public PayLineType LineType { get; private set; }

    public short SortOrder { get; private set; }

    /// <summary>The component code, or OT (overtime) or UL (unpaid leave).</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    /// <summary>How the amount was worked out, for example "10% of basic".</summary>
    public string Basis { get; private set; } = string.Empty;
}

/// <summary>
/// One employee's pay for one run (pay.Payslip), read-only once produced: it is created by processing a run and removed
/// only when the run returns to Draft. DEMONSTRATION figures. The payslip carries salary figures, which are personal
/// data, and the masked account number only.
/// </summary>
public sealed class Payslip : Entity
{
    public const string MaskPrefix = "•••• ";

    private readonly List<PayslipLine> lines = [];

    private Payslip()
    {
    }

    public int PayrollRunId { get; private set; }

    public string PeriodName { get; private set; } = string.Empty;

    public int EmployeeId { get; private set; }

    public string EmployeeCode { get; private set; } = string.Empty;

    public string EmployeeName { get; private set; } = string.Empty;

    public int DepartmentId { get; private set; }

    public string DepartmentName { get; private set; } = string.Empty;

    public string PositionTitle { get; private set; } = string.Empty;

    public string CompanyName { get; private set; } = string.Empty;

    public string BankName { get; private set; } = string.Empty;

    /// <summary>Four bullets, a space and the last four digits of the account number.</summary>
    public string AccountMasked { get; private set; } = string.Empty;

    /// <summary>Basic after proration for part-month employment.</summary>
    public decimal BasicSalary { get; private set; }

    public decimal GrossSalary { get; private set; }

    public decimal TotalDeductions { get; private set; }

    public decimal NetSalary { get; private set; }

    public int PeriodDays { get; private set; }

    public int DaysEmployed { get; private set; }

    public int WorkingDays { get; private set; }

    public decimal UnpaidLeaveDays { get; private set; }

    public int OvertimeMinutes { get; private set; }

    public DateOnly GeneratedOn { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public int? CreatedByUserId { get; private set; }

    public IReadOnlyCollection<PayslipLine> Lines => lines;

    public IReadOnlyList<PayslipLine> Earnings => LinesOf(PayLineType.Earning);

    public IReadOnlyList<PayslipLine> Deductions => LinesOf(PayLineType.Deduction);

    /// <summary>"•••• 4821": four bullets, a space and the last four digits. Fails for a number with fewer than four digits.</summary>
    public static Result<string> MaskAccount(string? accountNumber)
    {
        var digits = accountNumber?.Trim() ?? string.Empty;
        return digits.Length >= 4 && digits[^4..].All(char.IsAsciiDigit)
            ? Result.Success(MaskPrefix + digits[^4..])
            : Result.Failure<string>(AppError.Validation(
                "The bank account number has fewer than four digits.",
                "accountNumber",
                "Enter a valid account number."));
    }

    /// <summary>Builds the payslip of one employee for <paramref name="run"/> from the calculated pay.</summary>
    public static Payslip Create(
        PayrollRun run,
        PayslipSnapshot snapshot,
        PayResult pay,
        int workingDays,
        PayrollAdjustments adjustments,
        DateOnly today,
        DateTime nowUtc,
        int? createdByUserId)
    {
        var payslip = new Payslip
        {
            PayrollRunId = run.Id,
            PeriodName = run.PeriodName,
            EmployeeId = snapshot.EmployeeId,
            EmployeeCode = snapshot.EmployeeCode,
            EmployeeName = snapshot.EmployeeName,
            DepartmentId = snapshot.DepartmentId,
            DepartmentName = snapshot.DepartmentName,
            PositionTitle = snapshot.PositionTitle,
            CompanyName = snapshot.CompanyName,
            BankName = snapshot.BankName,
            AccountMasked = snapshot.AccountMasked,
            BasicSalary = pay.Basic,
            GrossSalary = pay.Gross,
            TotalDeductions = pay.TotalDeductions,
            NetSalary = pay.Net,
            PeriodDays = pay.PeriodDays,
            DaysEmployed = pay.DaysEmployed,
            WorkingDays = workingDays,
            UnpaidLeaveDays = adjustments.UnpaidLeaveDays,
            OvertimeMinutes = adjustments.OvertimeMinutes,
            GeneratedOn = today,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = createdByUserId,
        };
        for (var index = 0; index < pay.Earnings.Count; index++)
        {
            payslip.lines.Add(new PayslipLine(PayLineType.Earning, (short)(index + 1), pay.Earnings[index]));
        }

        for (var index = 0; index < pay.Deductions.Count; index++)
        {
            payslip.lines.Add(new PayslipLine(PayLineType.Deduction, (short)(index + 1), pay.Deductions[index]));
        }

        return payslip;
    }

    private List<PayslipLine> LinesOf(PayLineType type) =>
        [.. lines.Where(line => line.LineType == type).OrderBy(line => line.SortOrder)];
}
