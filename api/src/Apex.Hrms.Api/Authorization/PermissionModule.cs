namespace Apex.Hrms.Api.Authorization;

/// <summary>What a caller does with a resource; each verb has its own named policy per module.</summary>
internal enum Permission
{
    View,
    Create,
    Edit,
    Delete,

    /// <summary>Decide a workflow request (leave: approve or reject). Sort order 5 in the database seed.</summary>
    Approve,

    /// <summary>Read personal data (national id, date of birth, personal contact, address, emergency contact, documents).</summary>
    ViewSensitive,

    /// <summary>Change personal data.</summary>
    EditSensitive,

    /// <summary>Read salary bank details in full.</summary>
    ViewBank,

    /// <summary>Change salary bank details.</summary>
    EditBank,
}

/// <summary>
/// A group of endpoints that share one permission key, e.g. "organization". Its policies are named
/// <c>organization:View</c>, <c>organization:Create</c>, <c>organization:Edit</c> and <c>organization:Delete</c>; a
/// module that holds personal data also declares the sensitive and bank permissions (<c>employees:ViewSensitive</c>).
/// Add every new module to <see cref="All"/> so its policies are registered.
/// </summary>
internal sealed record PermissionModule(string Key, IReadOnlyList<Permission> Permissions)
{
    private static readonly Permission[] Standard = [Permission.View, Permission.Create, Permission.Edit, Permission.Delete];

    public static readonly PermissionModule Organization = new("organization", Standard);

    public static readonly PermissionModule Employees = new(
        "employees",
        [.. Standard, Permission.ViewSensitive, Permission.EditSensitive, Permission.ViewBank, Permission.EditBank]);

    public static readonly PermissionModule Leave = new("leave", [.. Standard, Permission.Approve]);

    public static readonly PermissionModule Attendance = new("attendance", [.. Standard, Permission.Approve]);

    /// <summary>The dashboard is read only.</summary>
    public static readonly PermissionModule Dashboard = new("dashboard", [Permission.View]);

    /// <summary>The activity log is read only and append-only: there is no way to change or remove an entry.</summary>
    public static readonly PermissionModule Activity = new("activity", [Permission.View]);

    public static IReadOnlyList<PermissionModule> All { get; } = [Organization, Employees, Leave, Attendance, Dashboard, Activity];

    public string PolicyName(Permission permission) => $"{Key}:{permission}";
}
