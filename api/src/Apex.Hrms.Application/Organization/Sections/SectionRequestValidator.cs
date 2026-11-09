using FluentValidation;

namespace Apex.Hrms.Application.Organization.Sections;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class SectionFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ISectionFields
{
    protected SectionFieldsValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Department is required.");
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Section name").RequiredText(100);
        RuleForText(x => x.Description, "Description").OptionalText(300);
    }
}

internal sealed class SectionCreateRequestValidator : SectionFieldsValidator<SectionCreateRequest>
{
    public SectionCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class SectionUpdateRequestValidator : SectionFieldsValidator<SectionUpdateRequest>
{
    public SectionUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
