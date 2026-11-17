using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Categories;

internal sealed class KpiCategoryMapping :
    IEntityDescriptor<KpiCategory>,
    IDtoMapper<KpiCategory, KpiCategoryDto>,
    IEntityFactory<KpiCategory, KpiCategoryDetails>
{
    public string Label => "KPI category";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Performance;

    public string Describe(KpiCategory entity) => entity.Name;

    public KpiCategoryDto ToDto(KpiCategory entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(KpiCategoryDetails details, KpiCategory? existing) => KpiCategory.Validate(details);

    public Result<KpiCategory> Create(KpiCategoryDetails details, RecordStatus status) => KpiCategory.Create(details, status);

    public Result Update(KpiCategory entity, KpiCategoryDetails details) => entity.Update(details);
}
