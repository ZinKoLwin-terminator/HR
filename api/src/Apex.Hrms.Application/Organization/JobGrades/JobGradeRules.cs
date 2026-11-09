using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.JobGrades;

internal sealed class JobGradeRules(IRepository<JobGrade> grades) : ISaveRules<JobGrade, JobGradeRequest>
{
    public Task<Result> CheckCreateAsync(JobGradeRequest request, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(request, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(JobGrade existing, JobGradeRequest request, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(request, existing.Id, cancellationToken);

    private Task<Result> EnsureUniqueCodeAsync(JobGradeRequest request, int excludedId, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        return HierarchyChecks.EnsureUniqueAsync(
            grades, g => g.Code == code && g.Id != excludedId, "Grade code", "code", code, cancellationToken);
    }
}
