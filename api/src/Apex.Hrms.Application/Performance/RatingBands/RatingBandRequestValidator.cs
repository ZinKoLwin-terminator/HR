using FluentValidation;

namespace Apex.Hrms.Application.Performance.RatingBands;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class RatingBandFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IRatingBandFields
{
    protected RatingBandFieldsValidator()
    {
        RuleForText(x => x.Label, "Label").RequiredText(60);
        RuleForText(x => x.Description, "Description").OptionalText(300);
        RuleFor(x => x.MinScore)
            .PrecisionScale(8, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
    }
}

internal sealed class RatingBandCreateRequestValidator : RatingBandFieldsValidator<RatingBandCreateRequest>
{
    public RatingBandCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class RatingBandUpdateRequestValidator : RatingBandFieldsValidator<RatingBandUpdateRequest>
{
    public RatingBandUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
