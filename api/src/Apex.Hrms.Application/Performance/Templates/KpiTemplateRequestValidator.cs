using Apex.Hrms.Domain.Performance;
using FluentValidation;

namespace Apex.Hrms.Application.Performance.Templates;

/// <summary>
/// Shape rules shared by the create and update requests: required fields, lengths, known texts and decimal places. Whether
/// the scoring rule is consistent is a domain rule (<see cref="KpiRule.Validate"/>).
/// </summary>
internal abstract class KpiTemplateFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IKpiTemplateFields
{
    private const string FourDecimals = "Use at most 4 decimal places.";
    private const string TwoDecimals = "Use at most 2 decimal places.";

    protected KpiTemplateFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "KPI name").RequiredText(150);
        RuleForText(x => x.Description, "Description").OptionalText(500);
        RuleForText(x => x.Unit, "Unit").OptionalText(20);
        RuleForText(x => x.MeasurementType, "Measurement type")
            .NotEmpty().WithMessage("Measurement type is required.")
            .Must(text => KpiMeasurementTypeText.TryParse(text?.Trim(), out _))
            .WithMessage($"Measurement type must be {KpiMeasurementTypeText.Map.OneOf}.");
        RuleForText(x => x.Direction, "Direction")
            .NotEmpty().WithMessage("Direction is required.")
            .Must(text => KpiDirectionText.TryParse(text?.Trim(), out _))
            .WithMessage($"Direction must be {KpiDirectionText.Map.OneOf}.");
        RuleForText(x => x.ScoringMethod, "Scoring method")
            .NotEmpty().WithMessage("Scoring method is required.")
            .Must(text => KpiScoringMethodText.TryParse(text?.Trim(), out _))
            .WithMessage($"Scoring method must be {KpiScoringMethodText.Map.OneOf}.");
        RuleForText(x => x.Frequency, "Frequency")
            .NotEmpty().WithMessage("Frequency is required.")
            .Must(text => KpiFrequencyText.TryParse(text?.Trim(), out _))
            .WithMessage($"Frequency must be {KpiFrequencyText.Map.OneOf}.");
        RuleFor(x => x.DefaultTarget).PrecisionScale(18, 4, ignoreTrailingZeros: true).WithMessage(FourDecimals);
        RuleFor(x => x.RangeMin).PrecisionScale(18, 4, ignoreTrailingZeros: true).WithMessage(FourDecimals);
        RuleFor(x => x.RangeMax).PrecisionScale(18, 4, ignoreTrailingZeros: true).WithMessage(FourDecimals);
        RuleFor(x => x.MaxRating).PrecisionScale(18, 4, ignoreTrailingZeros: true).WithMessage(FourDecimals);
        RuleFor(x => x.DefaultWeight).PrecisionScale(5, 2, ignoreTrailingZeros: true).WithMessage(TwoDecimals);
        RuleFor(x => x.MaxAchievementPct).PrecisionScale(8, 2, ignoreTrailingZeros: true).WithMessage(TwoDecimals);
    }
}

internal sealed class KpiTemplateCreateRequestValidator : KpiTemplateFieldsValidator<KpiTemplateCreateRequest>
{
    public KpiTemplateCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class KpiTemplateUpdateRequestValidator : KpiTemplateFieldsValidator<KpiTemplateUpdateRequest>
{
    public KpiTemplateUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
