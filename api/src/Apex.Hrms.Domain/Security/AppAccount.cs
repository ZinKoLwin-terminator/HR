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

    public const string LockedMessage = "This account is locked. Unlock it first.";

    public const string AlreadyLinkedMessage = "This account is already linked to an employee.";

    private AppAccount()
    {
    }

    /// <summary>
    /// The linked employee; null for an account that is not linked (the built-in administrator, or an account created before
    /// it was linked). An unlinked account can be linked once (<see cref="LinkEmployee"/>); after that the employee never changes.
    /// </summary>
    public int? EmployeeId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public int RoleId { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTime? LastLoginAtUtc { get; private set; }

    /// <summary>The identity user id (auth.User.Id) this account signs in with.</summary>
    public string? ExternalSubject { get; private set; }

    /// <summary>Creates an account linked to an identity user. A new account starts Active or Inactive, never Locked.</summary>
    public static AppAccount Create(
        int? employeeId,
        string displayName,
        string email,
        int roleId,
        string externalSubject,
        AccountStatus status = AccountStatus.Active) =>
        new()
        {
            EmployeeId = employeeId,
            DisplayName = TextNormalization.Clean(displayName),
            Email = TextNormalization.Clean(email),
            RoleId = roleId,
            Status = status == AccountStatus.Locked ? AccountStatus.Active : status,
            ExternalSubject = externalSubject,
        };

    public void ChangeRole(int roleId) => RoleId = roleId;

    /// <summary>Makes the account Active. A locked account must be unlocked first (that also clears the sign-in lockout).</summary>
    public Result Activate()
    {
        if (Status == AccountStatus.Locked)
        {
            return Result.Failure(AppError.Conflict(LockedMessage));
        }

        Status = AccountStatus.Active;
        return Result.Success();
    }

    public void Deactivate() => Status = AccountStatus.Inactive;

    /// <summary>
    /// Links an account that has no employee to its employee and takes the employee's name as the display name, as
    /// <see cref="Create"/> does. Only an unlinked account can be linked: the employee of a linked account never changes.
    /// </summary>
    public Result LinkEmployee(int employeeId, string displayName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(employeeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (EmployeeId is not null)
        {
            return Result.Failure(AppError.Conflict(AlreadyLinkedMessage));
        }

        EmployeeId = employeeId;
        DisplayName = TextNormalization.Clean(displayName);
        return Result.Success();
    }

    /// <summary>A locked account becomes Active again; any other status is left as it is.</summary>
    public void Unlock()
    {
        if (Status == AccountStatus.Locked)
        {
            Status = AccountStatus.Active;
        }
    }
}
