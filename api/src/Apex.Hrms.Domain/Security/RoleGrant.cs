namespace Apex.Hrms.Domain.Security;

/// <summary>One granted (role, module, action) row of sec.RolePermission.</summary>
public sealed class RoleGrant
{
    private RoleGrant()
    {
    }

    public int RoleId { get; private set; }

    public string ModuleKey { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public static RoleGrant Create(int roleId, string moduleKey, string action) =>
        new() { RoleId = roleId, ModuleKey = moduleKey, Action = action };
}

/// <summary>A module of the permission matrix with the actions that make sense for it (sec.PermissionModule and sec.PermissionModuleAction).</summary>
public sealed record PermissionModuleInfo(string Key, string Label, IReadOnlyList<string> Actions);
