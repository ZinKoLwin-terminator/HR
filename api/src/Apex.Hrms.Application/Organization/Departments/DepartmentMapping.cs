using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Departments;

internal sealed class DepartmentMapping :
    IEntityDescriptor<Department>,
    IDtoMapper<Department, DepartmentDto>,
    IEntityFactory<Department, DepartmentRequest>
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

    public Result<Department> Create(DepartmentRequest request) => Department.Create(ToDetails(request));

    public Result Update(Department entity, DepartmentRequest request) => entity.Update(ToDetails(request));

    private static DepartmentDetails ToDetails(DepartmentRequest request) => new(
        request.CompanyId,
        request.BranchId,
        request.Code,
        request.Name,
        request.Description,
        request.HeadEmployeeId,
        request.CostCenter,
        Enum.Parse<RecordStatus>(request.Status));
}
