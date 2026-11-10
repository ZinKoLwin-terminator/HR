using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Leave;
using FluentValidation;

namespace Apex.Hrms.Application.Leave.LeavePolicies;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class LeavePolicyFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ILeavePolicyFields
{
    protected LeavePolicyFieldsValidator()
    {
        RuleForText(x => x.Name, "Policy name").RequiredText(100);
        RuleFor(x => x.DaysPerYear)
            .PrecisionScale(5, 1, ignoreTrailingZeros: true).WithMessage("Use at most 1 decimal place.");
        RuleForText(x => x.Accrual, "Accrual")
            .NotEmpty().WithMessage("Accrual is required.")
            .Must(accrual => LeaveAccrualText.TryParse(accrual?.Trim(), out _))
            .WithMessage($"Accrual must be {LeaveAccrualText.Map.OneOf}.");
        RuleForText(x => x.EmploymentType, "Employment type")
            .Must(type => string.IsNullOrWhiteSpace(type) || EmploymentTypeText.TryParse(type.Trim(), out _))
            .WithMessage($"Employment type must be {EmploymentTypeText.Map.OneOf}, or empty for everyone.");
    }
}

internal sealed class LeavePolicyCreateRequestValidator : LeavePolicyFieldsValidator<LeavePolicyCreateRequest>
{
    public LeavePolicyCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class LeavePolicyUpdateRequestValidator : LeavePolicyFieldsValidator<LeavePolicyUpdateRequest>
{
    public LeavePolicyUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
