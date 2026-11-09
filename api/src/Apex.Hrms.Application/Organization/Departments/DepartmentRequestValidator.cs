using FluentValidation;

namespace Apex.Hrms.Application.Organization.Departments;

internal sealed class DepartmentRequestValidator : RequestValidator<DepartmentRequest>
{
    public DepartmentRequestValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0).WithMessage("Company is required.");
        RuleFor(x => x.BranchId).GreaterThan(0).WithMessage("Branch is required.");
        RuleFor(x => x.Code).RequiredCode(20);
        RuleFor(x => x.Name).RequiredText(100);
        RuleFor(x => x.Description).OptionalText(300);
        RuleFor(x => x.CostCenter).OptionalText(20);
        RuleFor(x => x.Status).RequiredStatus();
    }
}
