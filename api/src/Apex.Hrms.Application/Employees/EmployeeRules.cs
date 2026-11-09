using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Employees;

/// <summary>Limits of the reporting structure between employees.</summary>
public static class EmployeeReportingLimits
{
    /// <summary>Longest chain of managers that is walked when looking for a loop; anything deeper is rejected.</summary>
    public const int MaxDepth = 50;
}

/// <summary>
/// The rules of an employee that need other records: unique code and work email, an existing and consistent
/// organization assignment (company, branch, department, section, position, job grade), a manager who exists, has not
/// left and does not create a reporting loop. Inactive organization units cannot receive an employee, but existing
/// assignments may stay. Checked against normalized details.
/// </summary>
internal sealed class EmployeeRules(
    IRepository<Employee> employees,
    IRepository<Company> companies,
    IRepository<Branch> branches,
    IRepository<Department> departments,
    IRepository<Section> sections,
    IRepository<Position> positions,
    IRepository<JobGrade> jobGrades)
{
    /// <summary>
    /// Checks the details of a new employee (<paramref name="existing"/> is null) or of an update. On success it
    /// returns what the history entry needs to say about the position.
    /// </summary>
    public async Task<Result<PlacementNotes>> CheckAsync(
        EmployeeDetails details,
        Employee? existing,
        CancellationToken cancellationToken)
    {
        var unique = await CheckUniqueAsync(details, existing, cancellationToken);
        if (unique.IsFailure)
        {
            return Result.Failure<PlacementNotes>(unique.Error);
        }

        var violations = new Violations();
        var placement = await CheckPlacementAsync(details, existing, violations, cancellationToken);
        await CheckManagerAsync(details, existing, violations, cancellationToken);
        return violations.Any
            ? Result.Failure<PlacementNotes>(violations.ToError())
            : Result.Success(await DescribePlacementAsync(details, existing, placement, cancellationToken));
    }

    private async Task<Result> CheckUniqueAsync(EmployeeDetails details, Employee? existing, CancellationToken cancellationToken)
    {
        var excludedId = existing?.Id ?? 0;
        if (details.EmployeeCode is { } code)
        {
            var codeUnique = await HierarchyChecks.EnsureUniqueAsync(
                employees, e => e.EmployeeCode == code && e.Id != excludedId, "Employee code", "employeeCode", code, cancellationToken);
            if (codeUnique.IsFailure)
            {
                return codeUnique;
            }
        }

        var email = details.WorkEmail;
        return await HierarchyChecks.EnsureUniqueAsync(
            employees, e => e.WorkEmail == email && e.Id != excludedId, "Work email", "workEmail", email, cancellationToken);
    }

    /// <summary>Returns the position and job grade found, so the caller can reuse them.</summary>
    private async Task<(Position? Position, JobGrade? Grade)> CheckPlacementAsync(
        EmployeeDetails details,
        Employee? existing,
        Violations violations,
        CancellationToken cancellationToken)
    {
        var company = await companies.FindAsync(details.CompanyId, cancellationToken);
        var branch = await branches.FindAsync(details.BranchId, cancellationToken);
        var department = await departments.FindAsync(details.DepartmentId, cancellationToken);
        var position = await positions.FindAsync(details.PositionId, cancellationToken);
        var grade = await jobGrades.FindAsync(details.JobGradeId, cancellationToken);

        if (company is null)
        {
            violations.Add("companyId", "Choose an existing company.");
        }

        if (branch is null)
        {
            violations.Add("branchId", "Choose an existing branch.");
        }
        else if (company is not null && branch.CompanyId != company.Id)
        {
            violations.Add("branchId", "This branch does not belong to the chosen company.");
        }

        if (department is null)
        {
            violations.Add("departmentId", "Choose an existing department.");
        }
        else if (branch is not null && department.BranchId != branch.Id)
        {
            violations.Add("departmentId", "This department does not belong to the chosen branch.");
        }

        if (details.SectionId is { } sectionId)
        {
            var section = await sections.FindAsync(sectionId, cancellationToken);
            if (section is null)
            {
                violations.Add("sectionId", "Choose an existing section.");
            }
            else if (department is not null && section.DepartmentId != department.Id)
            {
                violations.Add("sectionId", "This section does not belong to the chosen department.");
            }
        }

        if (position is null)
        {
            violations.Add("positionId", "Choose an existing position.");
        }
        else if (department is not null && position.DepartmentId != department.Id)
        {
            violations.Add("positionId", "This position belongs to a different department.");
        }

        if (grade is null)
        {
            violations.Add("jobGradeId", "Choose an existing job grade.");
        }

        // Inactive units cannot receive an employee; an assignment the employee already has may stay.
        AddIfInactive(violations, company, "companyId", "company", existing is null || existing.CompanyId != details.CompanyId);
        AddIfInactive(violations, branch, "branchId", "branch", existing is null || existing.BranchId != details.BranchId);
        AddIfInactive(violations, department, "departmentId", "department", existing is null || existing.DepartmentId != details.DepartmentId);
        AddIfInactive(violations, position, "positionId", "position", existing is null || existing.PositionId != details.PositionId);
        return (position, grade);
    }

    private static void AddIfInactive(Violations violations, CodedRecord? record, string field, string label, bool assigned)
    {
        if (record is { IsActive: false } && assigned)
        {
            violations.Add(field, $"This {label} is inactive.");
        }
    }

    private async Task CheckManagerAsync(
        EmployeeDetails details,
        Employee? existing,
        Violations violations,
        CancellationToken cancellationToken)
    {
        if (details.ManagerId is not { } managerId || existing?.ManagerEmployeeId == managerId)
        {
            return;
        }

        if (existing is not null && managerId == existing.Id)
        {
            violations.Add("managerId", "Choose a different manager.", "An employee cannot be their own manager.");
            return;
        }

        var manager = await employees.FindAsync(managerId, cancellationToken);
        if (manager is null)
        {
            violations.Add("managerId", "The chosen manager does not exist.");
        }
        else if (manager.HasLeft)
        {
            violations.Add("managerId", "A manager cannot be an employee who has left.");
        }
        else if (existing is not null && await ReportsToAsync(manager, existing.Id, cancellationToken))
        {
            violations.Add(
                "managerId",
                "Choose a manager who does not report to this employee, directly or indirectly.",
                "That would create a reporting loop.");
        }
    }

    /// <summary>Walks up the chain of managers starting at <paramref name="start"/>; the walk is bounded so corrupt data cannot loop forever.</summary>
    private async Task<bool> ReportsToAsync(Employee start, int employeeId, CancellationToken cancellationToken)
    {
        var current = start;
        for (var depth = 0; depth < EmployeeReportingLimits.MaxDepth; depth++)
        {
            if (current.Id == employeeId)
            {
                return true;
            }

            if (current.ManagerEmployeeId is not { } nextId || await employees.FindAsync(nextId, cancellationToken) is not { } next)
            {
                return false;
            }

            current = next;
        }

        return true;
    }

    private async Task<PlacementNotes> DescribePlacementAsync(
        EmployeeDetails details,
        Employee? existing,
        (Position? Position, JobGrade? Grade) found,
        CancellationToken cancellationToken)
    {
        if (existing is null || existing.PositionId == details.PositionId)
        {
            return new PlacementNotes(null, found.Position?.Title, false);
        }

        var previous = await positions.FindAsync(existing.PositionId, cancellationToken);
        var previousGrade = existing.JobGradeId == details.JobGradeId
            ? found.Grade
            : await jobGrades.FindAsync(existing.JobGradeId, cancellationToken);
        var promoted = previousGrade is not null && found.Grade is not null && found.Grade.Level > previousGrade.Level;
        return new PlacementNotes(previous?.Title, found.Position?.Title, promoted);
    }
}
