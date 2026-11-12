using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Leave;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Excel;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;
using static Apex.Hrms.Application.Excel.ExcelModuleSupport;

namespace Apex.Hrms.Application.Attendance;

// Attendance records are an export only (they come from punches and approved adjustments, never from a file). Shift
// assignments can be imported: a row sets the work shift of an employee through the employee update handler.

internal static class AttendanceExcelRegistration
{
    public static IServiceCollection AddAttendanceExcel(this IServiceCollection services)
    {
        services.AddScoped<IExcelModule, AttendanceRecordExcelModule>();
        services.AddScoped<IExcelModule, ShiftAssignmentExcelModule>();
        return services;
    }
}

internal sealed class AttendanceRecordExcelModule(
    IQueryHandler<AttendanceListItemDto, AttendanceFilter> query,
    IGetByIdHandler<AttendanceListItemDto> get) : IExcelModule
{
    public static ExcelModuleInfo Definition { get; } = ExportOnly.Info(
        "attendance-records",
        "Attendance records",
        "Export attendance records you are allowed to see: your own, your team's, or everyone's, as on the Attendance screen. Import is not available.",
        [
            Out("date", "Date", ExcelColumnType.Date),
            Out("employeeCode", "Employee Code", width: 14),
            Out("employeeName", "Employee Name", width: 26),
            Out("department", "Department", width: 24),
            Out("branch", "Branch", width: 22),
            Out("shift", "Shift", width: 18),
            Out("checkIn", "Check In"),
            Out("checkOut", "Check Out"),
            Out("workedMinutes", "Worked Minutes", ExcelColumnType.Integer),
            Out("lateMinutes", "Late Minutes", ExcelColumnType.Integer),
            Out("earlyLeaveMinutes", "Early Leave Minutes", ExcelColumnType.Integer),
            Out("overtimeMinutes", "Overtime Minutes", ExcelColumnType.Integer),
            Out("status", "Status"),
            Out("source", "Source"),
            Out("remarks", "Remarks", width: 30),
        ],
        "Date");

    public ExcelModuleInfo Info => Definition;

    public IReadOnlyList<string> ImportPolicies { get; } = [];

    public IReadOnlyList<string> ExportPolicies { get; } = ["attendance:View"];

    public IReadOnlyList<string> SensitiveExportPolicies { get; } = [];

    public Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) => ExportOnly.NoReferences();

    public IExcelImportAdapter CreateImporter() => ExportOnly.Refuse();

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => [];

    public async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var filter = ParseFilter<AttendanceFilter>(request);
        if (filter.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(filter.Error);
        }

        var applied = filter.Value with
        {
            DateFrom = request.DateFrom ?? filter.Value.DateFrom,
            DateTo = request.DateTo ?? filter.Value.DateTo,
        };
        var items = await CollectAsync(query, get, request, applied, limit, cancellationToken);
        if (items.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(items.Error);
        }

        static string? Time(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture);
        return Result.Success(ExcelRows.Of(items.Value.Select(r => new Dictionary<string, object?>
        {
            ["date"] = r.Date,
            ["employeeCode"] = r.EmployeeCode,
            ["employeeName"] = r.EmployeeName,
            ["department"] = r.DepartmentName,
            ["branch"] = r.BranchName,
            ["shift"] = r.ShiftName,
            ["checkIn"] = Time(r.CheckIn),
            ["checkOut"] = Time(r.CheckOut),
            ["workedMinutes"] = r.WorkedMinutes,
            ["lateMinutes"] = r.LateMinutes,
            ["earlyLeaveMinutes"] = r.EarlyLeaveMinutes,
            ["overtimeMinutes"] = r.OvertimeMinutes,
            ["status"] = r.Status,
            ["source"] = r.Source,
            ["remarks"] = r.Remarks,
        })));
    }
}

