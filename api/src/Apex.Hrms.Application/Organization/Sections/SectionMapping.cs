using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Sections;

internal sealed class SectionMapping :
    IEntityDescriptor<Section>,
    IDtoMapper<Section, SectionDto>,
    IEntityFactory<Section, SectionDetails>
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

    public Result Validate(SectionDetails details, Section? existing) => Section.Validate(details);

    public Result<Section> Create(SectionDetails details, RecordStatus status) => Section.Create(details, status);

    public Result Update(Section entity, SectionDetails details) => entity.Update(details);
}
