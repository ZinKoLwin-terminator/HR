using FluentValidation;

namespace Apex.Hrms.Application.Performance.Categories;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class KpiCategoryFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IKpiCategoryFields
{
    protected KpiCategoryFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Category name").RequiredText(100);
        RuleForText(x => x.Description, "Description").OptionalText(300);
    }
}

internal sealed class KpiCategoryCreateRequestValidator : KpiCategoryFieldsValidator<KpiCategoryCreateRequest>
{
    public KpiCategoryCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class KpiCategoryUpdateRequestValidator : KpiCategoryFieldsValidator<KpiCategoryUpdateRequest>
{
    public KpiCategoryUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
