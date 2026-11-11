using System.Linq.Expressions;
using Apex.Hrms.Application.Dashboard;
using Apex.Hrms.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Read-only queries for the dashboard. Employees come through the EF model, narrowed to the columns the figures use.
/// Recruitment has its own API module, but the dashboard still reads its tables (rec.*) and the payroll table (pay.PayrollRun; no payroll module yet) with plain
/// parameterless-or-parameterized SQL that maps onto small row types; nothing here is added to the EF model, so the
/// payroll module can map its table itself.
/// </summary>
internal sealed class EfDashboardSource(HrmsDbContext context) : IDashboardSource
{
    public async Task<IReadOnlyList<EmployeeFigureRow>> EmployeesAsync(
        Expression<Func<Employee, bool>> scope,
        CancellationToken cancellationToken) =>
        await context.Set<Employee>()
            .AsNoTracking()
            .Where(scope)
            .OrderBy(employee => employee.Id)
            .Select(employee => new EmployeeFigureRow(
                employee.Id,
                employee.DepartmentId,
                employee.Status,
                employee.JoiningDate,
                employee.ExitDate,
                employee.DateOfBirth,
                employee.ProbationEndDate,
                employee.FirstName,
                employee.LastName))
            .ToListAsync(cancellationToken);

    public async Task<RecruitmentRows> RecruitmentAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var openings = (await context.Database
            .SqlQuery<OpeningsRow>($"SELECT COUNT(*) AS OpenJobs, COALESCE(SUM(CAST(Vacancies AS int)), 0) AS OpenPositions FROM rec.JobOpening WHERE Status = 'Open'")
            .ToListAsync(cancellationToken)).Single();
        var stages = await context.Database
            .SqlQuery<StageRow>($"SELECT Stage, COUNT(*) AS Total FROM rec.Application GROUP BY Stage")
            .ToListAsync(cancellationToken);
        var interviews = (await context.Database
            .SqlQuery<InterviewsRow>($"SELECT COUNT(*) AS Upcoming FROM rec.Interview WHERE Status = 'Scheduled' AND [Date] >= {today}")
            .ToListAsync(cancellationToken)).Single();

        return new RecruitmentRows(
            openings.OpenJobs,
            openings.OpenPositions,
            stages.ToDictionary(stage => stage.Stage, stage => stage.Total, StringComparer.Ordinal),
            interviews.Upcoming);
    }

    public async Task<IReadOnlyList<PayrollRunRow>> ProcessedPayrollRunsAsync(int count, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(count, 1, 12);
        var runs = await context.Database
            .SqlQuery<RunRow>($"SELECT TOP ({take}) PeriodName, Status, PayDate, EmployeeCount, TotalGross, TotalDeductions, TotalNet FROM pay.PayrollRun WHERE Status <> 'Draft' ORDER BY StartDate DESC")
            .ToListAsync(cancellationToken);
        return [.. runs.Select(run => new PayrollRunRow(
            run.PeriodName,
            run.Status,
            run.PayDate,
            run.EmployeeCount,
            run.TotalGross,
            run.TotalDeductions,
            run.TotalNet))];
    }

    // Row types of the SQL above; the property names are the column names.

    private sealed class OpeningsRow
    {
        public int OpenJobs { get; set; }

        public int OpenPositions { get; set; }
    }

    private sealed class StageRow
    {
        public string Stage { get; set; } = string.Empty;

        public int Total { get; set; }
    }

    private sealed class InterviewsRow
    {
        public int Upcoming { get; set; }
    }

    private sealed class RunRow
    {
        public string PeriodName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateOnly PayDate { get; set; }

        public int EmployeeCount { get; set; }

        public decimal TotalGross { get; set; }

        public decimal TotalDeductions { get; set; }

        public decimal TotalNet { get; set; }
    }
}
