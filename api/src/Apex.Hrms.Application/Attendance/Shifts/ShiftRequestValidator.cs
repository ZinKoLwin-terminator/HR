using FluentValidation;

namespace Apex.Hrms.Application.Attendance.Shifts;

/// <summary>Limits of a shift that follow from the database columns.</summary>
public static class ShiftLimits
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 60;

    /// <summary>Break and grace are smallint columns; a day has 1440 minutes, so more makes no sense.</summary>
    public const int MaxMinutes = 1440;
}

/// <summary>Shape rules shared by the create and update requests. Whether the times and days make sense is the domain's rule.</summary>
internal abstract class ShiftFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : IShiftFields
{
    protected ShiftFieldsValidator()
    {
        RuleForText(x => x.Code, "Code").RequiredCode(ShiftLimits.CodeMaxLength);
        RuleForText(x => x.Name, "Shift name").RequiredText(ShiftLimits.NameMaxLength);
        RuleFor(x => x.BreakMinutes).LessThanOrEqualTo(ShiftLimits.MaxMinutes)
            .WithMessage($"Must be at most {ShiftLimits.MaxMinutes} minutes.");
        RuleFor(x => x.GraceMinutes).LessThanOrEqualTo(ShiftLimits.MaxMinutes)
            .WithMessage($"Must be at most {ShiftLimits.MaxMinutes} minutes.");
    }
}

internal sealed class ShiftCreateRequestValidator : ShiftFieldsValidator<ShiftCreateRequest>
{
    public ShiftCreateRequestValidator() => RuleFor(x => x.Status).RequiredStatus();
}

internal sealed class ShiftUpdateRequestValidator : ShiftFieldsValidator<ShiftUpdateRequest>
{
    public ShiftUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
