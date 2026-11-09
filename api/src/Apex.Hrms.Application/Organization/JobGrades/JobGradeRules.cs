using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.JobGrades;

internal sealed class JobGradeRules(IRepository<JobGrade> grades) : ISaveRules<JobGrade, JobGradeDetails>
{
    public Task<Result> CheckCreateAsync(JobGradeDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(JobGrade existing, JobGradeDetails details, CancellationToken cancellationToken) =>
        EnsureUniqueCodeAsync(details, existing.Id, cancellationToken);

    private Task<Result> EnsureUniqueCodeAsync(JobGradeDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        return HierarchyChecks.EnsureUniqueAsync(
            grades, g => g.Code == code && g.Id != excludedId, "Grade code", "code", code, cancellationToken);
    }
}
