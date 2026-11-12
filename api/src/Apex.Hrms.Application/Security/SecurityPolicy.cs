namespace Apex.Hrms.Application.Security;

/// <summary>Sign-in rules shared by the identity configuration and the checks that run before it.</summary>
public static class SecurityPolicy
{
    /// <summary>The only password rule: length. No composition rules.</summary>
    public const int PasswordMinimumLength = 12;

    public const int MaxFailedSignIns = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    /// <summary>The built-in role the bootstrap command assigns (sec.Role name, created by the post-deployment script).</summary>
    public const string AdministratorRoleName = "HR Administrator";

    /// <summary>
    /// The application lock (<c>sp_getapplock</c>) that every change which could remove an HR Administrator holds for the
    /// length of its transaction, so two concurrent changes cannot both pass the "last administrator" check.
    /// </summary>
    public const string AdministratorLockName = "hrms:administrator-changes";

    /// <summary>A message when <paramref name="password"/> breaks the password rule, otherwise null.</summary>
    public static string? PasswordViolation(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < PasswordMinimumLength
            ? $"Use at least {PasswordMinimumLength} characters."
            : null;
}
