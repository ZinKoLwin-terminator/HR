using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Templates;

/// <summary>
/// Rules of a KPI template that need other records: a unique code, an existing category (an active one when it is new
/// or changed) and, when set, an existing department. Editing a template never touches the scorecard items that were
/// assigned from it: they hold their own copy.
/// </summary>
internal sealed class KpiTemplateRules(
    IRepository<KpiTemplate> templates,
    IRepository<KpiCategory> categories,
    IRepository<Department> departments) : ISaveRules<KpiTemplate, KpiTemplateDetails>
{
    public Task<Result> CheckCreateAsync(KpiTemplateDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing: null, cancellationToken);

    public Task<Result> CheckUpdateAsync(KpiTemplate existing, KpiTemplateDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing, cancellationToken);

    private async Task<Result> CheckAsync(KpiTemplateDetails details, KpiTemplate? existing, CancellationToken cancellationToken)
    {
        var code = details.Code;
        var excludedId = existing?.Id ?? 0;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            templates, t => t.Code == code && t.Id != excludedId, "KPI code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var category = await HierarchyChecks.FindParentAsync(categories, details.CategoryId, "KPI category", "categoryId", cancellationToken);
        if (category.IsFailure)
        {
            return category;
        }

        if (existing?.CategoryId != details.CategoryId)
        {
            var active = HierarchyChecks.EnsureActive(category.Value, "KPI category", category.Value.Name, "KPI templates", "categoryId");
            if (active.IsFailure)
            {
                return active;
            }
        }

        if (details.DepartmentId is { } departmentId)
        {
            var department = await HierarchyChecks.FindParentAsync(departments, departmentId, "Department", "departmentId", cancellationToken);
            if (department.IsFailure)
            {
                return department;
            }
        }

        return Result.Success();
    }
}
