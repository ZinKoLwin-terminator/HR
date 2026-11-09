namespace Apex.Hrms.Application.Abstractions;

/// <summary>Names every "how many records point at this one" question the delete rules ask.</summary>
public enum ReferenceQuery
{
    BranchesOfCompany,
    DepartmentsOfCompany,
    EmployeesOfCompany,
    DepartmentsOfBranch,
    EmployeesOfBranch,
    SectionsOfDepartment,
    PositionsOfDepartment,
    EmployeesOfDepartment,
    PositionsOfSection,
    EmployeesOfSection,
    PositionsOfJobGrade,
    EmployeesOfJobGrade,
    EmployeesOfPosition,
    PositionsReportingToPosition,
}

/// <summary>Counts records in other aggregates, including the not-yet-ported Employee table.</summary>
public interface IReferenceUsageReader
{
    Task<int> CountAsync(ReferenceQuery query, int ownerId, CancellationToken cancellationToken);
}
