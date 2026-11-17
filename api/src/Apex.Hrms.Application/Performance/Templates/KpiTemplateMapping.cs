using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Templates;

internal sealed class KpiTemplateMapping :
    IEntityDescriptor<KpiTemplate>,
    IDtoMapper<KpiTemplate, KpiTemplateDto>,
    IEntityFactory<KpiTemplate, KpiTemplateDetails>
{
    public string Label => "KPI template";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Performance;

    public string Describe(KpiTemplate entity) => entity.Name;

    public KpiTemplateDto ToDto(KpiTemplate entity) => new(
        entity.Id,
        entity.Code,
        entity.Name,
        entity.Description ?? string.Empty,
        entity.CategoryId,
        entity.DepartmentId,
        KpiMeasurementTypeText.ToText(entity.MeasurementType),
        entity.Unit,
        KpiDirectionText.ToText(entity.Direction),
        KpiScoringMethodText.ToText(entity.ScoringMethod),
        entity.DefaultTarget,
        entity.RangeMin,
        entity.RangeMax,
        entity.MaxRating,
        entity.DefaultWeight,
        entity.MaxAchievementPct,
        KpiFrequencyText.ToText(entity.Frequency),
        entity.Status.ToString(),
        entity.RowVersion);

    public Result Validate(KpiTemplateDetails details, KpiTemplate? existing) => KpiTemplate.Validate(details);

    public Result<KpiTemplate> Create(KpiTemplateDetails details, RecordStatus status) => KpiTemplate.Create(details, status);

    public Result Update(KpiTemplate entity, KpiTemplateDetails details) => entity.Update(details);
}
