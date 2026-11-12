using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Apex.Hrms.Infrastructure.Security;

/// <summary>
/// The Identity side of the accounts an administrator manages. A password goes straight into the Identity password hasher
/// (<c>UserManager.CreateAsync(user, password)</c>): it is checked against the one password policy, hashed with a salt, and
/// never kept, returned or logged. Reset tokens exist only inside the mail that is sent.
/// </summary>
internal sealed class EfAccountLogins(
    UserManager<IdentityUser> users,
    HrmsDbContext context,
    TimeProvider time,
    IEmailSender email,
    IConfiguration? configuration = null) : IAccountLogins
{
    public const string PasswordResetPageUrlKey = "Auth:PasswordResetUrl";

    public async Task<Result<string>> CreateAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = new IdentityUser { UserName = email, Email = email };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            // Identity error descriptions never contain the password; they are safe to show.
            return Result.Failure<string>(AppError.Validation(
                "The account could not be created.",
                created.Errors.GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "initialPassword" : "employeeId")
                    .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));
        }

        return Result.Success(user.Id);
    }

    public async Task DeleteAsync(string subject, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(subject) is { } user)
        {
            ThrowIfFailed(await users.DeleteAsync(user));
        }
    }

    public async Task InvalidateSessionsAsync(string subject, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(subject) is { } user)
        {
            ThrowIfFailed(await users.UpdateSecurityStampAsync(user));
        }
    }

    /// <summary>A refusal inside the caller's transaction: throwing rolls it back. Identity error codes carry no personal data.</summary>
    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"The identity store refused the change: {string.Join(',', result.Errors.Select(error => error.Code))}.");
        }
    }

    public async Task UnlockAsync(string subject, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(subject) is { } user)
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
        }
    }

    public async Task<IReadOnlySet<string>> LockedOutAsync(IReadOnlyCollection<string> subjects, CancellationToken cancellationToken)
    {
        if (subjects.Count == 0)
        {
            return new HashSet<string>();
        }

        var now = time.GetUtcNow();
        var locked = await context.Set<IdentityUser>().AsNoTracking()
            .Where(user => subjects.Contains(user.Id) && user.LockoutEnabled && user.LockoutEnd != null && user.LockoutEnd > now)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        return locked.ToHashSet(StringComparer.Ordinal);
    }

    public async Task SendPasswordResetAsync(string subject, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(subject);
        if (user?.Email is not { Length: > 0 } address)
        {
            throw new InvalidOperationException("The account has no email address to send a password reset to.");
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await email.SendAsync(PasswordResetEmail.Build(address, token, configuration?[PasswordResetPageUrlKey]), cancellationToken);
    }
}
