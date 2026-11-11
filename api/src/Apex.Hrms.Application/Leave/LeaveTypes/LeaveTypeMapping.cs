using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.LeaveTypes;

internal sealed class LeaveTypeMapping :
    IEntityDescriptor<LeaveType>,
    IDtoMapper<LeaveType, LeaveTypeDto>,
    IEntityFactory<LeaveType, LeaveTypeDetails>
{
    public string Label => "Leave type";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Leave;

    public string Describe(LeaveType entity) => entity.Name;

    public LeaveTypeDto ToDto(LeaveType entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.DefaultDaysPerYear,
        entity.IsPaid,
        entity.RequiresDocument,
        entity.MaxConsecutiveDays,
        entity.CarryForward,
        entity.MaxCarryForwardDays,
        entity.Color,
        entity.EligibleGender is { } gender ? GenderText.ToText(gender) : null,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(LeaveTypeDetails details, LeaveType? existing) => LeaveType.Validate(details);

    public Result<LeaveType> Create(LeaveTypeDetails details, RecordStatus status) => LeaveType.Create(details, status);

    public Result Update(LeaveType entity, LeaveTypeDetails details) => entity.Update(details);
}
