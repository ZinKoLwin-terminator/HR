using FluentValidation;

namespace Apex.Hrms.Application.Organization.Companies;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class CompanyFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ICompanyFields
{
    protected CompanyFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Company name").RequiredText(100);
        RuleForText(x => x.LegalName, "Legal name").RequiredText(150);
        RuleForText(x => x.RegistrationNumber, "Registration number").OptionalText(40);
        RuleForText(x => x.TaxId, "Tax ID").OptionalText(40);
        RuleForText(x => x.Industry, "Industry").RequiredText(80);
        RuleForText(x => x.Email, "Email").RequiredText(120).ValidEmail();
        RuleForText(x => x.Phone, "Phone").RequiredText(25).OptionalPhone();
        RuleForText(x => x.Website, "Website").OptionalText(150);
        RuleForText(x => x.Address, "Address").OptionalText(200);
        RuleForText(x => x.City, "City").RequiredText(60);
        RuleForText(x => x.Country, "Country").RequiredText(60);
        RuleFor(x => x.FoundedDate).NotEmpty().WithMessage("Founded date is required.");
    }
}

internal sealed class CompanyCreateRequestValidator : CompanyFieldsValidator<CompanyCreateRequest>
{
    public CompanyCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class CompanyUpdateRequestValidator : CompanyFieldsValidator<CompanyUpdateRequest>
{
    public CompanyUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
