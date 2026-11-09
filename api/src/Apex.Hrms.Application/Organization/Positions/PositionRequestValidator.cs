using FluentValidation;

namespace Apex.Hrms.Application.Organization.Positions;

/// <summary>Shape rules shared by the create and update requests. Labels match the domain wording.</summary>
internal abstract class PositionFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IPositionFields
{
    protected PositionFieldsValidator()
    {
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Department is required.");
        RuleFor(x => x.JobGradeId).GreaterThan(0).WithMessage("Job grade is required.");
        RuleForText(x => x.Code, "Code").RequiredCode(20);
        RuleForText(x => x.Title, "Position title").RequiredText(100);
        RuleForText(x => x.Description, "Description").OptionalText(300);
        RuleFor(x => x.Headcount).GreaterThanOrEqualTo((short)1).WithMessage("Headcount must be at least 1.");
    }
}

internal sealed class PositionCreateRequestValidator : PositionFieldsValidator<PositionCreateRequest>
{
    public PositionCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class PositionUpdateRequestValidator : PositionFieldsValidator<PositionUpdateRequest>
{
    public PositionUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
