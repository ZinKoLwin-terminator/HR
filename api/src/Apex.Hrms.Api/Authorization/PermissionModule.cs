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

    /// <summary>Upload Excel files and import them (the module's own create and edit permissions apply to the rows).</summary>
    Import,

    /// <summary>Download Excel exports (the module's own View permission applies to the rows).</summary>
    Export,

    /// <summary>Read the do-not-rehire list (including the reasons). Sort order 12 in the database seed; only HR Administrator holds it.</summary>
    ViewBlacklist,

    /// <summary>Add and lift do-not-rehire entries. Sort order 13 in the database seed.</summary>
    EditBlacklist,
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
        [.. Standard, Permission.ViewSensitive, Permission.EditSensitive, Permission.ViewBank, Permission.EditBank, Permission.ViewBlacklist, Permission.EditBlacklist]);

    public static readonly PermissionModule Leave = new("leave", [.. Standard, Permission.Approve]);

    public static readonly PermissionModule Attendance = new("attendance", [.. Standard, Permission.Approve]);

    /// <summary>Payroll has no Delete in the database seed (View, Create, Edit, Approve only): deleting a draft period or a salary row needs Edit.</summary>
    public static readonly PermissionModule Payroll = new("payroll", [Permission.View, Permission.Create, Permission.Edit, Permission.Approve]);

    /// <summary>The dashboard is read only.</summary>
    public static readonly PermissionModule Dashboard = new("dashboard", [Permission.View]);

    /// <summary>The activity log is read only and append-only: there is no way to change or remove an entry.</summary>
    public static readonly PermissionModule Activity = new("activity", [Permission.View]);

    public static readonly PermissionModule Recruitment = new("recruitment", Standard);

    /// <summary>Users, roles, the permission matrix and the general settings. Every endpoint also needs the HR Administrator role (see AdministratorRequirement).</summary>
    public static readonly PermissionModule Settings = new("settings", Standard);


    /// <summary>Excel import and export: the file transfer itself. Rows are protected by the permissions of the module they belong to.</summary>
    public static readonly PermissionModule Excel = new("excel", [Permission.View, Permission.Import, Permission.Export]);


    public static IReadOnlyList<PermissionModule> All { get; } = [Organization, Employees, Leave, Attendance, Payroll, Recruitment, Dashboard, Activity, Settings, Excel];

    public string PolicyName(Permission permission) => $"{Key}:{permission}";
}
