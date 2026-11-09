using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Branches;

internal sealed class BranchMapping(IClock clock) :
    IEntityDescriptor<Branch>,
    IDtoMapper<Branch, BranchDto>,
    IEntityFactory<Branch, BranchDetails>
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

    public Result Validate(BranchDetails details, Branch? existing) => Branch.Validate(details, clock.Today);

    public Result<Branch> Create(BranchDetails details, RecordStatus status) =>
        Branch.Create(details, status, clock.Today);

    public Result Update(Branch entity, BranchDetails details) => entity.Update(details, clock.Today);
}
