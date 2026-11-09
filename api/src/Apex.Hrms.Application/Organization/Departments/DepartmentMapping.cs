using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Departments;

internal sealed class DepartmentMapping :
    IEntityDescriptor<Department>,
    IDtoMapper<Department, DepartmentDto>,
    IEntityFactory<Department, DepartmentDetails>
{
    public string Label => "Department";

    public string Describe(Department entity) => entity.Name;

    public DepartmentDto ToDto(Department entity) => new(
        entity.Id,
        entity.CompanyId,
        entity.BranchId,
        entity.Code,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.HeadEmployeeId,
        entity.CostCenter ?? string.Empty,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(DepartmentDetails details, Department? existing) => Department.Validate(details);

    public Result<Department> Create(DepartmentDetails details, RecordStatus status) =>
        Department.Create(details, status);

    public Result Update(Department entity, DepartmentDetails details) => entity.Update(details);
}
