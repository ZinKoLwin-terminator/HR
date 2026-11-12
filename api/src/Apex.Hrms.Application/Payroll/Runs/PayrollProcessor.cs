using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Payroll.Runs;

/// <summary>What processing a run produced: the payslips and the notes. Nothing is saved yet.</summary>
internal sealed record ProcessedPayroll(IReadOnlyList<Payslip> Payslips, IReadOnlyList<string> Warnings, PayrollTotals Totals);

/// <summary>
/// Works out the payslips of a payroll period (the frontend's <c>PayrollRunMockService.process</c>), with the DEMONSTRATION
/// calculation of <see cref="PayCalculator"/>. For everyone employed on any day of the period:
/// the salary assignment in force at the end of the period and its structure give the components; approved unpaid leave
/// and approved overtime inside the period are collected; the pay is calculated and a payslip made, with the department,
/// position, company and bank name copied and the bank account number masked. An employee without an assignment, or
/// without a bank account to show on the payslip (the table requires one), is skipped with a note.
/// The full bank account number is read only to mask it: it is not kept, logged or returned.
/// </summary>
internal sealed class PayrollProcessor(
    IRepository<PayrollRun> runs,
    IRepository<Employee> employees,
    IRepository<SalaryAssignment> assignments,
    IRepository<SalaryStructure> structures,
    IRepository<SalaryComponent> components,
    IRepository<Department> departments,
    IRepository<Position> positions,
    IRepository<Company> companies,
    IRepository<Holiday> holidays,
    IRepository<LeaveType> leaveTypes,
    IRepository<LeaveApplication> leaves,
    IRepository<OvertimeRecord> overtime,
    IBusinessWeek week,
    IClock clock)
{
    private const string Unknown = "—";

    /// <summary>Earlier periods must be finished first: a Draft or Pending Approval period that starts before this one blocks it.</summary>
    public async Task<Result> EnsureEarlierPeriodsFinishedAsync(PayrollRun run, CancellationToken cancellationToken)
    {
        var start = run.StartDate;
        var earlier = (await runs.ListAsync(
                other => other.StartDate < start
                    && (other.Status == PayrollRunStatus.Draft || other.Status == PayrollRunStatus.PendingApproval),
                cancellationToken))
            .OrderBy(other => other.StartDate)
            .FirstOrDefault();
        return earlier is null
            ? Result.Success()
            : Result.Failure(AppError.Conflict(
                $"Finish {earlier.PeriodName} ({PayrollRunStatusText.ToText(earlier.Status)}) before processing {run.PeriodName}."));
    }

    public async Task<Result<ProcessedPayroll>> ProcessAsync(PayrollRun run, int? actorUserId, CancellationToken cancellationToken)
    {
        var start = run.StartDate;
        var end = run.EndDate;

        var holidayDates = (await holidays.ListAsync(holiday => holiday.Date >= start && holiday.Date <= end, cancellationToken))
            .Select(holiday => holiday.Date)
            .ToHashSet();
        var workingDays = WorkingDays.Count(start, end, week.WeekendDays, holidayDates);

        var unpaidTypeIds = (await leaveTypes.ListAsync(type => !type.IsPaid, cancellationToken))
            .Select(type => type.Id)
            .ToHashSet();
        var unpaidLeave = unpaidTypeIds.Count == 0
            ? []
            : await leaves.ListAsync(
                leave => leave.Status == LeaveStatus.Approved
                    && leave.StartDate <= end
                    && leave.EndDate >= start
                    && unpaidTypeIds.Contains(leave.LeaveTypeId),
                cancellationToken);
        var approvedOvertime = await overtime.ListAsync(
            entry => entry.Status == ApprovalStatus.Approved && entry.Date >= start && entry.Date <= end,
            cancellationToken);

        var staff = await employees.ListAsync(
            employee => employee.JoiningDate <= end && (employee.ExitDate == null || employee.ExitDate >= start),
            cancellationToken);
        var latestAssignment = (await assignments.ListAsync(assignment => assignment.EffectiveFrom <= end, cancellationToken))
            .GroupBy(assignment => assignment.EmployeeId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.EffectiveFrom).First());
        var structureById = (await structures.ListAsync(Predicates.Always<SalaryStructure>(), cancellationToken))
            .ToDictionary(structure => structure.Id);
        var componentById = (await components.ListAsync(Predicates.Always<SalaryComponent>(), cancellationToken))
            .ToDictionary(component => component.Id);
        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken))
            .ToDictionary(department => department.Id, department => department.Name);
        var positionTitles = (await positions.ListAsync(Predicates.Always<Position>(), cancellationToken))
            .ToDictionary(position => position.Id, position => position.Title);
        var companyNames = (await companies.ListAsync(Predicates.Always<Company>(), cancellationToken))
            .ToDictionary(company => company.Id, company => company.Name);

        var payslips = new List<Payslip>();
        var warnings = new List<string>();
        var today = clock.Today;
        var nowUtc = clock.UtcNow.UtcDateTime;

        foreach (var employee in staff)
        {
            var label = $"{employee.EmployeeCode} {employee.FirstName} {employee.LastName}";
            if (!latestAssignment.TryGetValue(employee.Id, out var assignment)
                || !structureById.TryGetValue(assignment.SalaryStructureId, out var structure))
            {
                warnings.Add($"{label} has no salary assignment and was skipped.");
                continue;
            }

            var structureComponents = structure.ComponentIds
                .Select(id => componentById.GetValueOrDefault(id))
                .OfType<SalaryComponent>()
                .ToList();
            var adjustments = PayrollAdjustments.Collect(
                employee.Id, start, end, unpaidLeave, unpaidTypeIds, approvedOvertime, holidayDates, week.WeekendDays);
            var pay = PayCalculator.Calculate(new PayInput(
                assignment.BasicSalary,
                structureComponents,
                start,
                end,
                employee.JoiningDate,
                employee.ExitDate,
                workingDays,
                adjustments.UnpaidLeaveDays,
                adjustments.OvertimeMinutes));
            if (pay is null)
            {
                continue;
            }

            var masked = employee.BankAccount is { } account ? Payslip.MaskAccount(account.AccountNumber) : null;
            if (masked is null || masked.IsFailure)
            {
                warnings.Add($"{label} has no usable bank account and was skipped.");
                continue;
            }

            var snapshot = new PayslipSnapshot(
                employee.Id,
                employee.EmployeeCode,
                $"{employee.FirstName} {employee.LastName}",
                employee.DepartmentId,
                departmentNames.GetValueOrDefault(employee.DepartmentId, Unknown),
                positionTitles.GetValueOrDefault(employee.PositionId, Unknown),
                companyNames.GetValueOrDefault(employee.CompanyId, Unknown),
                employee.BankAccount!.BankName,
                masked.Value);
            payslips.Add(Payslip.Create(run, snapshot, pay, workingDays, adjustments, today, nowUtc, actorUserId));
        }

        if (payslips.Count == 0)
        {
            return Result.Failure<ProcessedPayroll>(AppError.Conflict("There are no employees to pay in this period."));
        }

        var totals = new PayrollTotals(
            payslips.Count,
            payslips.Sum(payslip => payslip.BasicSalary),
            payslips.Sum(payslip => payslip.GrossSalary),
            payslips.Sum(payslip => payslip.TotalDeductions),
            payslips.Sum(payslip => payslip.NetSalary));
        return Result.Success(new ProcessedPayroll(payslips, warnings, totals));
    }
}
