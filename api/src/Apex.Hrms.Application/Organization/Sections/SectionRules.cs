using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Organization.Sections;

internal sealed class SectionRules(IRepository<Section> sections, IRepository<Department> departments)
    : ISaveRules<Section, SectionRequest>
{
    public Task<Result> CheckCreateAsync(SectionRequest request, CancellationToken cancellationToken) =>
        CheckAsync(request, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(Section existing, SectionRequest request, CancellationToken cancellationToken) =>
        CheckAsync(request, existing, cancellationToken);

    private async Task<Result> CheckAsync(SectionRequest request, Section? existing, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            sections, s => s.Code == code && s.Id != excludedId, "Section code", "code", code, cancellationToken);
        if (unique.IsFailure || existing?.DepartmentId == request.DepartmentId)
        {
            return unique;
        }

        var department = await HierarchyChecks.FindParentAsync(
            departments, request.DepartmentId, "Department", "departmentId", cancellationToken);
        return department.IsFailure
            ? department
            : HierarchyChecks.EnsureActive(
                department.Value, "Department", department.Value.Name, "sections", "departmentId");
    }
}
