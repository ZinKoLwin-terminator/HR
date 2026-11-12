using System.Globalization;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Payroll;

/// <summary>The year and month of a payroll period and the day it is paid (the end of the month when not given).</summary>
public sealed record PayrollRunDetails(int Year, int Month, DateOnly? PayDate);

/// <summary>Who acts on a run: the signed-in account and the name to keep as it was at that time.</summary>
public sealed record PayrollActor(int UserId, string Name);

/// <summary>The sums of a processed run. Allowances are what gross has above basic (allowances and overtime).</summary>
public sealed record PayrollTotals(int EmployeeCount, decimal Basic, decimal Gross, decimal Deductions, decimal Net);

/// <summary>A note from processing (for example an employee skipped for a missing salary assignment).</summary>
public sealed class PayrollRunWarning : Entity
{
    public const int MessageMaxLength = 400;

    private PayrollRunWarning()
    {
    }

    internal PayrollRunWarning(string message, DateTime createdAtUtc)
    {
        Message = message.Length <= MessageMaxLength ? message : message[..MessageMaxLength];
        CreatedAtUtc = createdAtUtc;
    }

    public int PayrollRunId { get; private set; }

    public string Message { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }
}

/// <summary>
/// One payroll period (pay.PayrollRun), a calendar month, and its workflow:
/// Draft to Pending Approval (<see cref="Process"/>), Pending Approval to Approved (<see cref="Approve"/>), Approved to Paid
/// (<see cref="MarkPaid"/>), and Pending Approval or Approved back to Draft (<see cref="ReturnToDraft"/>). Paid is final.
/// A user never approves a run that user processed. All figures are DEMONSTRATION figures. The database locks an
/// approved run and its payslips (triggers); these methods refuse the same moves first, with a message.
/// </summary>
public sealed class PayrollRun : AuditedEntity
{
    public const int MinYear = 1900;
    public const int MaxYear = 9999;
    public const int UserNameMaxLength = 130;

    private readonly List<PayrollRunWarning> warnings = [];

    private PayrollRun()
    {
    }

    public string PeriodName { get; private set; } = string.Empty;

    public int Year { get; private set; }

