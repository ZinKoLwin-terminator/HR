using FluentValidation;

namespace Apex.Hrms.Application.Organization.JobGrades;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class JobGradeFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IJobGradeFields
{
    protected JobGradeFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Name, "Grade name").RequiredText(60);
        RuleFor(x => x.Level).GreaterThanOrEqualTo((short)1).WithMessage("Level must be at least 1.");
        RuleFor(x => x.MinSalary)
            .GreaterThanOrEqualTo(0).WithMessage("Must not be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.MaxSalary)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleForText(x => x.Description, "Description").OptionalText(300);
    }
}

internal sealed class JobGradeCreateRequestValidator : JobGradeFieldsValidator<JobGradeCreateRequest>
{
    public JobGradeCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class JobGradeUpdateRequestValidator : JobGradeFieldsValidator<JobGradeUpdateRequest>
{
    public JobGradeUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
