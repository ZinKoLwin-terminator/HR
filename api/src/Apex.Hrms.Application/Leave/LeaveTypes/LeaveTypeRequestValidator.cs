using Apex.Hrms.Domain.Employees;
using FluentValidation;

namespace Apex.Hrms.Application.Leave.LeaveTypes;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class LeaveTypeFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ILeaveTypeFields
{
    private const string OneDecimal = "Use at most 1 decimal place.";

    protected LeaveTypeFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Leave type name").RequiredText(60);
        RuleForText(x => x.Description, "Description").OptionalText(300);
        RuleFor(x => x.DefaultDaysPerYear)
            .PrecisionScale(5, 1, ignoreTrailingZeros: true).WithMessage(OneDecimal);
        RuleFor(x => x.MaxCarryForwardDays)
            .PrecisionScale(5, 1, ignoreTrailingZeros: true).WithMessage(OneDecimal);
        RuleForText(x => x.EligibleGender, "Eligible gender")
            .Must(gender => string.IsNullOrWhiteSpace(gender) || GenderText.TryParse(gender.Trim(), out _))
            .WithMessage($"Eligible gender must be {GenderText.Map.OneOf}, or empty for everyone.");
    }
}

internal sealed class LeaveTypeCreateRequestValidator : LeaveTypeFieldsValidator<LeaveTypeCreateRequest>
{
    public LeaveTypeCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class LeaveTypeUpdateRequestValidator : LeaveTypeFieldsValidator<LeaveTypeUpdateRequest>
{
    public LeaveTypeUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
