using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Security;

/// <summary>
/// A business account with its role, as sign-in and the session check need it. <see cref="Permissions"/> holds
/// "module:Action" keys (for example <c>employees:View</c>) and is empty unless requested.
/// </summary>
public sealed record AccountSnapshot(
    int Id,
    int? EmployeeId,
    string DisplayName,
    string Email,
    string? ExternalSubject,
    AccountStatus Status,
    int RoleId,
    string RoleName,
    bool RoleActive,
    bool EmployeeLeft,
    IReadOnlyList<string> Permissions)
{
    /// <summary>An account signs in only when it is Active, its role is active and its linked employee has no exit date.</summary>
    public bool CanSignIn => Status == AccountStatus.Active && RoleActive && !EmployeeLeft;
}

/// <summary>Reads the business accounts (sec.AppUser) that identity users are linked to.</summary>
public interface IAccountDirectory
{
    /// <summary>The account linked to an identity user (sec.AppUser.ExternalSubject), or null.</summary>
    Task<AccountSnapshot?> FindByExternalSubjectAsync(string externalSubject, bool includePermissions, CancellationToken cancellationToken);

    Task<AccountSnapshot?> FindByIdAsync(int accountId, bool includePermissions, CancellationToken cancellationToken);

    /// <summary>Records the time of a sign-in. Does not count as a business change: the audit columns stay as they are.</summary>
    Task RecordSignInAsync(int accountId, CancellationToken cancellationToken);
}
