using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Sections;

internal sealed class SectionMapping :
    IEntityDescriptor<Section>,
    IDtoMapper<Section, SectionDto>,
    IEntityFactory<Section, SectionRequest>
{
    public string Label => "Section";

    public string Describe(Section entity) => entity.Name;

    public SectionDto ToDto(Section entity) => new(
        entity.Id,
        entity.DepartmentId,
        entity.Code,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.SupervisorEmployeeId,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result<Section> Create(SectionRequest request) => Section.Create(ToDetails(request));

    public Result Update(Section entity, SectionRequest request) => entity.Update(ToDetails(request));

    private static SectionDetails ToDetails(SectionRequest request) => new(
        request.DepartmentId,
        request.Code,
        request.Name,
        request.Description,
        request.SupervisorEmployeeId,
        Enum.Parse<RecordStatus>(request.Status));
}
