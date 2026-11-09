using FluentValidation;

namespace Apex.Hrms.Application.Organization.Sections;

internal sealed class SectionRequestValidator : RequestValidator<SectionRequest>
{
    public SectionRequestValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Department is required.");
        RuleFor(x => x.Code).RequiredCode(20);
        RuleFor(x => x.Name).RequiredText(100);
        RuleFor(x => x.Description).OptionalText(300);
        RuleFor(x => x.Status).RequiredStatus();
    }
}
