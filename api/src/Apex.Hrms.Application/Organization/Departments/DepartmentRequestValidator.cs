using FluentValidation;

namespace Apex.Hrms.Application.Organization.Departments;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class DepartmentFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IDepartmentFields
{
    protected DepartmentFieldsValidator()
    {
        RuleFor(x => x.CompanyId).GreaterThan(0).WithMessage("Company is required.");
        RuleFor(x => x.BranchId).GreaterThan(0).WithMessage("Branch is required.");
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Department name").RequiredText(100);
        RuleForText(x => x.Description, "Description").OptionalText(300);
        RuleForText(x => x.CostCenter, "Cost center").OptionalText(20);
    }
}

internal sealed class DepartmentCreateRequestValidator : DepartmentFieldsValidator<DepartmentCreateRequest>
{
    public DepartmentCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class DepartmentUpdateRequestValidator : DepartmentFieldsValidator<DepartmentUpdateRequest>
{
    public DepartmentUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
