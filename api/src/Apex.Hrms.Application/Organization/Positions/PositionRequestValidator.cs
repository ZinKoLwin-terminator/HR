using FluentValidation;

namespace Apex.Hrms.Application.Organization.Positions;

internal sealed class PositionRequestValidator : RequestValidator<PositionRequest>
{
    public PositionRequestValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Department is required.");
        RuleFor(x => x.JobGradeId).GreaterThan(0).WithMessage("Job grade is required.");
        RuleFor(x => x.Code).RequiredCode(20);
        RuleFor(x => x.Title).RequiredText(100);
        RuleFor(x => x.Description).OptionalText(300);
        RuleFor(x => x.Headcount).GreaterThanOrEqualTo((short)1).WithMessage("Headcount must be at least 1.");
        RuleFor(x => x.Status).RequiredStatus();
    }
}
