using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Attendance.Shifts;

internal sealed class ShiftMapping :
    IEntityDescriptor<Shift>,
    IDtoMapper<Shift, ShiftDto>,
    IEntityFactory<Shift, ShiftDetails>
{
    public string Label => "Shift";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Attendance;

    public string Describe(Shift entity) => entity.Name;

    public ShiftDto ToDto(Shift entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.StartTime,
        entity.EndTime,
        entity.BreakMinutes,
        entity.GraceMinutes,
        entity.WorkDayNumbers,
        entity.IsNightShift,
        entity.Color,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(ShiftDetails details, Shift? existing) => Shift.Validate(details);

    public Result<Shift> Create(ShiftDetails details, RecordStatus status) => Shift.Create(details, status);

    public Result Update(Shift entity, ShiftDetails details) => entity.Update(details);
}
