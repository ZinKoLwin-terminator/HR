using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Attendance.Records;

/// <summary>Adds the employee, department, branch and shift names to attendance records (the list shape of the frontend).</summary>
internal sealed class AttendanceListItemBuilder(
    IRepository<Employee> employees,
    IRepository<Department> departments,
    IRepository<Branch> branches,
    IRepository<Shift> shifts)
{
    public async Task<IReadOnlyList<AttendanceListItemDto>> BuildAsync(
        IReadOnlyList<AttendanceRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var employeeIds = records.Select(record => record.EmployeeId).Distinct().ToList();
        var people = (await employees.ListAsync(employee => employeeIds.Contains(employee.Id), cancellationToken))
            .ToDictionary(employee => employee.Id);
        var departmentNames = (await departments.ListAsync(Predicates.Always<Department>(), cancellationToken))
            .ToDictionary(department => department.Id, department => department.Name);
        var branchNames = (await branches.ListAsync(Predicates.Always<Branch>(), cancellationToken))
            .ToDictionary(branch => branch.Id, branch => branch.Name);
        var shiftNames = (await shifts.ListAsync(Predicates.Always<Shift>(), cancellationToken))
            .ToDictionary(shift => shift.Id, shift => shift.Name);

        return [.. records.Select(record =>
        {
            people.TryGetValue(record.EmployeeId, out var employee);
            return new AttendanceListItemDto(
                record.Id,
                record.EmployeeId,
                record.Date,
                record.ShiftId,
                record.CheckIn,
                record.CheckOut,
                record.WorkedMinutes,
                record.LateMinutes,
                record.EarlyLeaveMinutes,
                record.OvertimeMinutes,
                AttendanceStatusText.ToText(record.Status),
                AttendanceSourceText.ToText(record.Source),
                record.Remarks ?? string.Empty,
                record.RowVersion,
                employee?.EmployeeCode ?? "-",
                employee is null ? $"Employee #{record.EmployeeId}" : $"{employee.FirstName} {employee.LastName}",
                employee?.DepartmentId ?? 0,
                employee is not null && departmentNames.TryGetValue(employee.DepartmentId, out var department) ? department : "-",
                employee?.BranchId ?? 0,
                employee is not null && branchNames.TryGetValue(employee.BranchId, out var branch) ? branch : "-",
                record.ShiftId is { } shiftId && shiftNames.TryGetValue(shiftId, out var shift) ? shift : "-");
        })];
    }
}

internal static class AttendanceRecordQuerySpecification
{
    public static IReadOnlyDictionary<string, Expression<Func<AttendanceRecord, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<AttendanceRecord, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = r => r.Id,
            ["date"] = r => r.Date,
            ["employeeId"] = r => r.EmployeeId,
            ["checkIn"] = r => r.CheckIn ?? TimeOnly.MinValue,
            ["checkOut"] = r => r.CheckOut ?? TimeOnly.MinValue,
            ["workedMinutes"] = r => r.WorkedMinutes,
            ["lateMinutes"] = r => r.LateMinutes,
            ["earlyLeaveMinutes"] = r => r.EarlyLeaveMinutes,
            ["overtimeMinutes"] = r => r.OvertimeMinutes,
            ["status"] = r => r.Status,
        };

    /// <summary>The filters that look at the record itself. The employee, department and branch filters are handled by the caller.</summary>
    public static Expression<Func<AttendanceRecord, bool>> RecordFilter(AttendanceFilter filter)
    {
        var predicate = Predicates.Always<AttendanceRecord>()
            .And(filter.EmployeeId is { } employeeId ? r => r.EmployeeId == employeeId : Predicates.Always<AttendanceRecord>())
            .And(filter.DateFrom is { } from ? r => r.Date >= from : Predicates.Always<AttendanceRecord>())
            .And(filter.DateTo is { } to ? r => r.Date <= to : Predicates.Always<AttendanceRecord>())
            .And(StatusFilter(filter.Status));
        if (filter.LateOnly == true)
        {
            predicate = predicate.And(r => r.LateMinutes > 0);
        }

        if (filter.EarlyOnly == true)
        {
            predicate = predicate.And(r => r.EarlyLeaveMinutes > 0);
        }

        if (filter.OvertimeOnly == true)
        {
            predicate = predicate.And(r => r.OvertimeMinutes > 0);
        }

        return predicate;
    }

    private static Expression<Func<AttendanceRecord, bool>> StatusFilter(string? text)
    {
        if (!FilterPredicates.IsSet(text))
        {
            return Predicates.Always<AttendanceRecord>();
        }

        return AttendanceStatusText.TryParse(text, out var status)
            ? r => r.Status == status
            : Predicates.Never<AttendanceRecord>();
    }
}

