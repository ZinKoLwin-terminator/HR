using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>
/// The editable fields of a shift. <see cref="WorkDays"/> are weekday numbers, 0 = Sunday to 6 = Saturday. Status
/// changes through Activate and Deactivate.
/// </summary>
public sealed record ShiftDetails(
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceMinutes,
    IReadOnlyList<int> WorkDays,
    bool IsNightShift,
    string Color)
{
    /// <summary>Trims text, drops the seconds of the times and sorts the working days. Idempotent.</summary>
    public ShiftDetails Normalized() => this with
    {
        Code = TextNormalization.Clean(Code),
        Name = TextNormalization.Clean(Name),
        Color = TextNormalization.Clean(Color),
        StartTime = AttendanceRules.Truncate(StartTime),
        EndTime = AttendanceRules.Truncate(EndTime),
        WorkDays = [.. (WorkDays ?? []).Distinct().Order()],
    };
}

/// <summary>One working weekday of a shift (att.ShiftWorkDay).</summary>
public sealed class ShiftWorkDay
{
    private ShiftWorkDay()
    {
    }

    internal ShiftWorkDay(int dayOfWeek) => DayOfWeek = (byte)dayOfWeek;

    public int ShiftId { get; private set; }

    /// <summary>0 = Sunday ... 6 = Saturday.</summary>
    public byte DayOfWeek { get; private set; }
}

/// <summary>
/// A working-time pattern (att.Shift): start and end, break, grace period and working weekdays. A night shift may end
/// at or before its start time, which means the next day.
/// </summary>
public sealed class Shift : CodedRecord
{
    public const string InvalidColorMessage = "Use a colour like #3f6fd8.";

    private readonly List<ShiftWorkDay> workDays = [];

    private Shift()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public int BreakMinutes { get; private set; }

    public int GraceMinutes { get; private set; }

    public bool IsNightShift { get; private set; }

    /// <summary>"#rrggbb".</summary>
    public string Color { get; private set; } = string.Empty;

    public IReadOnlyCollection<ShiftWorkDay> WorkDays => workDays;

    public ShiftRules Rules => new(StartTime, EndTime, BreakMinutes, GraceMinutes, IsNightShift);

    /// <summary>The working weekdays, 0 = Sunday ... 6 = Saturday, ascending.</summary>
    public IReadOnlyList<int> WorkDayNumbers => [.. workDays.Select(day => (int)day.DayOfWeek).Order()];

    /// <summary>True when <paramref name="date"/> falls on one of the working weekdays of the shift.</summary>
    public bool WorksOn(DateOnly date) => workDays.Any(day => day.DayOfWeek == (int)date.DayOfWeek);

    public static bool IsValidColor(string? color) =>
        color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);

    public static Result<Shift> Create(ShiftDetails details, RecordStatus status)
    {
        var shift = new Shift();
        var result = shift.Update(details);
        if (result.IsFailure)
        {
            return Result.Failure<Shift>(result.Error);
        }

        shift.SetInitialStatus(status);
        return Result.Success(shift);
    }

    /// <summary>Checks the rules that need no other record, without changing anything.</summary>
    public static Result Validate(ShiftDetails details)
    {
        var normalized = details.Normalized();
        var violations = new Violations();
        CheckCode(violations, normalized.Code);
        violations
            .Require("name", "Shift name", normalized.Name)
            .AddIf(normalized.BreakMinutes < 0, "breakMinutes", "Must not be negative.")
            .AddIf(normalized.GraceMinutes < 0, "graceMinutes", "Must not be negative.")
            .AddIf(!IsValidColor(normalized.Color), "color", InvalidColorMessage);

        if (!normalized.IsNightShift && normalized.EndTime <= normalized.StartTime)
        {
            violations.Add(
                "endTime",
                "Must be after the start time (or mark the shift as a night shift).",
                "The end time must be after the start time for a day shift.");
        }

        if (normalized.WorkDays.Count == 0)
        {
            violations.Add("workDays", "Choose at least one day.", "Choose at least one working day.");
        }
        else if (normalized.WorkDays.Any(day => day is < 0 or > 6))
        {
            violations.Add("workDays", "Use 0 (Sunday) to 6 (Saturday).");
        }

        return violations.ToResult();
    }

    /// <summary>Applies the details, or changes nothing when any rule is violated. Status is left untouched.</summary>
    public Result Update(ShiftDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        AssignCode(normalized.Code);
        Name = normalized.Name;
        StartTime = normalized.StartTime;
        EndTime = normalized.EndTime;
        BreakMinutes = normalized.BreakMinutes;
        GraceMinutes = normalized.GraceMinutes;
        IsNightShift = normalized.IsNightShift;
        Color = normalized.Color;

        workDays.RemoveAll(day => !normalized.WorkDays.Contains(day.DayOfWeek));
        foreach (var number in normalized.WorkDays.Where(number => workDays.All(day => day.DayOfWeek != number)))
        {
            workDays.Add(new ShiftWorkDay(number));
        }

        return Result.Success();
    }
}
