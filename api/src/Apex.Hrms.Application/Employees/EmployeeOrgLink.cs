namespace Apex.Hrms.Application.Employees;

/// <summary>
/// Read model of the view hr.EmployeeOrgLink: where each employee sits in the organization, without personal data
/// (security review L5). The organization delete and move checks count employees through it instead of reading
/// hr.Employee. Nothing writes to it.
/// </summary>
public sealed class EmployeeOrgLink(
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
