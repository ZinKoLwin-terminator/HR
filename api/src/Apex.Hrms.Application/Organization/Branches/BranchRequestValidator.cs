using Apex.Hrms.Domain.Organization;
using FluentValidation;

namespace Apex.Hrms.Application.Organization.Branches;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class BranchFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IBranchFields
{
    protected BranchFieldsValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0).WithMessage("Company is required.");
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Branch name").RequiredText(100);
        RuleForText(x => x.Type, "Branch type")
            .NotEmpty().WithMessage("Branch type is required.")
            .Must(type => BranchTypeText.TryParse(type, out _))
            .WithMessage("Branch type must be Head Office, Branch, Regional Office or Warehouse.");
        RuleForText(x => x.Email, "Email").OptionalText(120).ValidEmail();
        RuleForText(x => x.Phone, "Phone").OptionalText(25).OptionalPhone();
        RuleForText(x => x.Address, "Address").OptionalText(200);
        RuleForText(x => x.City, "City").RequiredText(60);
        RuleForText(x => x.Country, "Country").RequiredText(60);
        RuleFor(x => x.OpenedDate).NotEmpty().WithMessage("Opened date is required.");
    }
}

internal sealed class BranchCreateRequestValidator : BranchFieldsValidator<BranchCreateRequest>
{
    public BranchCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class BranchUpdateRequestValidator : BranchFieldsValidator<BranchUpdateRequest>
{
    public BranchUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
