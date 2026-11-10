using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Leave;

namespace Apex.Hrms.Application.Leave.LeaveTypes;

internal sealed class LeaveTypeRules(IRepository<LeaveType> types) : ISaveRules<LeaveType, LeaveTypeDetails>
{
    public Task<Result> CheckCreateAsync(LeaveTypeDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(LeaveType existing, LeaveTypeDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, existing.Id, cancellationToken);

    private Task<Result> EnsureUniqueCodeAsync(LeaveTypeDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        return HierarchyChecks.EnsureUniqueAsync(
            types, t => t.Code == code && t.Id != excludedId, "Leave type code", "code", code, cancellationToken);
    }
}
