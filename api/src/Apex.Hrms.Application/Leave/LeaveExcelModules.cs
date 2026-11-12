using System.Text.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using Apex.Hrms.Domain.Organization;
using Microsoft.Extensions.DependencyInjection;
using static Apex.Hrms.Application.Excel.ExcelModuleSupport;

namespace Apex.Hrms.Application.Leave;

// Leave data in Excel is export only. There is no stored opening balance to import (balances are worked out from the types,
// policies and applications), and importing applications would bypass the submit/approve workflow, so imports are refused:
// the modules are in the catalog with canImport false. The rows an export contains are the rows the list shows to the caller
// (own record, own team, or everyone, by the leave tier), because the exports read through the module's own list handlers.

internal static class LeaveExcelRegistration
{
    public static IServiceCollection AddLeaveExcel(this IServiceCollection services)
    {
        services.AddScoped<IExcelModule, LeaveApplicationExcelModule>();
        services.AddScoped<IExcelModule, LeaveBalanceExcelModule>();
        return services;
    }
}


internal sealed class LeaveApplicationExcelModule(
    IQueryHandler<LeaveApplicationDto, LeaveApplicationFilter> query,
    IGetByIdHandler<LeaveApplicationDto> get,
    IRepository<LeaveType> types,
    IRepository<Department> departments,
    IRepository<Employee> employeeRows,
    IAccessScope<Employee> employeeScope,
    ICurrentUser user) : IExcelModule
{
    public static ExcelModuleInfo Definition { get; } = ExportOnly.Info(
        "leave-applications",
        "Leave applications",
        "Export leave applications you are allowed to see: your own, your team's, or everyone's, as on the Leave screen. Import is not available.",
        [
            Out("applicationNo", "Application No", width: 16),
            Out("employeeCode", "Employee Code", width: 14),
            Out("employeeName", "Employee Name", width: 26),
            Out("department", "Department", width: 24),
            Out("leaveTypeCode", "Leave Type Code", width: 16),
            Out("leaveType", "Leave Type", width: 22),
            Out("startDate", "Start Date", ExcelColumnType.Date),
            Out("endDate", "End Date", ExcelColumnType.Date),
            Out("days", "Days", ExcelColumnType.Decimal),
            Out("halfDay", "Half Day", ExcelColumnType.Boolean),
            Out("reason", "Reason", width: 36),
            Out("status", "Status"),
            Out("appliedOn", "Applied On", ExcelColumnType.Date),
            Out("actionedBy", "Actioned By", width: 24),
            Out("actionedOn", "Actioned On", ExcelColumnType.Date),
            Out("approverRemarks", "Approver Remarks", width: 30),
            Out("documentName", "Supporting Document", width: 26),
        ],
        "Leave dates");

    public ExcelModuleInfo Info => Definition;

    public IReadOnlyList<string> ImportPolicies { get; } = [];

    public IReadOnlyList<string> ExportPolicies { get; } = ["leave:View"];

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
        var filter = ParseFilter<LeaveApplicationFilter>(request);
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

        var ids = items.Value.Select(a => a.EmployeeId).Distinct().ToList();
        var people = ids.Count == 0
            ? []
            : (await employeeRows.ListAsync(employeeScope.PredicateFor(user).And(e => ids.Contains(e.Id)), cancellationToken)).ToDictionary(e => e.Id);
        var typeRows = (await types.ListAsync(Predicates.Always<LeaveType>(), cancellationToken)).ToDictionary(t => t.Id);
        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken)).ToDictionary(d => d.Id, d => d.Name);

        return Result.Success(ExcelRows.Of(items.Value.Select(a =>
        {
            people.TryGetValue(a.EmployeeId, out var person);
            typeRows.TryGetValue(a.LeaveTypeId, out var type);
            return new Dictionary<string, object?>
            {
                ["applicationNo"] = a.ApplicationNo,
                ["employeeCode"] = person?.EmployeeCode ?? string.Empty,
                ["employeeName"] = person is null ? string.Empty : EmployeeMapping.FullName(person),
                ["department"] = person is not null && departmentNames.TryGetValue(person.DepartmentId, out var department) ? department : string.Empty,
                ["leaveTypeCode"] = type?.Code ?? string.Empty,
                ["leaveType"] = type?.Name ?? string.Empty,
                ["startDate"] = a.StartDate,
                ["endDate"] = a.EndDate,
                ["days"] = a.Days,
                ["halfDay"] = a.HalfDay,
                ["reason"] = a.Reason,
                ["status"] = a.Status,
                ["appliedOn"] = a.AppliedOn,
                ["actionedBy"] = a.ActionedByName,
                ["actionedOn"] = a.ActionedOn,
                ["approverRemarks"] = a.ApproverRemarks,
                ["documentName"] = a.DocumentName,
            };
        })));
    }
}

