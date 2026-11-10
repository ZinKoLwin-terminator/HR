using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Leave;

/// <summary>The life of a leave application. Draft and Submitted are open; Approved leave can still be cancelled until it starts.</summary>
public enum LeaveStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Cancelled,
}

/// <summary>How a policy grants its days: all at once, or one twelfth per month of the current year.</summary>
public enum LeaveAccrual
{
    Upfront,
    Monthly,
}

public static class LeaveStatusText
{
    public static readonly TextMap<LeaveStatus> Map = new(new Dictionary<LeaveStatus, string>
    {
        [LeaveStatus.Draft] = "Draft",
        [LeaveStatus.Submitted] = "Submitted",
        [LeaveStatus.Approved] = "Approved",
        [LeaveStatus.Rejected] = "Rejected",
        [LeaveStatus.Cancelled] = "Cancelled",
    });

    public static string ToText(LeaveStatus value) => Map.ToText(value);

    public static LeaveStatus Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out LeaveStatus value) => Map.TryParse(text, out value);
}

public static class LeaveAccrualText
{
    public static readonly TextMap<LeaveAccrual> Map = new(new Dictionary<LeaveAccrual, string>
    {
        [LeaveAccrual.Upfront] = "Upfront",
        [LeaveAccrual.Monthly] = "Monthly",
    });

    public static string ToText(LeaveAccrual value) => Map.ToText(value);

    public static LeaveAccrual Parse(string? text) => Map.Parse(text);

    public static bool TryParse(string? text, out LeaveAccrual value) => Map.TryParse(text, out value);
}
