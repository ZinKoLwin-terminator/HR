using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.Applications;

internal sealed class LeaveApplicationMapping :
    IEntityDescriptor<LeaveApplication>,
    IDtoMapper<LeaveApplication, LeaveApplicationDto>
{
    public string Label => "Leave application";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Leave;

    public string Describe(LeaveApplication entity) => entity.ApplicationNo;

    public LeaveApplicationDto ToDto(LeaveApplication entity) => new(
        entity.Id,
        entity.ApplicationNo,
        entity.EmployeeId,
        entity.LeaveTypeId,
        entity.StartDate,
        entity.EndDate,
        entity.Days,
        entity.HalfDay,
        entity.Reason,
        entity.DocumentName,
        LeaveStatusText.ToText(entity.Status),
        entity.AppliedOn,
        entity.ApproverEmployeeId,
        entity.ActionedByName,
        entity.ActionedOn,
        entity.ApproverRemarks,
        entity.RowVersion);
}
