namespace Apex.Hrms.Domain.Security;

/// <summary>One granted (role, module, action) row of sec.RoleGrant. Read only here.</summary>
public sealed class RoleGrant
{
    private RoleGrant()
    {
    }

    public int RoleId { get; private set; }

    public string ModuleKey { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;
}
