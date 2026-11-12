using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Runs;

/// <summary>
/// Mirrors the PayrollRun interface in payroll.models.ts. ALL FIGURES ARE DEMONSTRATION FIGURES produced from configurable
/// sample rules: they are not statutory tax, social security or banking calculations. The totals are salary data
/// (personal data in aggregate): only callers holding payroll:View receive them (the payroll routes and the dashboard payroll
/// summary both check it). Payroll runs are not row-scoped.
/// </summary>
/// <param name="TotalBasic">Basic salary actually payable (after proration).</param>
/// <param name="TotalAllowances">Earnings above basic (allowances, overtime). <c>totalGross = totalBasic + totalAllowances</c>.</param>
/// <param name="Warnings">Notes from processing (employees, named by employee code only, skipped for a missing salary assignment or bank account). Empty in Draft.</param>
/// <param name="ProcessedBy">Display name of the user who processed the run, as it was then. The same user cannot approve it.</param>
/// <param name="ApprovedBy">Display name of the user who approved the run, as it was then.</param>
public sealed record PayrollRunDto(
    int Id,
    string PeriodName,
    int Year,
    int Month,
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PayDate,
    string Status,
    int EmployeeCount,
    decimal TotalBasic,
    decimal TotalAllowances,
    decimal TotalGross,
    decimal TotalDeductions,
    decimal TotalNet,
    IReadOnlyList<string> Warnings,
    DateOnly? ProcessedOn,
    string? ProcessedBy,
    string? ApprovedBy,
    DateOnly? ApprovedOn,
    DateOnly? PaidOn,
    byte[] RowVersion) : IIdentified;

/// <summary>
/// POST body: the period (year and month; not in the future, one run per month). The run starts in Draft with no figures.
/// <c>payDate</c> defaults to the last day of the month and cannot be before the period starts.
/// </summary>
public sealed record PayrollRunCreateRequest(int Year, int Month, DateOnly? PayDate)
    : ICreateRequest<PayrollRunDetails>
{
    public PayrollRunDetails ToDetails() => new(Year, Month, PayDate);

    // A run always starts in Draft; the shared create flow asks for a status and the factory ignores it.
    public RecordStatus ParseStatus() => RecordStatus.Active;
}

/// <summary>PUT body: changes the period or the pay date of a Draft run. Requires the row version.</summary>
public sealed record PayrollRunUpdateRequest(int Year, int Month, DateOnly? PayDate, string? RowVersion)
    : IDetailsRequest<PayrollRunDetails>, IVersionedRequest
{
    public PayrollRunDetails ToDetails() => new(Year, Month, PayDate);
}

public sealed record PayrollRunFilter(string? Status = null, int? Year = null);

// ---- Commands (the status never changes through PUT) ----

/// <summary>Draft to Pending Approval: calculates a payslip for everyone employed in the period.</summary>
public sealed record ProcessPayrollRunCommand(int Id, string? RowVersion) : IEntityCommand<ProcessPayrollRunCommand>
{
    public static ProcessPayrollRunCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>Pending Approval to Approved. The user who processed the run cannot approve it.</summary>
public sealed record ApprovePayrollRunCommand(int Id, string? RowVersion) : IEntityCommand<ApprovePayrollRunCommand>
{
    public static ApprovePayrollRunCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>Approved to Paid. Sample only: it records that payment happened; no money moves and no bank is contacted.</summary>
public sealed record MarkPayrollRunPaidCommand(int Id, string? RowVersion) : IEntityCommand<MarkPayrollRunPaidCommand>
{
    public static MarkPayrollRunPaidCommand From(int id, string? rowVersion) => new(id, rowVersion);
}

/// <summary>Pending Approval or Approved back to Draft; the generated payslips and warnings are removed. The reason is optional (at most 200 characters) and goes into the activity entry.</summary>
public sealed record ReturnPayrollRunToDraftCommand(int Id, string? RowVersion, string? Reason) : IEntityCommand;
