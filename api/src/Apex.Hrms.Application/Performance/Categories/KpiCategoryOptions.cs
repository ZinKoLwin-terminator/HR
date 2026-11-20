using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Categories;

/// <summary>A KPI category as a picker option: id, code, name and status (as in <see cref="KpiCategoryDto"/>), no description or row version.</summary>
public sealed record KpiCategoryOptionDto(int Id, string Code, string Name, string Status);

/// <summary>
/// The category picker for callers without the setup permission (the category filter of the KPI reports): every category, active and
/// inactive, so old report rows keep their names.
/// </summary>
public interface IKpiCategoryOptionsHandler
{
    /// <summary>Every category, by name then id, reduced to <see cref="KpiCategoryOptionDto"/>.</summary>
    Task<Result<IReadOnlyList<KpiCategoryOptionDto>>> HandleAsync(CancellationToken cancellationToken);
}

/// <summary>Unscoped reference data (the host requires <c>kpi:View</c>). One untracked projection of the four columns.</summary>
internal sealed class KpiCategoryOptionsHandler(IReadModel read) : IKpiCategoryOptionsHandler
{
    public async Task<Result<IReadOnlyList<KpiCategoryOptionDto>>> HandleAsync(CancellationToken cancellationToken)
    {
        var rows = await read.ToListAsync(
            read.Query<KpiCategory>()
                .OrderBy(category => category.Name).ThenBy(category => category.Id)
                .Select(category => new { category.Id, category.Code, category.Name, category.Status }),
            cancellationToken);

        // The status text is the enum name, as KpiCategoryMapping writes it; converted here, after the query.
        IReadOnlyList<KpiCategoryOptionDto> options =
            [.. rows.Select(row => new KpiCategoryOptionDto(row.Id, row.Code, row.Name, row.Status.ToString()))];
        return Result.Success(options);
    }
}
