using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.References;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Sections;

internal sealed class SectionRules(
    IRepository<Section> sections,
    IRepository<Department> departments,
    IEnumerable<IReferenceCheck<Section>> references) : ISaveRules<Section, SectionDetails>
{
    public Task<Result> CheckCreateAsync(SectionDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(Section existing, SectionDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing, cancellationToken);

    private async Task<Result> CheckAsync(SectionDetails details, Section? existing, CancellationToken cancellationToken)
    {
        var code = details.Code;
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            sections, s => s.Code == code && s.Id != excludedId, "Section code", "code", code, cancellationToken);
        if (unique.IsFailure || existing?.DepartmentId == details.DepartmentId)
        {
            return unique;
        }

        var department = await HierarchyChecks.FindParentAsync(
            departments, details.DepartmentId, "Department", "departmentId", cancellationToken);
        if (department.IsFailure)
        {
            return department;
        }

        var active = HierarchyChecks.EnsureActive(
            department.Value, "Department", department.Value.Name, "sections", "departmentId");
        if (active.IsFailure || existing is null)
        {
            return active;
        }

        return await ReferenceUsage.EnsureCanMoveAsync(
            references, existing.Id, "Section", existing.Name, "department", "departmentId", cancellationToken);
    }
}
