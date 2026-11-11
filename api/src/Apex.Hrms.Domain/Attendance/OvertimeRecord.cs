using System.Globalization;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>What an overtime request states. The minutes are not one of them: they are worked out from the two times.</summary>
public sealed record OvertimeDetails(int EmployeeId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Reason)
{
    /// <summary>Trims the reason and drops the seconds of the times. Idempotent.</summary>
    public OvertimeDetails Normalized() => this with
    {
        Reason = TextNormalization.Clean(Reason),
        StartTime = AttendanceRules.Truncate(StartTime),
        EndTime = AttendanceRules.Truncate(EndTime),
    };
}

/// <summary>
/// An overtime request (att.OvertimeRecord): from a start to an end time on a day, 15 minutes to 12 hours. The minutes
/// are the time from start to end, rolling over midnight, as the table's check constraint requires. One entry per
/// employee and day (the Application layer checks it; the table has a unique key too).
/// </summary>
public sealed class OvertimeRecord : ApprovalRequest
{
    public const int ReasonMaxLength = 120;

    private OvertimeRecord()
    {
    }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public int Minutes { get; private set; }

    public override string Label => $"OT-{Id.ToString("0000", CultureInfo.InvariantCulture)}";

    public static Result Validate(OvertimeDetails details, DateOnly today)
    {
        var normalized = details.Normalized();
        var violations = CheckCommon(new Violations(), normalized.Date, today, normalized.Reason);
        var minutes = AttendanceRules.MinutesBetween(normalized.StartTime, normalized.EndTime);
        violations.AddIf(
            minutes < AttendanceRules.OvertimeMinMinutes || minutes > AttendanceRules.OvertimeMaxMinutes,
            "endTime",
            "Duration must be between 15 minutes and 12 hours.",
            "Overtime must be between 15 minutes and 12 hours.");
        return violations.ToResult();
    }

    public static Result<OvertimeRecord> Create(OvertimeDetails details, DateOnly today)
    {
        var valid = Validate(details, today);
        if (valid.IsFailure)
        {
            return Result.Failure<OvertimeRecord>(valid.Error);
        }

        var normalized = details.Normalized();
        var overtime = new OvertimeRecord
        {
            StartTime = normalized.StartTime,
            EndTime = normalized.EndTime,
            Minutes = AttendanceRules.MinutesBetween(normalized.StartTime, normalized.EndTime),
        };
        overtime.StartPending(normalized.EmployeeId, normalized.Date, normalized.Reason, today);
        return Result.Success(overtime);
    }
}
