using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Employees.Salary;

/// <summary>
/// Mirrors EmployeeSalarySummary in employee-extras.models.ts. DEMONSTRATION figures: a full month at the current salary
/// assignment worked out with the sample pay rules, not real payroll, tax or statutory pay. A salary is personal data:
/// the route needs payroll:View, only payroll:Approve sees another employee's figures, and nothing here is logged.
/// </summary>
/// <param name="BandPosition">0 to 100: where the basic salary sits inside the job-grade band (0 when the band has no width).</param>
/// <param name="Assumptions">Plain-language description of the sample rules behind the figures.</param>
public sealed record EmployeeSalarySummaryDto(
    int EmployeeId,
    decimal Basic,
    decimal Allowances,
    decimal Gross,
    decimal Deductions,
    decimal Net,
    string GradeCode,
    string GradeName,
    decimal BandMin,
    decimal BandMax,
    int BandPosition,
    IReadOnlyList<string> Assumptions);

/// <summary>GET /api/employees/{id}/salary-summary; the id is the employee id.</summary>
internal sealed class EmployeeSalarySummaryHandler(
    EntityLoader<Employee> employees,
    Apex.Hrms.Application.Security.SelfService self,
    IRepository<SalaryAssignment> assignments,
    IRepository<SalaryStructure> structures,
    IRepository<SalaryComponent> components,
    IRepository<JobGrade> grades,
    IRepository<Holiday> holidays,
    IBusinessWeek week,
    IClock clock) : IGetByIdHandler<EmployeeSalarySummaryDto>
{
    private const string Unknown = "—";

    public async Task<Result<EmployeeSalarySummaryDto>> HandleAsync(int employeeId, CancellationToken cancellationToken)
    {
        var loaded = await employees.LoadAsync(employeeId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<EmployeeSalarySummaryDto>(loaded.Error);
        }

        var employee = loaded.Value;
        if (!self.CanSeeSalaryOf(employeeId))
        {
            // Salary figures of another employee need payroll:Approve; outside that rule the employee looks missing, like any scoped record.
            return Result.Failure<EmployeeSalarySummaryDto>(NotFoundError.For("Employee", employeeId));
        }

        var today = clock.Today;
        var assignment = (await assignments.ListAsync(
                a => a.EmployeeId == employeeId && a.EffectiveFrom <= today,
                cancellationToken))
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefault();
        var structure = assignment is null ? null : await structures.FindAsync(assignment.SalaryStructureId, cancellationToken);
        if (assignment is null || structure is null)
        {
            return Result.Failure<EmployeeSalarySummaryDto>(AppError.NotFound(
                $"{employee.FirstName} {employee.LastName} has no salary assignment yet."));
        }

        var componentIds = structure.ComponentIds;
        var byId = (await components.ListAsync(c => componentIds.Contains(c.Id), cancellationToken))
            .ToDictionary(c => c.Id);
        var structureComponents = componentIds.Select(id => byId.GetValueOrDefault(id)).OfType<SalaryComponent>().ToList();

        var periodStart = new DateOnly(today.Year, today.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var holidayDates = (await holidays.ListAsync(h => h.Date >= periodStart && h.Date <= periodEnd, cancellationToken))
            .Select(h => h.Date)
            .ToHashSet();

        // A full month at the current salary: no proration, overtime or unpaid leave.
        var pay = PayCalculator.Calculate(new PayInput(
            assignment.BasicSalary,
            structureComponents,
            periodStart,
            periodEnd,
            new DateOnly(1900, 1, 1),
            ExitDate: null,
            WorkingDays.Count(periodStart, periodEnd, week.WeekendDays, holidayDates),
            UnpaidLeaveDays: 0,
            OvertimeMinutes: 0))!;

        var grade = await grades.FindAsync(employee.JobGradeId, cancellationToken);
        var min = grade?.MinSalary ?? assignment.BasicSalary;
        var max = grade?.MaxSalary ?? assignment.BasicSalary;

        return Result.Success(new EmployeeSalarySummaryDto(
            employeeId,
            pay.Basic,
            PayrollRules.Round2(pay.Gross - pay.Basic),
            pay.Gross,
            pay.TotalDeductions,
            pay.Net,
            grade?.Code ?? Unknown,
            grade?.Name ?? Unknown,
            min,
            max,
            BandPosition(assignment.BasicSalary, min, max),
            Assumptions(assignment, structure, structureComponents)));
    }

    /// <summary>Rounded percentage of the band (halves up), clamped to 0 to 100; 0 when the band has no width.</summary>
    internal static int BandPosition(decimal basic, decimal min, decimal max) =>
        max > min
            ? (int)Math.Clamp(Math.Round((basic - min) / (max - min) * 100, 0, MidpointRounding.AwayFromZero), 0, 100)
            : 0;

    private static List<string> Assumptions(SalaryAssignment assignment, SalaryStructure structure, List<SalaryComponent> structureComponents)
    {
        var lines = new List<string>
        {
            $"Basic salary comes from the current assignment: {structure.Name}, effective {assignment.EffectiveFrom:yyyy-MM-dd}.",
        };
        lines.AddRange(structureComponents
            .Where(c => c.IsActive)
            .Select(c => $"{c.Name} ({c.Type.ToString().ToLowerInvariant()}): {PayrollRules.Describe(c.Calculation, c.Value)}."));
        lines.Add("These are demonstration rules, not real tax or statutory calculations.");
        return lines;
    }
}
