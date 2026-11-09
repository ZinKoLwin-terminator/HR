using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Security;

/// <summary>
/// The business account of a person who can sign in (sec.AppUser). It holds no credentials: the password hash lives in
/// the identity store (auth.User) and is linked through <see cref="ExternalSubject"/>.
/// </summary>
public sealed class AppAccount : AuditedEntity
{
    public const int EmailMaxLength = 120;
    public const int DisplayNameMaxLength = 130;

    private AppAccount()
    {
    }

    /// <summary>The linked employee; null only for the built-in administrator.</summary>
    public int? EmployeeId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public int RoleId { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    /// <summary>The identity user id (auth.User.Id) this account signs in with.</summary>
    public string? ExternalSubject { get; private set; }

    /// <summary>Creates an active account linked to an identity user.</summary>
    public static AppAccount Create(int? employeeId, string displayName, string email, int roleId, string externalSubject) =>
        new()
        {
            EmployeeId = employeeId,
            DisplayName = TextNormalization.Clean(displayName),
            Email = TextNormalization.Clean(email),
            RoleId = roleId,
            Status = AccountStatus.Active,
            ExternalSubject = externalSubject,
        };
}