/// <summary>
/// Which work shift each employee has. Export: the employees whose attendance the caller may see (by the attendance tier);
/// import: one row sets an employee's shift through the employee update handler (so its rules apply) and needs the "all"
/// tier, which is <c>attendance:Approve</c> together with <c>employees:Edit</c>.
/// </summary>
internal sealed class ShiftAssignmentExcelModule(
    IRepository<Employee> employees,
    IAccessScope<Employee> employeeScope,
    ICurrentUser user,
    SelfService self,
    IQuerySpecification<Employee, EmployeeFilter> specification,
    IRepository<Shift> shifts,
    IRepository<Department> departments,
    IRepository<Branch> branches,
    IGetByIdHandler<EmployeeDto> getEmployee,
    IUpdateHandler<EmployeeUpdateRequest, EmployeeDto> updateEmployee) : IExcelModule
{
    private static readonly IReadOnlyList<ExcelColumn> Columns =
    [
        Col("employeeCode", "Employee Code", "Code of the employee. Existing employees are matched by this code.", "EMP061", c => c with
        {
            Required = true,
            MaxLength = 12,
            Uppercase = true,
            Pattern = Code,
            PatternMessage = "Employee Code must use capital letters, numbers and dashes only.",
            Aliases = ["Employee ID", "Emp Code"],
        }),
        Col("shiftCode", "Shift Code", "Code of the work shift to assign (see the \"Shift codes\" sheet).", "GEN", c => c with { Required = true, MaxLength = 20 }),
    ];

    private static readonly IReadOnlyList<ExcelColumn> ExportColumns =
    [
        Out("employeeCode", "Employee Code", width: 14),
        Out("employeeName", "Employee Name", width: 26),
        Out("department", "Department", width: 24),
        Out("branch", "Branch", width: 22),
        Out("employmentStatus", "Employment Status"),
        Out("shiftCode", "Shift Code", width: 14),
        Out("shift", "Shift", width: 20),
    ];

    public static ExcelModuleInfo Definition { get; } = new(
        "shift-assignments",
        "Shift assignments",
        "Shift assignments",
        "Assign work shifts to employees. Each row sets the shift of the employee with that Employee Code. Import needs the HR tier of attendance (attendance:Approve and employees:Edit).",
        Columns,
        ExportColumns,
        ["employeeCode"],
        "Employee Code",
        CanImport: true,
        CanExport: true,
        ExportDateLabel: null);

    public ExcelModuleInfo Info => Definition;

    public IReadOnlyList<string> ImportPolicies { get; } = ["attendance:Edit", "attendance:Approve", "employees:Edit"];

    public IReadOnlyList<string> ExportPolicies { get; } = ["attendance:View"];

    public IReadOnlyList<string> SensitiveExportPolicies { get; } = [];

    public IExcelImportAdapter CreateImporter() => new Importer(employees, employeeScope, user, shifts, getEmployee, updateEmployee);

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => [];

    public async Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken)
    {
        var rows = (await shifts.ListAsync(Predicates.Always<Shift>(), cancellationToken)).OrderBy(s => s.Code, StringComparer.Ordinal)
            .Select(s => new Dictionary<string, object?>
            {
                ["code"] = s.Code,
                ["name"] = s.Name,
                ["hours"] = $"{s.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)}-{s.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)}",
            });
        return
        [
            new ExportSheet(
                "Shift codes",
                [Plain("code", "Shift Code", 14), Plain("name", "Shift", 28), Plain("hours", "Hours", 14)],
                ExcelRows.Of(rows)),
        ];
    }

    public async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var filter = ParseFilter<EmployeeFilter>(request);
        if (filter.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(filter.Error);
        }

        // The employees the caller may see in the employee list AND whose attendance they may see (the attendance tier).
        var visible = employeeScope.PredicateFor(user).And(self.Rows<Employee>("attendance", ids => employee => ids.Contains(employee.Id)));
        var people = await CollectEmployeesAsync(employees, visible, specification, request, filter.Value, limit, cancellationToken);
        if (people.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(people.Error);
        }

        var shiftRows = (await shifts.ListAsync(Predicates.Always<Shift>(), cancellationToken)).ToDictionary(s => s.Id);
        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken)).ToDictionary(d => d.Id, d => d.Name);
        var branchNames = (await branches.ListAsync(Predicates.Always<Branch>(), cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        return Result.Success(ExcelRows.Of(people.Value.Select(e =>
        {
            var shift = e.ShiftId is { } id ? shiftRows.GetValueOrDefault(id) : null;
            return new Dictionary<string, object?>
            {
                ["employeeCode"] = e.EmployeeCode,
                ["employeeName"] = EmployeeMapping.FullName(e),
                ["department"] = departmentNames.GetValueOrDefault(e.DepartmentId, string.Empty),
                ["branch"] = branchNames.GetValueOrDefault(e.BranchId, string.Empty),
                ["employmentStatus"] = EmploymentStatusText.ToText(e.Status),
                ["shiftCode"] = shift?.Code ?? string.Empty,
                ["shift"] = shift?.Name ?? string.Empty,
            };
        })));
    }

    private sealed class Importer(
        IRepository<Employee> employees,
        IAccessScope<Employee> employeeScope,
        ICurrentUser user,
        IRepository<Shift> shifts,
        IGetByIdHandler<EmployeeDto> getEmployee,
        IUpdateHandler<EmployeeUpdateRequest, EmployeeDto> updateEmployee) : IExcelImportAdapter
    {
        private static readonly Dictionary<string, string> FieldToColumn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["shiftId"] = "shiftCode",
        };

        public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>A shift assignment exists when the employee already has a shift.</summary>
        public async Task<bool> ExistsAsync(ParsedRow row, CancellationToken cancellationToken) =>
            await FindAsync(row.Text("employeeCode"), cancellationToken) is { ShiftId: not null };

        public async Task<IReadOnlyList<ImportIssueDto>> ApplyAsync(ParsedRow row, bool exists, CancellationToken cancellationToken)
        {
            var problems = new RowProblems(Columns, row);
            var employee = await FindAsync(row.Text("employeeCode"), cancellationToken);
            if (employee is null)
            {
                problems.Add("employeeCode", $"Employee \"{row.Text("employeeCode")}\" does not exist.");
            }

            var code = row.Text("shiftCode").Trim().ToUpperInvariant();
            var shiftMatches = await shifts.ListAsync(s => s.Code == code, cancellationToken);
            var shift = shiftMatches.Count > 0 ? shiftMatches[0] : null;
            if (shift is null)
            {
                problems.Add("shiftCode", $"Shift \"{row.Text("shiftCode")}\" does not exist.");
            }

            if (employee is null || shift is null)
            {
                return problems.Issues;
            }

            var current = await getEmployee.HandleAsync(employee.Id, cancellationToken);
            if (current.IsFailure)
            {
                problems.Issues.AddRange(ToIssues(current.Error, row, Columns, FieldToColumn, "employeeCode"));
                return problems.Issues;
            }

            var cur = current.Value;
            if (cur.ShiftId == shift.Id)
            {
                return problems.Issues;
            }

            var request = new EmployeeUpdateRequest(
                cur.EmployeeCode, cur.FirstName, cur.LastName, cur.Gender, cur.WorkEmail, cur.PhotoUrl, cur.CompanyId, cur.BranchId,
                cur.DepartmentId, cur.SectionId, cur.PositionId, cur.JobGradeId, cur.ManagerId, shift.Id, cur.EmploymentType,
                cur.JoiningDate, cur.ProbationEndDate, Convert.ToBase64String(cur.RowVersion));
            var updated = await updateEmployee.HandleAsync(cur.Id, request, cancellationToken);
            if (updated.IsFailure)
            {
                problems.Issues.AddRange(ToIssues(updated.Error, row, Columns, FieldToColumn, "employeeCode"));
            }

            return problems.Issues;
        }

        private async Task<Employee?> FindAsync(string code, CancellationToken cancellationToken)
        {
            var upper = code.Trim().ToUpperInvariant();
            var scope = employeeScope.PredicateFor(user).And(e => e.EmployeeCode == upper);
            var found = await employees.ListAsync(scope, cancellationToken);
            return found.Count > 0 ? found[0] : null;
        }
    }
}
