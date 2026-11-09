using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Positions;

internal sealed class PositionDeletionPolicy : IDeletionPolicy<Position>
{
    public IReadOnlyList<ReferenceCheck> References { get; } =
    [
        new("employee(s)", ReferenceQuery.EmployeesOfPosition),
        new("position(s) reporting to it", ReferenceQuery.PositionsReportingToPosition),
    ];
}