    /// <summary>1 to 12.</summary>
    public int Month { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public DateOnly PayDate { get; private set; }

    public PayrollRunStatus Status { get; private set; }

    public int EmployeeCount { get; private set; }

    /// <summary>Basic salary payable after proration.</summary>
    public decimal TotalBasic { get; private set; }

    /// <summary>Earnings above basic (allowances, overtime). Gross is basic plus allowances.</summary>
    public decimal TotalAllowances { get; private set; }

    public decimal TotalGross { get; private set; }

    public decimal TotalDeductions { get; private set; }

    public decimal TotalNet { get; private set; }

    public DateOnly? ProcessedOn { get; private set; }

    public int? ProcessedByUserId { get; private set; }

    public string? ProcessedByName { get; private set; }

    public int? ApprovedByUserId { get; private set; }

    public string? ApprovedByName { get; private set; }

    public DateOnly? ApprovedOn { get; private set; }

    public DateOnly? PaidOn { get; private set; }

    /// <summary>Notes from the last processing. Empty again once the run is back in Draft.</summary>
    public IReadOnlyCollection<PayrollRunWarning> Warnings => warnings;

    /// <summary>The first day of the month of <paramref name="year"/> and <paramref name="month"/>.</summary>
    public static DateOnly StartOf(int year, int month) => new(year, month, 1);

    /// <summary>"November 2026".</summary>
    public static string NameOf(DateOnly start) => start.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(PayrollRunDetails details, DateOnly today)
    {
        var violations = new Violations();
        if (details.Year is < MinYear or > MaxYear)
        {
            return violations.Add("year", $"Enter a year from {MinYear} to {MaxYear}.").ToResult();
        }

        if (details.Month is < 1 or > 12)
        {
            return violations.Add("month", "Choose a month.").ToResult();
        }

        var start = StartOf(details.Year, details.Month);
        violations.AddIf(
            start > StartOf(today.Year, today.Month),
            "month",
            "Choose the current or an earlier month.",
            "A payroll period cannot be in the future.");
        violations.AddIf(
            details.PayDate is { } payDate && payDate < start,
            "payDate",
            "Must be on or after the period start.",
            "The pay date cannot be before the period starts.");
        return violations.ToResult();
    }

    /// <summary>A new period in Draft with no figures.</summary>
    public static Result<PayrollRun> Create(PayrollRunDetails details, DateOnly today)
    {
        var valid = Validate(details, today);
        if (valid.IsFailure)
        {
            return Result.Failure<PayrollRun>(valid.Error);
        }

        var run = new PayrollRun { Status = PayrollRunStatus.Draft };
        run.ApplyPeriod(details);
        return Result.Success(run);
    }

    /// <summary>Changes the period and the pay date of a draft.</summary>
    public Result Update(PayrollRunDetails details, DateOnly today)
    {
        var allowed = EnsureDraftForChange();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var valid = Validate(details, today);
        if (valid.IsFailure)
        {
            return valid;
        }

        ApplyPeriod(details);
        return Result.Success();
    }

    /// <summary>The status rule of changing a period: only a draft.</summary>
    public Result EnsureDraftForChange() =>
        Status != PayrollRunStatus.Draft
            ? Result.Failure(AppError.Conflict("Only a draft period can be changed. Return it to draft first."))
            : Result.Success();

    /// <summary>Only a draft period can be deleted.</summary>
    public Result EnsureCanDelete() =>
        Status != PayrollRunStatus.Draft
            ? Result.Failure(AppError.Conflict("Only a draft period can be deleted."))
            : Result.Success();

    /// <summary>The status rule of processing: only a draft. Whether earlier periods are finished is the caller's rule (it needs other runs).</summary>
    public Result EnsureCanProcess() =>
        Status != PayrollRunStatus.Draft
            ? Result.Failure(AppError.Conflict($"Only a draft period can be processed (this one is {StatusText})."))
            : Result.Success();

    /// <summary>
    /// Draft to Pending Approval: keeps the sums, the notes and who processed the run and when. The payslips are added by
    /// the caller, in the same save.
    /// </summary>
    public Result Process(PayrollActor actor, PayrollTotals totals, IReadOnlyList<string> notes, DateOnly today, DateTime nowUtc)
    {
        var allowed = EnsureCanProcess();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        Status = PayrollRunStatus.PendingApproval;
        EmployeeCount = totals.EmployeeCount;
        TotalBasic = totals.Basic;
        TotalAllowances = PayrollRules.Round2(totals.Gross - totals.Basic);
        TotalGross = totals.Gross;
        TotalDeductions = totals.Deductions;
        TotalNet = totals.Net;
        ProcessedOn = today;
        ProcessedByUserId = actor.UserId;
        ProcessedByName = Limit(actor.Name);
        ApprovedByUserId = null;
        ApprovedByName = null;
        ApprovedOn = null;
        PaidOn = null;
        warnings.AddRange(notes.Select(note => new PayrollRunWarning(note, nowUtc)));
        return Result.Success();
    }

    /// <summary>
    /// Pending Approval to Approved. The user who processed the run cannot approve it (the database refuses it too, with a
    /// check constraint, but the caller gets a clear message here first).
    /// </summary>
    public Result Approve(PayrollActor actor, DateOnly today)
    {
        if (Status != PayrollRunStatus.PendingApproval)
        {
            return Result.Failure(AppError.Conflict(
                $"Only a period pending approval can be approved (this one is {StatusText})."));
        }

        if (ProcessedByUserId == actor.UserId)
        {
            return Result.Failure(AppError.Conflict(
                "You processed this payroll period, so you cannot approve it. Another approver must do it."));
        }

        Status = PayrollRunStatus.Approved;
        ApprovedByUserId = actor.UserId;
        ApprovedByName = Limit(actor.Name);
        ApprovedOn = today;
        return Result.Success();
    }

    /// <summary>
    /// Approved to Paid. Records that payment happened: no money moves and no bank is contacted. Only the status and the
    /// paid date change (the database refuses any other change of an approved run being paid).
    /// </summary>
    public Result MarkPaid(DateOnly today)
    {
        if (Status != PayrollRunStatus.Approved)
        {
            return Result.Failure(AppError.Conflict(
                $"Only an approved period can be marked as paid (this one is {StatusText})."));
        }

        Status = PayrollRunStatus.Paid;
        PaidOn = today;
        return Result.Success();
    }

    /// <summary>
    /// Pending Approval or Approved back to Draft: the figures and milestones are cleared. The payslips and the notes are
    /// removed by the caller in a later statement of the same transaction (the database allows that only once the run is
    /// Draft), so this method leaves the warning collection alone: clearing it here would make the change tracker delete
    /// the notes in the same save, possibly before the run is updated.
    /// </summary>
    public Result ReturnToDraft()
    {
        if (Status is not (PayrollRunStatus.PendingApproval or PayrollRunStatus.Approved))
        {
            return Result.Failure(AppError.Conflict(
                $"Only a period pending approval or approved can be returned to draft (this one is {StatusText})."));
        }

        Status = PayrollRunStatus.Draft;
        EmployeeCount = 0;
        TotalBasic = 0;
        TotalAllowances = 0;
        TotalGross = 0;
        TotalDeductions = 0;
        TotalNet = 0;
        ProcessedOn = null;
        ProcessedByUserId = null;
        ProcessedByName = null;
        ApprovedByUserId = null;
        ApprovedByName = null;
        ApprovedOn = null;
        PaidOn = null;
        return Result.Success();
    }

    private string StatusText => PayrollRunStatusText.ToText(Status);

    private static string Limit(string name) => name.Length <= UserNameMaxLength ? name : name[..UserNameMaxLength];

    private void ApplyPeriod(PayrollRunDetails details)
    {
        Year = details.Year;
        Month = details.Month;
        StartDate = StartOf(details.Year, details.Month);
        EndDate = StartDate.AddMonths(1).AddDays(-1);
        PeriodName = NameOf(StartDate);
        PayDate = details.PayDate ?? EndDate;
    }
}
