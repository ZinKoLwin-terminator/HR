using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Sections;

internal sealed class SectionDeletionPolicy : IDeletionPolicy<Section>
{
    public IReadOnlyList<ReferenceCheck> References { get; } =
    [
        new("position(s)", ReferenceQuery.PositionsOfSection),
        new("employee(s)", ReferenceQuery.EmployeesOfSection),
    ];
}
