using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Positions;

internal sealed class PositionMapping :
    IEntityDescriptor<Position>,
    IDtoMapper<Position, PositionDto>,
    IEntityFactory<Position, PositionDetails>
{
    public string Label => "Position";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Organization;

    public string Describe(Position entity) => entity.Title;

    public PositionDto ToDto(Position entity) => new(
        entity.Id,
        entity.DepartmentId,
        entity.SectionId,
        entity.JobGradeId,
        entity.Code,
        entity.Title,
        entity.Description ?? string.Empty,
        entity.Headcount,
        entity.ReportsToPositionId,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(PositionDetails details, Position? existing) =>
        Position.Validate(details, existing?.Id ?? 0);

    public Result<Position> Create(PositionDetails details, RecordStatus status) => Position.Create(details, status);

    public Result Update(Position entity, PositionDetails details) => entity.Update(details);
}
