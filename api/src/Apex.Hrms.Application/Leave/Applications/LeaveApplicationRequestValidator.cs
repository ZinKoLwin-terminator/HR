using FluentValidation;

namespace Apex.Hrms.Application.Leave.Applications;

/// <summary>Limits of a leave request that follow from the database columns.</summary>
public static class LeaveApplicationLimits
{
    public const int ReasonMaxLength = 500;
    public const int DocumentNameMaxLength = 260;
    public const int RemarksMaxLength = 250;

    /// <summary>The longest range a request may cover (days = decimal(4,1) holds at most 999.9; a year is more than any rule allows).</summary>
    public const int MaxRangeDays = 366;
}

/// <summary>Shape rules shared by the create and update requests. Whether the dates make sense is the domain's rule.</summary>
internal abstract class LeaveApplicationFieldsValidator<TRequest> : RequestValidator<TRequest>
    where TRequest : ILeaveApplicationFields
{
    protected LeaveApplicationFieldsValidator()
    {
        RuleForText(x => x.Reason, "Reason").RequiredText(LeaveApplicationLimits.ReasonMaxLength);
        RuleForText(x => x.DocumentName, "Document name").OptionalText(LeaveApplicationLimits.DocumentNameMaxLength);
        RuleFor(x => x.EndDate)
            .Must((request, end) => end < request.StartDate || end.DayNumber - request.StartDate.DayNumber < LeaveApplicationLimits.MaxRangeDays)
            .WithMessage($"Leave cannot cover more than {LeaveApplicationLimits.MaxRangeDays} days at a time.");
    }
}

internal sealed class LeaveApplicationCreateRequestValidator : LeaveApplicationFieldsValidator<LeaveApplicationCreateRequest>;

internal sealed class LeaveApplicationUpdateRequestValidator : LeaveApplicationFieldsValidator<LeaveApplicationUpdateRequest>
{
    public LeaveApplicationUpdateRequestValidator() => RuleForText(x => x.RowVersion).RequiredRowVersion();
}
