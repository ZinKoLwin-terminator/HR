using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Attendance;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Attendance.Shifts;

/// <summary>The shift code is unique (compared after trimming).</summary>
internal sealed class ShiftSaveRules(IRepository<Shift> shifts) : ISaveRules<Shift, ShiftDetails>
{
    public Task<Result> CheckCreateAsync(ShiftDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(Shift existing, ShiftDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, existing.Id, cancellationToken);

    private Task<Result> EnsureUniqueCodeAsync(ShiftDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        return HierarchyChecks.EnsureUniqueAsync(
            shifts, s => s.Code == code && s.Id != excludedId, "Shift code", "code", code, cancellationToken);
    }
}
