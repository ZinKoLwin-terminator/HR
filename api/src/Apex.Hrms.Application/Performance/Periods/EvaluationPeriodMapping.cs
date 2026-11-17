using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Periods;

internal sealed class EvaluationPeriodMapping :
    IEntityDescriptor<EvaluationPeriod>,
    IDtoMapper<EvaluationPeriod, EvaluationPeriodDto>,
    IEntityFactory<EvaluationPeriod, EvaluationPeriodDetails>
{
    public string Label => "Evaluation period";

    public string ActivityCategory => Domain.Activity.ActivityCategories.Performance;

    public string Describe(EvaluationPeriod entity) => entity.Name;

    public EvaluationPeriodDto ToDto(EvaluationPeriod entity) => new(
        entity.Id,
        entity.CompanyId,
        entity.Name,
        KpiFrequencyText.ToText(entity.PeriodType),
        entity.StartDate,
        entity.EndDate,
        entity.SubmissionDueDate,
        entity.RequiresHrReview,
        EvaluationPeriodStatusText.ToText(entity.Status),
        entity.RowVersion);

    public static EvaluationPeriodOptionDto ToOption(EvaluationPeriod entity) => new(
        entity.Id,
        entity.Name,
        entity.CompanyId,
        KpiFrequencyText.ToText(entity.PeriodType),
        entity.StartDate,
        entity.EndDate,
        EvaluationPeriodStatusText.ToText(entity.Status),
        entity.RequiresHrReview);

    public Result Validate(EvaluationPeriodDetails details, EvaluationPeriod? existing) => EvaluationPeriod.Validate(details);

    public Result<EvaluationPeriod> Create(EvaluationPeriodDetails details, RecordStatus status) => EvaluationPeriod.Create(details);

    public Result Update(EvaluationPeriod entity, EvaluationPeriodDetails details) => entity.Update(details);
}
