using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.LeavePolicies;

internal sealed class LeavePolicyMapping :
    IEntityDescriptor<LeavePolicy>,
    IDtoMapper<LeavePolicy, LeavePolicyDto>,
    IEntityFactory<LeavePolicy, LeavePolicyDetails>
{
    public string Label => "Leave policy";

    public string Describe(LeavePolicy entity) => entity.Name;

    public LeavePolicyDto ToDto(LeavePolicy entity) => new(
        entity.Id,
        entity.Name,
        entity.LeaveTypeId,
        entity.JobGradeId,
        entity.EmploymentType is { } type ? EmploymentTypeText.ToText(type) : null,
        entity.DaysPerYear,
        LeaveAccrualText.ToText(entity.Accrual),
        entity.MinServiceMonths,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(LeavePolicyDetails details, LeavePolicy? existing) => LeavePolicy.Validate(details);

    public Result<LeavePolicy> Create(LeavePolicyDetails details, RecordStatus status) => LeavePolicy.Create(details, status);

    public Result Update(LeavePolicy entity, LeavePolicyDetails details) => entity.Update(details);
}
