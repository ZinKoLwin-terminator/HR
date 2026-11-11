using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>The editable fields of a holiday. <see cref="Type"/> is "Public" or "Company".</summary>
public sealed record HolidayDetails(DateOnly Date, string Name, string Type)
{
    /// <summary>Trims the text. Idempotent.</summary>
    public HolidayDetails Normalized() => this with
    {
        Name = TextNormalization.Clean(Name),
        Type = TextNormalization.Clean(Type),
    };
}

/// <summary>
/// A public or company holiday (att.Holiday). The leave module leaves these dates out of working-day counts and shows
/// them on its calendar; attendance owns writing them. At most one holiday falls on a date (checked by the
/// Application layer: the table has no unique index on the date).
/// </summary>
public sealed class Holiday : AuditedEntity
{
    public const string PublicType = "Public";
    public const string CompanyType = "Company";

    private Holiday()
    {
    }

    public DateOnly Date { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>"Public" or "Company".</summary>
    public string Type { get; private set; } = string.Empty;

    public static Result<Holiday> Create(HolidayDetails details)
    {
        var holiday = new Holiday();
        var result = holiday.Update(details);
        return result.IsFailure ? Result.Failure<Holiday>(result.Error) : Result.Success(holiday);
    }

    public static Result Validate(HolidayDetails details)
    {
        var normalized = details.Normalized();
        return new Violations()
            .Require("name", "Holiday name", normalized.Name)
            .AddIf(
                normalized.Type is not (PublicType or CompanyType),
                "type",
                "Type must be Public or Company.")
            .ToResult();
    }

    public Result Update(HolidayDetails details)
    {
        var valid = Validate(details);
        if (valid.IsFailure)
        {
            return valid;
        }

        var normalized = details.Normalized();
        Date = normalized.Date;
        Name = normalized.Name;
        Type = normalized.Type;
        return Result.Success();
    }
}
