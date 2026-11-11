using System.Globalization;

namespace Apex.Hrms.Application.Activity;

/// <summary>
/// The wording of the activity feed, ported from the frontend mock services so both feeds read alike. A subject is an
/// id or a code (a name for records that are not people), never personal data: the mock names employees, the API
/// uses the employee code.
/// </summary>
public static class ActivityMessages
{
    /// <summary>The generic CRUD wording: <c>Department "Engineering" created</c>.</summary>
    public static string Created(string label, string subject) => $"{label} \"{subject}\" created";

    public static string Updated(string label, string subject) => $"{label} \"{subject}\" updated";

    public static string Deleted(string label, string subject) => $"{label} \"{subject}\" deleted";

    /// <summary>A leave application moved on: <c>LV-2026-0004 submitted for approval</c>.</summary>
    public static string LeaveSubmitted(string applicationNo) => $"{applicationNo} submitted for approval";

    /// <summary>Approved or rejected: the decision in lower case, as the mock writes it.</summary>
    public static string LeaveDecided(string applicationNo, string decision) =>
        $"{applicationNo} {decision.ToLowerInvariant()}";

    public static string LeaveCancelled(string applicationNo) => $"{applicationNo} cancelled";

    /// <summary>A punch: <c>EMP-0007 checked in at 09:02</c> (the mock names the employee; the API uses the code).</summary>
    public static string Punch(string employeeCode, bool checkIn, TimeOnly time) =>
        $"{employeeCode} checked {(checkIn ? "in" : "out")} at {time.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    /// <summary>An attendance adjustment decided: <c>ADJ-0004 approved for 2026-11-09</c>.</summary>
    public static string AdjustmentDecided(string label, string decision, DateOnly date) =>
        $"{label} {decision.ToLowerInvariant()} for {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>An overtime request decided: <c>OT-0004 approved</c>.</summary>
    public static string OvertimeDecided(string label, string decision) => $"{label} {decision.ToLowerInvariant()}";

    /// <summary>A job opening was moved by a command: <c>Job opening "JOB-2026-007 Backend Engineer" set to On Hold</c>.</summary>
    public static string JobOpeningStatusSet(string subject, string status) => $"Job opening \"{subject}\" set to {status}";

    /// <summary>An opening became Filled (<c>filled</c>) or Open again (<c>reopened</c>) because of a selection.</summary>
    public static string JobOpeningFilled(string subject) => $"Job opening \"{subject}\" filled";

    public static string JobOpeningReopened(string subject) => $"Job opening \"{subject}\" reopened";

    /// <summary>An application moved to a stage: <c>Ada Test for Backend Engineer moved to Screening</c>.</summary>
    public static string StageMoved(string applicationLabel, string stage) => $"{applicationLabel} moved to {stage}";
}
