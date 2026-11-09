namespace Apex.Hrms.Application.Organization;

/// <summary>
/// Read-only slice of hr.Employee: just the columns that point at organization records, so the reference checks can
/// count employees before the Employee module is ported. Replace it with the real aggregate then.
/// </summary>
public sealed class EmployeeReference(
    int id,
    int companyId,
    int branchId,
    int departmentId,
    int? sectionId,
    int positionId,
    int jobGradeId)
{
    public int Id { get; private set; } = id;

    public int CompanyId { get; private set; } = companyId;

    public int BranchId { get; private set; } = branchId;

    public int DepartmentId { get; private set; } = departmentId;

    public int? SectionId { get; private set; } = sectionId;

    public int PositionId { get; private set; } = positionId;

    public int JobGradeId { get; private set; } = jobGradeId;
}
