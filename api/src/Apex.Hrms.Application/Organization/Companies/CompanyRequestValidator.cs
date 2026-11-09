using FluentValidation;

namespace Apex.Hrms.Application.Organization.Companies;

internal sealed class CompanyRequestValidator : RequestValidator<CompanyRequest>
{
    public CompanyRequestValidator()
    {
        RuleFor(x => x.Code).RequiredCode(20);
        RuleFor(x => x.Name).RequiredText(100);
        RuleFor(x => x.LegalName).RequiredText(150);
        RuleFor(x => x.RegistrationNumber).OptionalText(40);
        RuleFor(x => x.TaxId).OptionalText(40);
        RuleFor(x => x.Industry).RequiredText(80);
        RuleFor(x => x.Email).RequiredText(120).EmailAddress().WithMessage("Enter a valid email address.");
        RuleFor(x => x.Phone).RequiredText(25).OptionalPhone();
        RuleFor(x => x.Website).OptionalText(150);
        RuleFor(x => x.Address).OptionalText(200);
        RuleFor(x => x.City).RequiredText(60);
        RuleFor(x => x.Country).RequiredText(60);
        RuleFor(x => x.FoundedDate).NotEmpty().WithMessage("Founded date is required.");
        RuleFor(x => x.Status).RequiredStatus();
    }
}
