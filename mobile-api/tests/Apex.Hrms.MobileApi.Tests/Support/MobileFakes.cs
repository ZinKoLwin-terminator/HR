using System.Collections.Concurrent;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Mobile.Application.Abstractions;
using Apex.Hrms.Mobile.Domain.Sessions;
using Microsoft.AspNetCore.Identity;

namespace Apex.Hrms.MobileApi.Tests.Support;

/// <summary>A clock the tests move forward. Starts at the real time, because the bearer handler checks token lifetimes against it.</summary>
internal sealed class MutableTimeProvider : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}

/// <summary>An Identity user store in memory: passwords, emails, security stamps and lockout. No database.</summary>
internal sealed class InMemoryUserStore :
    IUserPasswordStore<IdentityUser>,
    IUserEmailStore<IdentityUser>,
    IUserSecurityStampStore<IdentityUser>,
    IUserLockoutStore<IdentityUser>
{
    private readonly ConcurrentDictionary<string, IdentityUser> users = new(StringComparer.Ordinal);

    public void Add(IdentityUser user) => users[user.Id] = user;

    public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        users[user.Id] = user;
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        users[user.Id] = user;
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        users.TryRemove(user.Id, out _);
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(users.GetValueOrDefault(userId));

    public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        Task.FromResult(users.Values.FirstOrDefault(user => user.NormalizedUserName == normalizedUserName));

    public Task<IdentityUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(users.Values.FirstOrDefault(user => user.NormalizedEmail == normalizedEmail));

    public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);

    public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);

    public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetPasswordHashAsync(IdentityUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash is not null);

    public Task SetEmailAsync(IdentityUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(IdentityUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedEmailAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(IdentityUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task SetSecurityStampAsync(IdentityUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(IdentityUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(IdentityUser user, CancellationToken cancellationToken) =>
        Task.FromResult(++user.AccessFailedCount);

    public Task ResetAccessFailedCountAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(IdentityUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}

/// <summary>The business accounts, keyed by id.</summary>
internal sealed class FakeAccountDirectory : IAccountDirectory
{
    private readonly ConcurrentDictionary<int, AccountSnapshot> accounts = new();

    public int SignIns { get; private set; }

    public void Set(AccountSnapshot account) => accounts[account.Id] = account;

    public Task<AccountSnapshot?> FindByExternalSubjectAsync(string externalSubject, bool includePermissions, CancellationToken cancellationToken) =>
        Task.FromResult(accounts.Values.FirstOrDefault(account => account.ExternalSubject == externalSubject));

    public Task<AccountSnapshot?> FindByIdAsync(int accountId, bool includePermissions, CancellationToken cancellationToken) =>
        Task.FromResult(accounts.GetValueOrDefault(accountId));

    public Task RecordSignInAsync(int accountId, CancellationToken cancellationToken)
    {
        SignIns++;
        return Task.CompletedTask;
    }
}

/// <summary>Employee codes and profiles in memory.</summary>
internal sealed class FakeEmployeeDirectory : IMobileEmployeeDirectory
{
    private readonly ConcurrentDictionary<string, string> subjectsByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, MobileEmployeeProfile> profiles = new();

    public void Add(int employeeId, string identityUserId, MobileEmployeeProfile profile)
    {
        subjectsByCode[profile.EmployeeCode] = identityUserId;
        profiles[employeeId] = profile;
    }

    public Task<string?> FindIdentityUserIdByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken) =>
        Task.FromResult(subjectsByCode.GetValueOrDefault(employeeCode));

    public Task<MobileEmployeeProfile?> FindProfileAsync(int employeeId, CancellationToken cancellationToken) =>
        Task.FromResult(profiles.GetValueOrDefault(employeeId));
}

/// <summary>The sessions in memory, with the same compare-and-swap rotation as the SQL store.</summary>
internal sealed class InMemoryMobileSessionStore(InMemoryUserStore users) : IMobileSessionStore
{
    private readonly ConcurrentDictionary<(string User, string Device), MobileSession> sessions = new();
    private readonly Lock gate = new();

    public int Count(string identityUserId) => sessions.Keys.Count(key => key.User == identityUserId);

    public async Task<MobileSessionState?> FindAsync(string identityUserId, string deviceId, CancellationToken cancellationToken)
    {
        if (!sessions.TryGetValue((identityUserId, deviceId), out var session))
        {
            return null;
        }

        var user = await users.FindByIdAsync(identityUserId, cancellationToken);
        return new MobileSessionState(session, user?.SecurityStamp);
    }

    public Task SaveAsync(MobileSession session, CancellationToken cancellationToken)
    {
        sessions[(session.IdentityUserId, session.DeviceId)] = session;
        return Task.CompletedTask;
    }

    public Task<bool> ReplaceAsync(MobileSession current, MobileSession replacement, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            var key = (current.IdentityUserId, current.DeviceId);
            if (!sessions.TryGetValue(key, out var stored) || stored.RefreshTokenHash != current.RefreshTokenHash)
            {
                return Task.FromResult(false);
            }

            sessions[key] = replacement;
            return Task.FromResult(true);
        }
    }

    public Task RemoveAsync(string identityUserId, string deviceId, CancellationToken cancellationToken)
    {
        sessions.TryRemove((identityUserId, deviceId), out _);
        return Task.CompletedTask;
    }

    public Task RemoveAllAsync(string identityUserId, string? keepDeviceId, CancellationToken cancellationToken)
    {
        foreach (var key in sessions.Keys.Where(key => key.User == identityUserId && key.Device != keepDeviceId))
        {
            sessions.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}

/// <summary>The fictional people of the tests. Not real employees.</summary>
internal static class TestPeople
{
    public const string Password = "correct horse battery";
    public const string EmployeeCode = "TST-00421";
    public const string Email = "aye.chan@example.test";
    public const int AccountId = 7;
    public const int EmployeeId = 42;

    public const string AdminEmail = "admin@example.test";
    public const int AdminAccountId = 1;

    public static AccountSnapshot Account(string identityUserId, AccountStatus status = AccountStatus.Active) => new(
        AccountId,
        EmployeeId,
        "TST- Aye Chan Moe",
        Email,
        identityUserId,
        status,
        3,
        "Employee",
        RoleActive: true,
        EmployeeLeft: false,
        ["attendance:View", "leave:Create"]);

    public static MobileEmployeeProfile Profile { get; } = new(EmployeeCode, "TST- Aye Chan Moe", "Finance", "Senior Accountant", null);

    public static IdentityUser NewUser(string email)
    {
        var user = new IdentityUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            LockoutEnabled = true,
        };
        user.PasswordHash = new PasswordHasher<IdentityUser>().HashPassword(user, Password);
        return user;
    }
}
