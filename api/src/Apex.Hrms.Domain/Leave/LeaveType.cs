using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Domain.Leave;

/// <summary>
/// The editable fields of a leave type. Entitlements are sample figures, not statutory ones. Status changes through
/// Activate and Deactivate.
/// </summary>
public sealed record LeaveTypeDetails(
    string Code,
    string Name,
    string? Description,
    decimal DefaultDaysPerYear,
    bool IsPaid,
    bool RequiresDocument,
    short MaxConsecutiveDays,
    bool CarryForward,
    decimal MaxCarryForwardDays,
    string Color,
    Gender? EligibleGender)
{
    /// <summary>Trims text and turns blank optional text into null. Idempotent.</summary>
    public LeaveTypeDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Description = TextNormalization.CleanOptional(Description),
        Color = TextNormalization.Clean(Color),
    };
}

/// <summary>A kind of leave (lve.LeaveType), for example Annual or Medical leave.</summary>
public sealed class LeaveType : CodedRecord
{
    public const string InvalidColorMessage = "Use a colour like #3f6fd8.";

    private LeaveType()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal DefaultDaysPerYear { get; private set; }

    public bool IsPaid { get; private set; }

    public bool RequiresDocument { get; private set; }

    public short MaxConsecutiveDays { get; private set; }

    public bool CarryForward { get; private set; }

    public decimal MaxCarryForwardDays { get; private set; }

    /// <summary>"#rrggbb".</summary>
    public string Color { get; private set; } = string.Empty;

    /// <summary>When set, only employees of this gender are eligible (for example maternity leave).</summary>
    public Gender? EligibleGender { get; private set; }

    /// <summary>True when the colour is "#" followed by six hexadecimal digits (as CK_LeaveType_Color).</summary>
    public static bool IsValidColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);

    public static Result<LeaveType> Create(LeaveTypeDetails details, RecordStatus status)
    {
        var type = new LeaveType();
        var result = type.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<LeaveType>(result.Error);
        }

        type.SetInitialStatus(status);
        return Result.Success(type);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(LeaveTypeDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations
            .Require("name", "Leave type name", normalized.Name)
            .AddIf(normalized.DefaultDaysPerYear < 0, "defaultDaysPerYear", "Must not be negative.")
            .AddIf(
                normalized.MaxConsecutiveDays < 1,
                "maxConsecutiveDays",
                "Must be at least 1.",
                "Maximum consecutive days must be at least 1.")
            .AddIf(normalized.MaxCarryForwardDays < 0, "maxCarryForwardDays", "Must not be negative.")
            .AddIf(
                normalized.CarryForward && normalized.MaxCarryForwardDays > normalized.DefaultDaysPerYear,
                "maxCarryForwardDays",
                "Must not exceed the yearly entitlement.",
                "Carry-forward days cannot exceed the yearly entitlement.")
            .AddIf(!IsValidColor(normalized.Color), "color", InvalidColorMessage);
        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(LeaveTypeDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        Name = normalized.Name;
        Description = normalized.Description;
        DefaultDaysPerYear = normalized.DefaultDaysPerYear;
        IsPaid = normalized.IsPaid;
        RequiresDocument = normalized.RequiresDocument;
        MaxConsecutiveDays = normalized.MaxConsecutiveDays;
        CarryForward = normalized.CarryForward;
        MaxCarryForwardDays = normalized.MaxCarryForwardDays;
        Color = normalized.Color;
        EligibleGender = normalized.EligibleGender;
        return Result.Success();
    }
}
