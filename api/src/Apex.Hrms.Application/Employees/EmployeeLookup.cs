using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Employees;

/// <summary>
/// The minimal view of a current employee for pickers (the hiring manager of a job opening): id, code, display name, job
/// title and department name, and nothing else. No contact details, dates, status, salary or national id.
/// </summary>
public sealed record EmployeeLookupDto(int Id, string EmployeeCode, string DisplayName, string JobTitle, string DepartmentName);

public interface IEmployeeLookupHandler
{
    /// <summary>One page of current employees (no exit date, not Resigned or Terminated), by name, matching <paramref name="search"/> on code or name.</summary>
    Task<Result<PagedResult<EmployeeLookupDto>>> HandleAsync(string? search, int pageIndex, int pageSize, CancellationToken cancellationToken);
}

/// <summary>
/// Deliberately unscoped: the caller needs recruitment:Edit or employees:View (checked by the host) and receives only the
/// fields of <see cref="EmployeeLookupDto"/>, so it exposes nobody's personal data. Page size is at most 200.
/// </summary>
internal sealed class EmployeeLookupHandler(
    IRepository<Employee> employees,
    IRepository<Position> positions,
    IRepository<Department> departments) : IEmployeeLookupHandler
{
    public async Task<Result<PagedResult<EmployeeLookupDto>>> HandleAsync(
        string? search,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<EmployeeLookupDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.",
                "search",
                $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var predicate = Predicates.Always<Employee>()
            .And(e => e.ExitDate == null && e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated);
        if (!string.IsNullOrWhiteSpace(search))
        {
            predicate = predicate.And(Predicates.ContainsAny<Employee>(search.Trim(), e => e.EmployeeCode, e => e.FirstName, e => e.LastName));
        }

        var page = await employees.QueryAsync(
            new EntityQuery<Employee>(predicate, new SortCriterion<Employee>(e => e.FirstName, false), Math.Max(0, pageIndex), Paging.ClampPageSize(pageSize)),
            cancellationToken);

        var positionIds = page.Items.Select(e => e.PositionId).Distinct().ToArray();
        var departmentIds = page.Items.Select(e => e.DepartmentId).Distinct().ToArray();
        var titles = positionIds.Length == 0
            ? []
            : (await positions.ListAsync(p => positionIds.Contains(p.Id), cancellationToken)).ToDictionary(p => p.Id, p => p.Title);
        var names = departmentIds.Length == 0
            ? []
            : (await departments.ListAsync(d => departmentIds.Contains(d.Id), cancellationToken)).ToDictionary(d => d.Id, d => d.Name);

        return Result.Success(page.Map(e => new EmployeeLookupDto(
            e.Id,
            e.EmployeeCode,
            $"{e.FirstName} {e.LastName}",
            titles.GetValueOrDefault(e.PositionId, string.Empty),
            names.GetValueOrDefault(e.DepartmentId, string.Empty))));
    }
}
