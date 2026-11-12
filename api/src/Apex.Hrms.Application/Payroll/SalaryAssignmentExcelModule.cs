using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Excel;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Payroll;
using Microsoft.Extensions.DependencyInjection;
using static Apex.Hrms.Application.Excel.ExcelModuleSupport;

namespace Apex.Hrms.Application.Payroll;

internal static class PayrollExcelRegistration
{
    public static IServiceCollection AddPayrollExcel(this IServiceCollection services)
    {
        services.AddScoped<IExcelModule, SalaryAssignmentExcelModule>();
        return services;
    }
}

/// <summary>
/// Salary assignments in Excel. Salary figures are the most sensitive data of the system, so import and export both need
/// <c>payroll:Approve</c> (the tier that sees every employee's salary) on top of the usual payroll permissions. Rows go through the
/// create and update handlers of the API: the job grade band, the joining date and one assignment per employee and start date
/// are enforced by them, not repeated here. The amounts are never written to the activity feed, and the Basic Salary import
/// column is masked in previews and stored issues. The figures are demonstration figures, not statutory pay.
/// </summary>
internal sealed class SalaryAssignmentExcelModule(
    IQueryHandler<SalaryAssignmentDto, SalaryAssignmentFilter> query,
    IGetByIdHandler<SalaryAssignmentDto> get,
    ICreateHandler<SalaryAssignmentCreateRequest, SalaryAssignmentDto> create,
    IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto> update,
    IRepository<SalaryAssignment> assignments,
    IAccessScope<SalaryAssignment> assignmentScope,
    IRepository<SalaryStructure> structures,
    IRepository<Employee> employees,
    IAccessScope<Employee> employeeScope,
    IRepository<JobGrade> grades,
    ICurrentUser user) : IExcelModule
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        Col("employeeCode", "Employee Code", "Code of the employee. Part of the key that matches an existing assignment.", "EMP061", c => c with
        {
            Required = true,
            MaxLength = 12,
            Uppercase = true,
            Pattern = Code,
            PatternMessage = "Employee Code must use capital letters, numbers and dashes only.",
            Aliases = ["Employee ID", "Emp Code"],
        }),
        Col("effectiveFrom", "Effective From", "First day the salary applies, not before the joining date. Part of the matching key.", "2026-01-01", c => c with { Required = true, Type = ExcelColumnType.Date }),
        Col("structureCode", "Salary Structure Code", "Code of an active salary structure.", "STAFF", c => c with { Required = true, MaxLength = 20 }),
        Col("basicSalary", "Basic Salary", "Monthly basic salary (sample data), inside the band of the employee's job grade.", 1500, c => c with
        {
            Required = true,
            Type = ExcelColumnType.Decimal,
            Min = 0,
            Decimals = 2,
            Sensitive = true,
        }),
        Col("remarks", "Remarks", "Note about the change (optional).", "Annual review", c => c with { MaxLength = SalaryAssignment.RemarksMaxLength }),
    ];

    private static readonly IReadOnlyList<ExcelColumn> ExportColumns =
    [
        Out("employeeCode", "Employee Code", width: 14),
        Out("employeeName", "Employee Name", width: 26),
        Out("jobGrade", "Job Grade"),
        Out("structureCode", "Salary Structure Code", width: 18),
        Out("structure", "Salary Structure", width: 24),
        Out("effectiveFrom", "Effective From", ExcelColumnType.Date),
        Out("basicSalary", "Basic Salary", ExcelColumnType.Decimal),
        Out("remarks", "Remarks", width: 30),
    ];

    private static readonly Dictionary<string, string> FieldToColumn = new(StringComparer.OrdinalIgnoreCase)
    {
        ["employeeId"] = "employeeCode",
        ["structureId"] = "structureCode",
    };

    public static ExcelModuleInfo Definition { get; } = new(
        "salary-assignments",
        "Salary assignments",
        "Salary assignments",
        "Import and export salary assignments (basic salary by employee and start date). Needs payroll:Approve. The basic salary must be inside the employee's job grade band and an employee has one assignment per start date.",
        Columns,
        ExportColumns,
        ["employeeCode", "effectiveFrom"],
        "Employee Code + Effective From",
        CanImport: true,
        CanExport: true,
        ExportDateLabel: "Effective date");

    public ExcelModuleInfo Info => Definition;

    public IReadOnlyList<string> ImportPolicies { get; } = ["payroll:View", "payroll:Create", "payroll:Edit", "payroll:Approve"];

    public IReadOnlyList<string> ExportPolicies { get; } = ["payroll:View", "payroll:Approve"];

    public IReadOnlyList<string> SensitiveExportPolicies { get; } = ["payroll:Approve"];

    public IExcelImportAdapter CreateImporter() => new Importer(create, update, get, assignments, assignmentScope, structures, employees, employeeScope, user);

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => [];

    public async Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken)
    {
        var rows = (await structures.ListAsync(s => s.Status == RecordStatus.Active, cancellationToken)).OrderBy(s => s.Code, StringComparer.Ordinal)
            .Select(s => new Dictionary<string, object?> { ["code"] = s.Code, ["name"] = s.Name });
        return
        [
            new ExportSheet(
                "Structure codes",
                [Plain("code", "Salary Structure Code", 22), Plain("name", "Salary Structure", 30)],
                ExcelRows.Of(rows)),
        ];
    }

    public async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var filter = ParseFilter<SalaryAssignmentFilter>(request);
        if (filter.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(filter.Error);
        }

        // The list handler applies the salary scope (payroll:Approve sees everyone; the route policies already require it).
        var items = await CollectAsync(query, get, request, filter.Value, request.DateFrom is null && request.DateTo is null ? limit : int.MaxValue / 2, cancellationToken);
        if (items.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(items.Error);
        }

        var selected = items.Value
            .Where(a => (request.DateFrom is null || a.EffectiveFrom >= request.DateFrom) && (request.DateTo is null || a.EffectiveFrom <= request.DateTo))
            .Take(limit + 1)
            .ToList();
        var ids = selected.Select(a => a.EmployeeId).Distinct().ToList();
        var people = ids.Count == 0
            ? []
            : (await employees.ListAsync(employeeScope.PredicateFor(user).And(e => ids.Contains(e.Id)), cancellationToken)).ToDictionary(e => e.Id);
        var structureRows = (await structures.ListAsync(Predicates.Always<SalaryStructure>(), cancellationToken)).ToDictionary(s => s.Id);
        var gradeCodes = (await grades.ListAsync(Predicates.Always<JobGrade>(), cancellationToken)).ToDictionary(g => g.Id, g => g.Code);

        return Result.Success(ExcelRows.Of(selected.Select(a =>
        {
            people.TryGetValue(a.EmployeeId, out var person);
            structureRows.TryGetValue(a.StructureId, out var structure);
            return new Dictionary<string, object?>
            {
                ["employeeCode"] = person?.EmployeeCode ?? string.Empty,
                ["employeeName"] = person is null ? string.Empty : EmployeeMapping.FullName(person),
                ["jobGrade"] = person is not null && gradeCodes.TryGetValue(person.JobGradeId, out var grade) ? grade : string.Empty,
                ["structureCode"] = structure?.Code ?? string.Empty,
                ["structure"] = structure?.Name ?? string.Empty,
                ["effectiveFrom"] = a.EffectiveFrom,
                ["basicSalary"] = a.BasicSalary,
                ["remarks"] = a.Remarks,
            };
        })));
    }

    private sealed class Importer(
        ICreateHandler<SalaryAssignmentCreateRequest, SalaryAssignmentDto> create,
        IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto> update,
        IGetByIdHandler<SalaryAssignmentDto> get,
        IRepository<SalaryAssignment> assignments,
        IAccessScope<SalaryAssignment> assignmentScope,
        IRepository<SalaryStructure> structures,
        IRepository<Employee> employees,
        IAccessScope<Employee> employeeScope,
        ICurrentUser user) : IExcelImportAdapter
    {
        public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<bool> ExistsAsync(ParsedRow row, CancellationToken cancellationToken) =>
            await FindAssignmentAsync(row, cancellationToken) is not null;

        public async Task<IReadOnlyList<ImportIssueDto>> ApplyAsync(ParsedRow row, bool exists, CancellationToken cancellationToken)
        {
            var problems = new RowProblems(Columns, row);
            var code = row.Text("employeeCode").Trim().ToUpperInvariant();
            var employeeMatches = await employees.ListAsync(employeeScope.PredicateFor(user).And(e => e.EmployeeCode == code), cancellationToken);
            var employee = employeeMatches.Count > 0 ? employeeMatches[0] : null;
            if (employee is null)
            {
                problems.Add("employeeCode", $"Employee \"{row.Text("employeeCode")}\" does not exist.");
            }

            var structureCode = row.Text("structureCode").Trim().ToUpperInvariant();
            var structureMatches = await structures.ListAsync(s => s.Code == structureCode, cancellationToken);
            var structure = structureMatches.Count > 0 ? structureMatches[0] : null;
            if (structure is null)
            {
                problems.Add("structureCode", $"Salary structure \"{row.Text("structureCode")}\" does not exist.");
            }

            if (employee is null || structure is null)
            {
                return problems.Issues;
            }

            var basic = Number(row.Text("basicSalary"));
            var from = Date(row.Text("effectiveFrom"));
            var failure = exists
                ? await UpdateAsync(row, employee, structure, basic, from, cancellationToken)
                : await CreateAsync(row, employee, structure, basic, from, cancellationToken);
            if (failure is not null)
            {
                problems.Issues.AddRange(ToIssues(failure, row, Columns, FieldToColumn, "employeeCode"));
            }

            return problems.Issues;
        }

        private async Task<AppError?> CreateAsync(ParsedRow row, Employee employee, SalaryStructure structure, decimal basic, DateOnly from, CancellationToken cancellationToken)
        {
            var created = await create.HandleAsync(
                new SalaryAssignmentCreateRequest(employee.Id, structure.Id, basic, from, Opt(row.Text("remarks"))),
                cancellationToken);
            return created.IsFailure ? created.Error : null;
        }

        private async Task<AppError?> UpdateAsync(ParsedRow row, Employee employee, SalaryStructure structure, decimal basic, DateOnly from, CancellationToken cancellationToken)
        {
            var existing = await FindAssignmentAsync(row, cancellationToken);
            if (existing is null)
            {
                return AppError.NotFound("The record no longer exists.");
            }

            var current = await get.HandleAsync(existing.Id, cancellationToken);
            if (current.IsFailure)
            {
                return current.Error;
            }

            // A blank remark keeps the current one.
            var remarks = row.Has("remarks") ? row.Text("remarks") : current.Value.Remarks;
            var updated = await update.HandleAsync(
                current.Value.Id,
                new SalaryAssignmentUpdateRequest(employee.Id, structure.Id, basic, from, Opt(remarks), Convert.ToBase64String(current.Value.RowVersion)),
                cancellationToken);
            return updated.IsFailure ? updated.Error : null;
        }

        private async Task<SalaryAssignment?> FindAssignmentAsync(ParsedRow row, CancellationToken cancellationToken)
        {
            var code = row.Text("employeeCode").Trim().ToUpperInvariant();
            var employeeMatches = await employees.ListAsync(employeeScope.PredicateFor(user).And(e => e.EmployeeCode == code), cancellationToken);
            if (employeeMatches.Count == 0 || row.Text("effectiveFrom").Length == 0)
            {
                return null;
            }

            var employeeId = employeeMatches[0].Id;
            var from = Date(row.Text("effectiveFrom"));
            var found = await assignments.ListAsync(
                assignmentScope.PredicateFor(user).And(a => a.EmployeeId == employeeId && a.EffectiveFrom == from),
                cancellationToken);
            return found.Count > 0 ? found[0] : null;
        }
    }
}
