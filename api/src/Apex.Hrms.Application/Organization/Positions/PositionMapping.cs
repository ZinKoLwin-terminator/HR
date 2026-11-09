using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Positions;

internal sealed class PositionMapping :
    IEntityDescriptor<Position>,
    IDtoMapper<Position, PositionDto>,
    IEntityFactory<Position, PositionRequest>
{
    public string Label => "Position";

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

    public Result<Position> Create(PositionRequest request) => Position.Create(ToDetails(request));

    public Result Update(Position entity, PositionRequest request) => entity.Update(ToDetails(request));

    private static PositionDetails ToDetails(PositionRequest request) => new(
        request.DepartmentId,
        request.SectionId,
        request.JobGradeId,
        request.Code,
        request.Title,
        request.Description,
        request.Headcount,
        request.ReportsToPositionId,
        Enum.Parse<RecordStatus>(request.Status));
}