/// <summary>
/// Leave balances worked out as the balance screen does (per employee, leave type and year) for the employees whose leave the
/// caller may see. Not stored anywhere, so there is nothing to import.
/// </summary>
internal sealed class LeaveBalanceExcelModule(
    IRepository<Employee> employees,
    IAccessScope<Employee> employeeScope,
    ICurrentUser user,
    SelfService self,
    IQuerySpecification<Employee, EmployeeFilter> specification,
    LeaveBalanceService balances,
    IRepository<Department> departments,
    IClock clock) : IExcelModule
{
    public static ExcelModuleInfo Definition { get; } = ExportOnly.Info(
        "leave-balances",
        "Leave balances",
        "Export each employee's leave entitlement, days taken, pending days and available days for one year (the filter \"year\", the year of the start date, or the current year). Balances are worked out, not stored, so import is not available.",
        [
            Out("employeeCode", "Employee Code", width: 14),
            Out("employeeName", "Employee Name", width: 26),
            Out("department", "Department", width: 24),
            Out("year", "Year", ExcelColumnType.Integer),
            Out("leaveTypeCode", "Leave Type Code", width: 16),
            Out("leaveType", "Leave Type", width: 22),
            Out("entitled", "Entitled", ExcelColumnType.Decimal),
            Out("taken", "Taken", ExcelColumnType.Decimal),
            Out("pending", "Pending", ExcelColumnType.Decimal),
            Out("available", "Available", ExcelColumnType.Decimal),
        ],
        null);

    public ExcelModuleInfo Info => Definition;

    public IReadOnlyList<string> ImportPolicies { get; } = [];

    public IReadOnlyList<string> ExportPolicies { get; } = ["leave:View"];

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
        var filter = ParseFilter<EmployeeFilter>(request);
        var year = YearOf(request);
        if (filter.IsFailure || year.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(filter.IsFailure ? filter.Error : year.Error);
        }

        // The employees the caller may see in the employee list AND whose leave they may see (the leave tier).
        var visible = employeeScope.PredicateFor(user).And(self.Rows<Employee>("leave", ids => employee => ids.Contains(employee.Id)));
        var people = await CollectEmployeesAsync(employees, visible, specification, request, filter.Value, limit, cancellationToken);
        if (people.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(people.Error);
        }

        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken)).ToDictionary(d => d.Id, d => d.Name);
        var rows = new List<Dictionary<string, object?>>();
        foreach (var person in people.Value)
        {
            foreach (var line in await balances.ForEmployeeAsync(person, year.Value, cancellationToken))
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["employeeCode"] = person.EmployeeCode,
                    ["employeeName"] = EmployeeMapping.FullName(person),
                    ["department"] = departmentNames.GetValueOrDefault(person.DepartmentId, string.Empty),
                    ["year"] = year.Value,
                    ["leaveTypeCode"] = line.Code,
                    ["leaveType"] = line.Name,
                    ["entitled"] = line.Entitled,
                    ["taken"] = line.Taken,
                    ["pending"] = line.Pending,
                    ["available"] = line.Available,
                });
                if (rows.Count > limit)
                {
                    return Result.Success(ExcelRows.Of(rows));
                }
            }
        }

        return Result.Success(ExcelRows.Of(rows));
    }

    private Result<int> YearOf(ExportRequest request)
    {
        var year = clock.Today.Year;
        if (request.Scope == ExportScope.Filtered && request.Filters is not null
            && request.Filters.FirstOrDefault(f => string.Equals(f.Key, "year", StringComparison.OrdinalIgnoreCase)).Value is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } value)
        {
            var parsed = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
                ? number
                : int.TryParse(value.ToString(), out var text) ? text : 0;
            year = parsed;
        }
        else if (request.DateFrom is { } from)
        {
            year = from.Year;
        }

        if (year is < LeaveBalanceService.FirstYear or > LeaveBalanceService.LastYear)
        {
            var message = $"Year must be between {LeaveBalanceService.FirstYear} and {LeaveBalanceService.LastYear}.";
            return Result.Failure<int>(AppError.Validation(message, "year", message));
        }

        return Result.Success(year);
    }
}
