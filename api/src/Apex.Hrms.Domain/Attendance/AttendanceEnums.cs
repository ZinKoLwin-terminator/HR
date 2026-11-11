using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Attendance;

/// <summary>The outcome of a day: what the attendance record says about it.</summary>
public enum AttendanceStatus
{
    Present,
    Absent,
    Late,
    HalfDay,
    OnLeave,
    Holiday,
    Weekend,
}

/// <summary>How a record came to be. The API only creates Web records (punches) and Manual ones (approved adjustments).</summary>
public enum AttendanceSource
{
    Biometric,
    Web,
    Mobile,
    Manual,
}

/// <summary>The life of an adjustment or an overtime request.</summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
}

public enum PunchKind
{
    CheckIn,
    CheckOut,
}

public static class AttendanceStatusText
{
    public static readonly TextMap<AttendanceStatus> Map = new(new Dictionary<AttendanceStatus, string>
    {
        [AttendanceStatus.Present] = "Present",
        [AttendanceStatus.Absent] = "Absent",
        [AttendanceStatus.Late] = "Late",
        [AttendanceStatus.HalfDay] = "Half Day",
        [AttendanceStatus.OnLeave] = "On Leave",
        [AttendanceStatus.Holiday] = "Holiday",
        [AttendanceStatus.Weekend] = "Weekend",
    });

    public static string ToText(AttendanceStatus value) => Map.ToText(value);

    public static AttendanceStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out AttendanceStatus value) => Map.TryParse(text, out value);
}

public static class AttendanceSourceText
{
    public static readonly TextMap<AttendanceSource> Map = new(new Dictionary<AttendanceSource, string>
    {
        [AttendanceSource.Biometric] = "Biometric",
        [AttendanceSource.Web] = "Web",
        [AttendanceSource.Mobile] = "Mobile",
        [AttendanceSource.Manual] = "Manual",
    });

    public static string ToText(AttendanceSource value) => Map.ToText(value);

    public static AttendanceSource Parse(string? text) => Map.Parse(text);
}

public static class ApprovalStatusText
{
    public static readonly TextMap<ApprovalStatus> Map = new(new Dictionary<ApprovalStatus, string>
    {
        [ApprovalStatus.Pending] = "Pending",
        [ApprovalStatus.Approved] = "Approved",
        [ApprovalStatus.Rejected] = "Rejected",
    });

    public static string ToText(ApprovalStatus value) => Map.ToText(value);

    public static ApprovalStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out ApprovalStatus value) => Map.TryParse(text, out value);
}

public static class PunchKindText
{
    public static readonly TextMap<PunchKind> Map = new(new Dictionary<PunchKind, string>
    {
        [PunchKind.CheckIn] = "CheckIn",
        [PunchKind.CheckOut] = "CheckOut",
    });

    public static string ToText(PunchKind value) => Map.ToText(value);

    public static PunchKind Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out PunchKind value) => Map.TryParse(text, out value);
}
