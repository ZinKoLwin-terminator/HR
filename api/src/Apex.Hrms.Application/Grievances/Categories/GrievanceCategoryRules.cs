using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Grievances.Categories;

/// <summary>A grievance category has a unique code and a unique name. A category with cases cannot be deleted (a reference rule).</summary>
internal sealed class GrievanceCategoryRules(IRepository<GrievanceCategory> categories) : ISaveRules<GrievanceCategory, GrievanceCategoryDetails>
{
    public Task<Result> CheckCreateAsync(GrievanceCategoryDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, excludedId: 0, cancellationToken);

    public Task<Result> CheckUpdateAsync(GrievanceCategory existing, GrievanceCategoryDetails details, CancellationToken cancellationToken) =>
        CheckAsync(details, existing.Id, cancellationToken);

    private async Task<Result> CheckAsync(GrievanceCategoryDetails details, int excludedId, CancellationToken cancellationToken)
    {
        var code = details.Code;
        var unique = await HierarchyChecks.EnsureUniqueAsync(
            categories, c => c.Code == code && c.Id != excludedId, "Grievance category code", "code", code, cancellationToken);
        if (unique.IsFailure)
        {
            return unique;
        }

        var name = details.Name;
        return await HierarchyChecks.EnsureUniqueAsync(
            categories, c => c.Name == name && c.Id != excludedId, "Grievance category name", "name", name, cancellationToken);
    }
}
