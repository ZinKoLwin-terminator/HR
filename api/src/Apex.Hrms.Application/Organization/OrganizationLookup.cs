using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization;

/// <summary>A company as a name source: id, code, name and status, and nothing else (no legal, tax or contact data).</summary>
public sealed record CompanyLookupDto(int Id, string Code, string Name, string Status);

/// <summary>A branch as a name source: no contact data, address or manager.</summary>
public sealed record BranchLookupDto(int Id, int CompanyId, string Code, string Name, string Status);

/// <summary>A department as a name source: no head employee or cost centre.</summary>
public sealed record DepartmentLookupDto(int Id, int CompanyId, int BranchId, string Code, string Name, string Status);

/// <summary>A section as a name source: no supervisor.</summary>
public sealed record SectionLookupDto(int Id, int DepartmentId, string Code, string Name, string Status);

/// <summary>A position as a name source: no headcount or reporting line.</summary>
public sealed record PositionLookupDto(int Id, int DepartmentId, int? SectionId, int JobGradeId, string Code, string Title, string Status);

/// <summary>A job grade as a name source: no salary band.</summary>
public sealed record JobGradeLookupDto(int Id, string Code, string Name, short Level, string Status);

/// <summary>
/// Every organization record (active and inactive, so old records still show their names) as the names and links that
/// dropdowns and table columns need. Each list is ordered by name (positions by title), then id. No row versions.
/// </summary>
public sealed record OrganizationLookupDto(
    IReadOnlyList<CompanyLookupDto> Companies,
    IReadOnlyList<BranchLookupDto> Branches,
    IReadOnlyList<DepartmentLookupDto> Departments,
    IReadOnlyList<SectionLookupDto> Sections,
    IReadOnlyList<PositionLookupDto> Positions,
    IReadOnlyList<JobGradeLookupDto> JobGrades);

public interface IOrganizationLookupHandler
{
    /// <summary>All companies, branches, departments, sections, positions and job grades, reduced to <see cref="OrganizationLookupDto"/>.</summary>
    Task<Result<OrganizationLookupDto>> HandleAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Deliberately unscoped: the host lets any signed-in user call it, so it returns only non-personal reference data (ids, codes,
/// names, parent ids, job grade level and status). One untracked projection per record type: no employee ids, contact data,
/// cost centre, legal or tax data, salary bands or row versions leave the database.
/// </summary>
internal sealed class OrganizationLookupHandler(IReadModel read) : IOrganizationLookupHandler
{
    public async Task<Result<OrganizationLookupDto>> HandleAsync(CancellationToken cancellationToken)
    {
        var companies = await read.ToListAsync(
            read.Query<Company>()
                .OrderBy(company => company.Name).ThenBy(company => company.Id)
                .Select(company => new { company.Id, company.Code, company.Name, company.Status }),
            cancellationToken);
        var branches = await read.ToListAsync(
            read.Query<Branch>()
                .OrderBy(branch => branch.Name).ThenBy(branch => branch.Id)
                .Select(branch => new { branch.Id, branch.CompanyId, branch.Code, branch.Name, branch.Status }),
            cancellationToken);
        var departments = await read.ToListAsync(
            read.Query<Department>()
                .OrderBy(department => department.Name).ThenBy(department => department.Id)
                .Select(department => new { department.Id, department.CompanyId, department.BranchId, department.Code, department.Name, department.Status }),
            cancellationToken);
        var sections = await read.ToListAsync(
            read.Query<Section>()
                .OrderBy(section => section.Name).ThenBy(section => section.Id)
                .Select(section => new { section.Id, section.DepartmentId, section.Code, section.Name, section.Status }),
            cancellationToken);
        var positions = await read.ToListAsync(
            read.Query<Position>()
                .OrderBy(position => position.Title).ThenBy(position => position.Id)
                .Select(position => new { position.Id, position.DepartmentId, position.SectionId, position.JobGradeId, position.Code, position.Title, position.Status }),
            cancellationToken);
        var jobGrades = await read.ToListAsync(
            read.Query<JobGrade>()
                .OrderBy(grade => grade.Name).ThenBy(grade => grade.Id)
                .Select(grade => new { grade.Id, grade.Code, grade.Name, grade.Level, grade.Status }),
            cancellationToken);

        // The status text is the enum name, as in the resource DTOs ("Active" / "Inactive"); converted here, after the query.
        return Result.Success(new OrganizationLookupDto(
            [.. companies.Select(row => new CompanyLookupDto(row.Id, row.Code, row.Name, row.Status.ToString()))],
            [.. branches.Select(row => new BranchLookupDto(row.Id, row.CompanyId, row.Code, row.Name, row.Status.ToString()))],
            [.. departments.Select(row => new DepartmentLookupDto(row.Id, row.CompanyId, row.BranchId, row.Code, row.Name, row.Status.ToString()))],
            [.. sections.Select(row => new SectionLookupDto(row.Id, row.DepartmentId, row.Code, row.Name, row.Status.ToString()))],
            [.. positions.Select(row => new PositionLookupDto(row.Id, row.DepartmentId, row.SectionId, row.JobGradeId, row.Code, row.Title, row.Status.ToString()))],
            [.. jobGrades.Select(row => new JobGradeLookupDto(row.Id, row.Code, row.Name, row.Level, row.Status.ToString()))]));
    }
}
