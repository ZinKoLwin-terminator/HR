using System.Linq.Expressions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.JobGrades;

internal sealed class JobGradeQuerySpecification : IQuerySpecification<JobGrade, JobGradeFilter>
{
    public IReadOnlyDictionary<string, Expression<Func<JobGrade, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<JobGrade, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = g => g.Id,
            ["code"] = g => g.Code,
            ["name"] = g => g.Name,
            ["level"] = g => g.Level,
            ["minSalary"] = g => g.MinSalary,
            ["maxSalary"] = g => g.MaxSalary,
            ["status"] = g => g.Status,
        };

    public Expression<Func<JobGrade, bool>> Search(string term) =>
        Predicates.ContainsAny<JobGrade>(term, g => g.Code, g => g.Name);

    public Expression<Func<JobGrade, bool>> Filter(JobGradeFilter filter) =>
        FilterPredicates.Status<JobGrade>(filter.Status);
}