/// <summary>
/// The list of attendance records, a page at a time. The employee-level filters (department, branch, search by name or
/// code) are turned into the ids of the matching employees first.
/// </summary>
internal sealed class AttendanceRecordQueryHandler(
    IRepository<AttendanceRecord> records,
    IRepository<Employee> employees,
    IAccessScope<AttendanceRecord> scope,
    ICurrentUser user,
    AttendanceListItemBuilder builder) : IQueryHandler<AttendanceListItemDto, AttendanceFilter>
{
    public async Task<Result<PagedResult<AttendanceListItemDto>>> HandleAsync(
        PageRequest<AttendanceFilter> request,
        CancellationToken cancellationToken)
    {
        SortCriterion<AttendanceRecord>? sort = null;
        if (request.Sort is { Active: { Length: > 0 } active, Direction: { Length: > 0 } direction })
        {
            if (!AttendanceRecordQuerySpecification.SortKeys.TryGetValue(active, out var key))
            {
                return Result.Failure<PagedResult<AttendanceListItemDto>>(
                    AppError.Validation($"Cannot sort by \"{active}\".", "sort", "Choose a sortable field."));
            }

            sort = new SortCriterion<AttendanceRecord>(key, string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase));
        }

        if (request.Search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<AttendanceListItemDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.",
                "search",
                $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var filter = request.Filters;
        var predicate = scope.PredicateFor(user).And(AttendanceRecordQuerySpecification.RecordFilter(filter));

        var employeeCriteria = EmployeeCriteria(filter, request.Search);
        if (employeeCriteria is not null)
        {
            var matching = await employees.ListAsync(employeeCriteria, cancellationToken);
            var ids = matching.Select(employee => employee.Id).ToList();
            predicate = predicate.And(record => ids.Contains(record.EmployeeId));
        }

        var page = await records.QueryAsync(
            new EntityQuery<AttendanceRecord>(
                predicate,
                sort,
                Math.Max(0, request.PageIndex),
                Paging.ClampPageSize(request.PageSize)),
            cancellationToken);
        var items = await builder.BuildAsync(page.Items, cancellationToken);
        return Result.Success(new PagedResult<AttendanceListItemDto>(items, page.Total, page.PageIndex, page.PageSize));
    }

    private static Expression<Func<Employee, bool>>? EmployeeCriteria(AttendanceFilter filter, string? search)
    {
        Expression<Func<Employee, bool>>? criteria = null;
        if (filter.DepartmentId is { } departmentId)
        {
            criteria = employee => employee.DepartmentId == departmentId;
        }

        if (filter.BranchId is { } branchId)
        {
            Expression<Func<Employee, bool>> byBranch = employee => employee.BranchId == branchId;
            criteria = criteria is null ? byBranch : criteria.And(byBranch);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var bySearch = Predicates.ContainsAny<Employee>(term, employee => employee.FirstName, employee => employee.LastName, employee => employee.EmployeeCode);
            criteria = criteria is null ? bySearch : criteria.And(bySearch);
        }

        return criteria;
    }
}

internal sealed class AttendanceRecordGetHandler(
    EntityLoader<AttendanceRecord> loader,
    AttendanceListItemBuilder builder) : IGetByIdHandler<AttendanceListItemDto>
{
    public async Task<Result<AttendanceListItemDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(id, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<AttendanceListItemDto>(loaded.Error);
        }

        var items = await builder.BuildAsync([loaded.Value], cancellationToken);
        return Result.Success(items[0]);
    }
}
