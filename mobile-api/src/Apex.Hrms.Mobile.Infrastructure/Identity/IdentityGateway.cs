using Apex.Hrms.Mobile.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Apex.Hrms.Mobile.Infrastructure.Identity;

/// <summary>
/// The credential store through ASP.NET Core Identity's <see cref="UserManager{TUser}"/> (registered by the shared HRMS
/// Infrastructure on auth.User): the same password hashing, lockout (5 failures, 15 minutes) and security stamps as the web sign-in.
/// </summary>
internal sealed class IdentityGateway(UserManager<IdentityUser> users) : IIdentityGateway
{
    // A real hash, so an unknown user costs the same time as a wrong password (no timing based enumeration).
    private static readonly Lazy<string> DummyHash = new(
        () => new PasswordHasher<IdentityUser>().HashPassword(new IdentityUser(), Guid.NewGuid().ToString("N")));

    public async Task<string?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken) =>
        (await users.FindByEmailAsync(email))?.Id;

    public async Task<CredentialCheck> CheckPasswordAsync(string identityUserId, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(identityUserId);
        if (user is null)
        {
            SpendPasswordCheck(password);
            return CredentialCheck.WrongPassword;
        }

        // The same sequence as Identity's CheckPasswordSignInAsync with lockoutOnFailure, as the web sign-in uses.
        if (await IsLockedOutAsync(user))
        {
            return CredentialCheck.LockedOut;
        }

        if (await users.CheckPasswordAsync(user, password))
        {
            await users.ResetAccessFailedCountAsync(user);
            return CredentialCheck.Valid;
        }

        await CountFailureAsync(user);
        return CredentialCheck.WrongPassword;
    }

    public void SpendPasswordCheck(string password) =>
        users.PasswordHasher.VerifyHashedPassword(new IdentityUser(), DummyHash.Value, password);

    public async Task<string> GetSecurityStampAsync(string identityUserId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(identityUserId)
            ?? throw new InvalidOperationException("The identity user of a valid sign-in no longer exists.");
        var stamp = await users.GetSecurityStampAsync(user);
        if (!string.IsNullOrEmpty(stamp))
        {
            return stamp;
        }

        await users.UpdateSecurityStampAsync(user);
        return await users.GetSecurityStampAsync(user);
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(string identityUserId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(identityUserId);
        if (user is null)
        {
            return new PasswordChangeResult(PasswordChangeOutcome.UnknownUser);
        }

        if (await IsLockedOutAsync(user))
        {
            return new PasswordChangeResult(PasswordChangeOutcome.LockedOut);
        }

        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            await users.ResetAccessFailedCountAsync(user);
            return new PasswordChangeResult(PasswordChangeOutcome.Changed);
        }

        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            await CountFailureAsync(user);
            return new PasswordChangeResult(PasswordChangeOutcome.WrongCurrentPassword);
        }

        return new PasswordChangeResult(PasswordChangeOutcome.Refused, string.Join(",", result.Errors.Select(error => error.Code)));
    }

    private async Task<bool> IsLockedOutAsync(IdentityUser user) =>
        await users.GetLockoutEnabledAsync(user) && await users.IsLockedOutAsync(user);

    private async Task CountFailureAsync(IdentityUser user)
    {
        if (await users.GetLockoutEnabledAsync(user))
        {
            await users.AccessFailedAsync(user);
        }
    }
}
