using Apex.Hrms.Domain.Grievances;
using FluentValidation;

namespace Apex.Hrms.Application.Grievances.Categories;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class GrievanceCategoryFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IGrievanceCategoryFields
{
    protected GrievanceCategoryFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(GrievanceCategory.CodeMaxLength);
        RuleForText(x => x.Name, "Category name").RequiredText(GrievanceCategory.NameMaxLength);
        RuleForText(x => x.Description, "Description").OptionalText(GrievanceCategory.DescriptionMaxLength);
        RuleFor(x => x.DefaultTargetDays)
            .InclusiveBetween(GrievanceCategory.MinTargetDays, GrievanceCategory.MaxTargetDays)
            .WithMessage($"The target must be between {GrievanceCategory.MinTargetDays} and {GrievanceCategory.MaxTargetDays} days.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, short.MaxValue).WithMessage("The sort order must be between 0 and 32767.");
    }
}

internal sealed class GrievanceCategoryCreateRequestValidator : GrievanceCategoryFieldsValidator<GrievanceCategoryCreateRequest>
{
    public GrievanceCategoryCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class GrievanceCategoryUpdateRequestValidator : GrievanceCategoryFieldsValidator<GrievanceCategoryUpdateRequest>
{
    public GrievanceCategoryUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
