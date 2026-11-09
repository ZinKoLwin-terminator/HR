using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Branches;

internal sealed class BranchMapping(IClock clock) :
    IEntityDescriptor<Branch>,
    IDtoMapper<Branch, BranchDto>,
    IEntityFactory<Branch, BranchRequest>
{
    public string Label => "Branch";

    public string Describe(Branch entity) => entity.Name;

    public BranchDto ToDto(Branch entity) => new(
        entity.Id,
        entity.CompanyId,
        entity.Code,
        entity.Name,
        BranchTypeText.ToText(entity.Type),
        entity.Email ?? string.Empty,
        entity.Phone ?? string.Empty,
        entity.Address ?? string.Empty,
        entity.City,
        entity.Country,
        entity.ManagerEmployeeId,
        entity.OpenedDate,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result<Branch> Create(BranchRequest request) => Branch.Create(ToDetails(request), clock.Today);

    public Result Update(Branch entity, BranchRequest request) => entity.Update(ToDetails(request), clock.Today);

    private static BranchDetails ToDetails(BranchRequest request)
    {
        return new BranchDetails(
            request.CompanyId,
            request.Code,
            request.Name,
            BranchTypeText.Parse(request.Type),
            request.Email,
            request.Phone,
            request.Address,
            request.City,
            request.Country,
            request.ManagerEmployeeId,
            request.OpenedDate,
            Enum.Parse<RecordStatus>(request.Status));
    }
}
