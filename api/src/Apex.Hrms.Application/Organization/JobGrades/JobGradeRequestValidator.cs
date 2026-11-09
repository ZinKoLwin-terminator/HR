using FluentValidation;

namespace Apex.Hrms.Application.Organization.JobGrades;

internal sealed class JobGradeRequestValidator : RequestValidator<JobGradeRequest>
{
    public JobGradeRequestValidator()
    {
        RuleFor(x => x.Code).RequiredCode(20);
        RuleFor(x => x.Name).RequiredText(60);
        RuleFor(x => x.Level).GreaterThanOrEqualTo((short)1).WithMessage("Level must be at least 1.");
        RuleFor(x => x.MinSalary)
            .GreaterThanOrEqualTo(0).WithMessage("Must not be negative.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.MaxSalary)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true).WithMessage("Use at most 2 decimal places.");
        RuleFor(x => x.Description).OptionalText(300);
        RuleFor(x => x.Status).RequiredStatus();
    }
}
