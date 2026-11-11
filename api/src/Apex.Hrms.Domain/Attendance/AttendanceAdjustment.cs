using System.Globalization;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>
/// What an employee asks for: new times or a status for a day that already has an attendance record.
/// </summary>
public sealed record AttendanceAdjustmentDetails(
    int EmployeeId,
    DateOnly Date,
    TimeOnly? RequestedCheckIn,
    TimeOnly? RequestedCheckOut,
    AttendanceStatus? RequestedStatus,
    string Reason)
{
    /// <summary>Trims the reason and drops the seconds of the times. Idempotent.</summary>
    public AttendanceAdjustmentDetails Normalized() => this with
    {
        Reason = TextNormalization.Clean(Reason),
        RequestedCheckIn = RequestedCheckIn is { } checkIn ? AttendanceRules.Truncate(checkIn) : null,
        RequestedCheckOut = RequestedCheckOut is { } checkOut ? AttendanceRules.Truncate(checkOut) : null,
    };
}

/// <summary>
/// A request to correct an attendance record (att.AttendanceAdjustment). Approving it applies the request to the record
/// (see <see cref="AttendanceRecord.ApplyAdjustment"/>); the times the record had when the request was made are kept as
/// a snapshot. At most one request per employee and date is pending (the database has a filtered unique index too).
/// </summary>
public sealed class AttendanceAdjustment : ApprovalRequest
{
    public const int ReasonMaxLength = 250;

    private AttendanceAdjustment()
    {
    }

    public TimeOnly? CurrentCheckIn { get; private set; }

    public TimeOnly? CurrentCheckOut { get; private set; }

    public TimeOnly? RequestedCheckIn { get; private set; }

    public TimeOnly? RequestedCheckOut { get; private set; }

    public AttendanceStatus? RequestedStatus { get; private set; }

    public override string Label => $"ADJ-{Id.ToString("0000", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Checks the rules that need only the request and the record it corrects: not in the future, something requested,
    /// and a check-out after the check-in (unless the shift is a night shift).
    /// </summary>
    public static Result Validate(
        AttendanceAdjustmentDetails details,
        TimeOnly? currentCheckIn,
        TimeOnly? currentCheckOut,
        bool isNightShift,
        DateOnly today)
    {
        var normalized = details.Normalized();
        var violations = CheckCommon(new Violations(), normalized.Date, today, normalized.Reason);
        violations.AddIf(
            normalized.RequestedCheckIn is null && normalized.RequestedCheckOut is null && normalized.RequestedStatus is null,
            "requestedCheckIn",
            "Enter a time or choose a status.",
            "Request at least one change: a time or a status.");
        var checkIn = normalized.RequestedCheckIn ?? currentCheckIn;
        var checkOut = normalized.RequestedCheckOut ?? currentCheckOut;
        violations.AddIf(
            checkIn is { } start && checkOut is { } end && !isNightShift && end <= start,
            "requestedCheckOut",
            "Must be after the check-in time.",
            "The check-out time must be after the check-in time.");
        return violations.ToResult();
    }

    /// <summary>Starts a pending request for a day whose record shows <paramref name="record"/>'s times.</summary>
    public static Result<AttendanceAdjustment> Create(
        AttendanceAdjustmentDetails details,
        AttendanceRecord record,
        bool isNightShift,
        DateOnly today)
    {
        var valid = Validate(details, record.CheckIn, record.CheckOut, isNightShift, today);
        if (valid.IsFailure)
        {
            return Result.Failure<AttendanceAdjustment>(valid.Error);
        }

        var normalized = details.Normalized();
        var adjustment = new AttendanceAdjustment
        {
            CurrentCheckIn = record.CheckIn,
            CurrentCheckOut = record.CheckOut,
            RequestedCheckIn = normalized.RequestedCheckIn,
            RequestedCheckOut = normalized.RequestedCheckOut,
            RequestedStatus = normalized.RequestedStatus,
        };
        adjustment.StartPending(normalized.EmployeeId, normalized.Date, normalized.Reason, today);
        return Result.Success(adjustment);
    }
}
