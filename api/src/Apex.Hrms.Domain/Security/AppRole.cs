using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Security;

/// <summary>A role (sec.Role). Read only here: roles are administered elsewhere.</summary>
public sealed class AppRole : AuditedEntity
{
    private AppRole()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsSystem { get; private set; }

    public RecordStatus Status { get; private set; }
}
