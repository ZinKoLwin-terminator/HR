using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.JobGrades;

internal sealed class JobGradeDeletionPolicy : IDeletionPolicy<JobGrade>
{
    public IReadOnlyList<ReferenceCheck> References { get; } =
    [
        new("position(s)", ReferenceQuery.PositionsOfJobGrade),
        new("employee(s)", ReferenceQuery.EmployeesOfJobGrade),
    ];
}
