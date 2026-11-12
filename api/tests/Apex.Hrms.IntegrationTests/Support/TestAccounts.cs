using System.Security.Cryptography;
using Apex.Hrms.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Apex.Hrms.IntegrationTests.Support;

/// <summary>An account the tests created: both halves (auth.User and sec.AppUser).</summary>
internal sealed record TestAccount(string Email, string Password, string AuthUserId, int AccountId);

/// <summary>
/// Creates sign-in accounts with plain SQL (fictional data, TST- email prefix) using the real Identity password
/// hasher, so the API verifies them like any other. They are removed through the tag in the email (see <see cref="TestRows"/>).
/// </summary>
internal static class TestAccounts
{
    public const string AdministratorRole = "HR Administrator";

    /// <summary>A random password of 24 characters; never reused, never written to a file.</summary>
    public static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(12)) + "a!";

    /// <summary>The email of an account of the test with this tag; it starts with the tag so the cleanup finds it.</summary>
    public static string EmailFor(string tag, string name) => $"{tag}-{name}@example.test";

    public static async Task<TestAccount> CreateAsync(
        string email,
        string? password = null,
        string status = "Active",
        string roleName = AdministratorRole,
        bool lockoutEnabled = true,
        bool linkAccount = true,
        int? employeeId = null)
    {
        password ??= NewPassword();
        var authUserId = Guid.NewGuid().ToString();
        var hash = new PasswordHasher<IdentityUser>().HashPassword(new IdentityUser(), password);

        await Sql.ExecuteAsync(
            """
            INSERT INTO auth.[User] (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash,
                                     SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES (@id, @email, UPPER(@email), @email, UPPER(@email), 1, @hash, @stamp, @concurrency, 0, 0, @lockout, 0);
            """,
            ("@id", authUserId),
            ("@email", email),
            ("@hash", hash),
            ("@stamp", Guid.NewGuid().ToString("N")),
            ("@concurrency", Guid.NewGuid().ToString()),
            ("@lockout", lockoutEnabled));

        if (!linkAccount)
        {
            return new TestAccount(email, password, authUserId, 0);
        }

        var accountId = await Sql.ScalarAsync<int>(
            """
            INSERT INTO sec.AppUser (EmployeeId, DisplayName, Email, RoleId, Status, ExternalSubject)
            OUTPUT INSERTED.Id
            SELECT @employeeId, N'TST- Test Person', @email, r.Id, @status, @subject FROM sec.Role r WHERE r.Name = @role;
            """,
            ("@email", email),
            ("@status", status),
            ("@subject", authUserId),
            ("@role", roleName),
            ("@employeeId", employeeId));
        return new TestAccount(email, password, authUserId, accountId);
    }

    /// <summary>An inactive role named with the tag (removed with the tag); accounts can be given it to test "inactive role".</summary>
    public static async Task CreateInactiveRoleAsync(string name) =>
        await Sql.ExecuteAsync("INSERT INTO sec.Role (Name, IsSystem, Status) VALUES (@name, 0, 'Inactive');", ("@name", name));
}

/// <summary>
/// The administrator the fixture creates once per run: the HTTP clients of the tests sign in as this account, so every
/// audited write has a user to attribute it to. It is removed at the end of the run.
/// </summary>
internal static class TestAdministrator
{
    private static TestAccount? account;

    public static string Tag { get; } = TestRows.NewTag();

    public static TestAccount Account => account ?? throw new InvalidOperationException("The test administrator has not been created yet.");

    public static int AccountId => Account.AccountId;

    public static async Task CreateAsync() =>
        account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "admin"));
}

/// <summary>A fake signed-in caller (a TST- user) for tests that call the handlers directly, without a request.</summary>
internal sealed class TestCurrentUser(int? userId, string displayName, bool isSystem = false, int? employeeId = null) : ICurrentUser
{
    public int? UserId { get; } = userId;

    public int? EmployeeId { get; } = employeeId;

    public bool IsAuthenticated => UserId is not null;

    public bool IsSystem { get; } = isSystem;

    public string DisplayName { get; } = displayName;
}

/// <summary>A caller who holds every permission (an approver): row-level scoping lets such a caller see and act for everyone.</summary>
internal sealed class AllGrantsCallerPermissions : Apex.Hrms.Application.Security.ICallerPermissions
{
    public bool Has(string moduleKey, string action) => true;
}
