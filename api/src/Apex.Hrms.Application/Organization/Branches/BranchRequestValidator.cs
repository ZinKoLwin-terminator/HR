using Apex.Hrms.Domain.Organization;
using FluentValidation;

namespace Apex.Hrms.Application.Organization.Branches;

internal sealed class BranchRequestValidator : RequestValidator<BranchRequest>
{
    public BranchRequestValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0).WithMessage("Company is required.");
        RuleFor(x => x.Code).RequiredCode(20);
        RuleFor(x => x.Name).RequiredText(100);
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Branch type is required.")
            .Must(type => BranchTypeText.TryParse(type, out _))
            .WithMessage("Branch type must be Head Office, Branch, Regional Office or Warehouse.");
        RuleFor(x => x.Email).OptionalText(120).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Enter a valid email address.");
        RuleFor(x => x.Phone).OptionalText(25).OptionalPhone();
        RuleFor(x => x.Address).OptionalText(200);
        RuleFor(x => x.City).RequiredText(60);
        RuleFor(x => x.Country).RequiredText(60);
        RuleFor(x => x.OpenedDate).NotEmpty().WithMessage("Opened date is required.");
        RuleFor(x => x.Status).RequiredStatus();
    }
}
