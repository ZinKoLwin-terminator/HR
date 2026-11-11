namespace Apex.Hrms.Domain.Activity;

/// <summary>
/// The categories of the activity feed. They are the values the CHECK constraint of app.ActivityLog allows and the
/// ActivityCategory union of the frontend.
/// </summary>
public static class ActivityCategories
{
    public const string Employee = "employee";
    public const string Organization = "organization";
    public const string Attendance = "attendance";
    public const string Leave = "leave";
    public const string Payroll = "payroll";
    public const string Recruitment = "recruitment";
    public const string System = "system";

    public static IReadOnlyList<string> All { get; } =
        [Employee, Organization, Attendance, Leave, Payroll, Recruitment, System];

    public static bool IsValid(string? category) => category is not null && All.Contains(category, StringComparer.Ordinal);
}
