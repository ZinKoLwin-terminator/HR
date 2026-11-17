using Apex.Hrms.Domain.Performance;
using FluentValidation;

namespace Apex.Hrms.Application.Performance.Periods;

/// <summary>Shape rules shared by the create and update requests. Date order is a domain rule.</summary>
internal abstract class EvaluationPeriodFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IEvaluationPeriodFields
{
    protected EvaluationPeriodFieldsValidator()
    {
        RuleForText(x => x.Name, "Period name").RequiredText(100);
        RuleForText(x => x.PeriodType, "Period type")
            .NotEmpty().WithMessage("Period type is required.")
            .Must(text => KpiFrequencyText.TryParse(text?.Trim(), out _))
            .WithMessage($"Period type must be {KpiFrequencyText.Map.OneOf}.");
    }
}

internal sealed class EvaluationPeriodCreateRequestValidator : EvaluationPeriodFieldsValidator<EvaluationPeriodCreateRequest>;

internal sealed class EvaluationPeriodUpdateRequestValidator : EvaluationPeriodFieldsValidator<EvaluationPeriodUpdateRequest>
{
    public EvaluationPeriodUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
