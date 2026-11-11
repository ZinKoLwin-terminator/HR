using System.Reflection;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;
using Apex.Hrms.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>The Identity side in memory. It keeps no password: only what a test needs to see (who was created, locked, mailed).</summary>
internal sealed class FakeAccountLogins : IAccountLogins
{
    private int lastId;

    public List<string> CreatedEmails { get; } = [];

    public List<string> Deleted { get; } = [];

    public List<string> Unlocked { get; } = [];

    public List<string> ResetMailsFor { get; } = [];

    public HashSet<string> LockedOut { get; } = [];

    /// <summary>The length of the password of the last create: all a test may know about it.</summary>
    public int LastPasswordLength { get; private set; }

    public Exception? SendFailure { get; set; }

    public Task<Result<string>> CreateAsync(string email, string password, CancellationToken cancellationToken)
    {
        LastPasswordLength = password.Length;
        if (SecurityPolicy.PasswordViolation(password) is { } problem)
        {
            return Task.FromResult(Result.Failure<string>(AppError.Validation("The account could not be created.", "initialPassword", problem)));
        }

        CreatedEmails.Add(email);
        return Task.FromResult(Result.Success($"sub-{++lastId}"));
    }

    public Task DeleteAsync(string subject, CancellationToken cancellationToken)
    {
        Deleted.Add(subject);
        return Task.CompletedTask;
    }

    public Task UnlockAsync(string subject, CancellationToken cancellationToken)
    {
        Unlocked.Add(subject);
        LockedOut.Remove(subject);
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> LockedOutAsync(IReadOnlyCollection<string> subjects, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(subjects.Where(LockedOut.Contains).ToHashSet());

    public Task SendPasswordResetAsync(string subject, CancellationToken cancellationToken)
    {
        if (SendFailure is not null)
        {
            throw SendFailure;
        }

        ResetMailsFor.Add(subject);
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public bool Configured { get; set; } = true;

    public void EnsureAvailable()
    {
        if (!Configured)
        {
            throw new EmailNotConfiguredException();
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>The modules of the post-deployment script; grants are kept in a list. Saving goes through the fake unit of work, like the real store.</summary>
internal sealed class FakePermissionStore(IUnitOfWork unitOfWork) : IPermissionStore
{
    public static IReadOnlyList<PermissionModuleInfo> Modules { get; } =
    [
        new("dashboard", "Dashboard", ["View"]),
        new("employees", "Employees", ["View", "Create", "Edit", "Delete", "ViewSensitive", "EditSensitive", "ViewBank", "EditBank"]),
        new("leave", "Leave", ["View", "Create", "Edit", "Delete", "Approve"]),
        new("settings", "Settings", ["View", "Create", "Edit", "Delete"]),
    ];

    public List<RoleGrant> Grants { get; } = [];

    public Task<IReadOnlyList<PermissionModuleInfo>> ModulesAsync(CancellationToken cancellationToken) => Task.FromResult(Modules);

    public Task<IReadOnlyList<RoleGrant>> GrantsAsync(int? roleId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoleGrant>>([.. Grants.Where(grant => roleId is null || grant.RoleId == roleId)]);

    public async Task<Result> ReplaceGrantsAsync(AppRole role, IReadOnlyCollection<(string ModuleKey, string Action)> grants, CancellationToken cancellationToken)
    {
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved;
        }

        Grants.RemoveAll(grant => grant.RoleId == role.Id);
        Grants.AddRange(grants.Select(grant => RoleGrant.Create(role.Id, grant.ModuleKey, grant.Action)));
        return Result.Success();
    }
}

/// <summary>The settings use cases on the in-memory host, with seeding helpers.</summary>
internal sealed class SettingsHost : IDisposable
{
    public const string AdministratorRole = SecurityPolicy.AdministratorRoleName;

    private static readonly PropertyInfo IsSystemProperty = typeof(AppRole).GetProperty(nameof(AppRole.IsSystem))!;
    private static readonly PropertyInfo StatusProperty = typeof(AppAccount).GetProperty(nameof(AppAccount.Status))!;

    private OrgSeed? org;
    private int employeeNumber;

    public SettingsHost()
    {
        Host = new TestHost(services =>
        {
            services.AddSingleton<IAccountLogins>(Logins);
            services.AddSingleton<IEmailSender>(Email);
            services.AddSingleton<IPermissionStore>(provider => Permissions = new FakePermissionStore(provider.GetRequiredService<IUnitOfWork>()));
        });
        Permissions = (FakePermissionStore)Host.Get<IPermissionStore>();

        // The caller is an administrator whose account is not among the seeded ones, so "my own account" rules only apply when a test says so.
        Host.User.UserId = 999;
    }

    public TestHost Host { get; }

    public FakeAccountLogins Logins { get; } = new();

    public FakeEmailSender Email { get; } = new();

    public FakePermissionStore Permissions { get; private set; }

    public void Dispose() => Host.Dispose();

    public T Handler<T>()
        where T : notnull =>
        Host.Get<T>();

    public AppRole SeedRole(string name, bool builtIn = false, bool active = true)
    {
        var role = ResultAssert.Succeeded(AppRole.Create(new RoleDetails(name, "Fictional role"), RecordStatus.Active));
        if (!active)
        {
            Assert.True(role.Deactivate().IsSuccess);
        }

        if (builtIn)
        {
            IsSystemProperty.SetValue(role, true);
        }

        Host.Repository<AppRole>().Add(role);
        return role;
    }

    /// <summary>An account that was already created (an employee-less one when <paramref name="employeeId"/> is null), as an administrator would have left it.</summary>
    public AppAccount SeedAccount(AppRole role, int? employeeId, string name, string email, AccountStatus status = AccountStatus.Active, bool linked = true)
    {
        var account = AppAccount.Create(employeeId, name, email, role.Id, linked ? $"seed-{email}" : null!, AccountStatus.Active);
        if (status != AccountStatus.Active)
        {
            StatusProperty.SetValue(account, status);
        }

        Host.Repository<AppAccount>().Add(account);
        return account;
    }

    /// <summary>A current employee with a work email of its own; each call makes a new one.</summary>
    public async Task<EmployeeDto> SeedEmployeeAsync(string? email = null)
    {
        org ??= await Host.SeedOrgAsync();
        var number = ++employeeNumber;
        var created = await Host.Get<ICreateHandler<EmployeeCreateRequest, EmployeeDto>>()
            .HandleAsync(EmployeeFixtures.NewEmployee(org, null, email ?? $"person{number}@example.test"), default);
        return ResultAssert.Succeeded(created);
    }

    public async Task ExitAsync(EmployeeDto employee) =>
        ResultAssert.Succeeded(await Host.Get<ICommandHandler<ExitEmployeeCommand, EmployeeDto>>()
            .HandleAsync(new ExitEmployeeCommand(employee.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 11, 1), "Resigned"), default));

    public Task<Result<UserDto>> CreateUserAsync(int employeeId, int roleId, string? password = "a-long-enough-password", string? status = null) =>
        Handler<ICreateHandler<UserCreateRequest, UserDto>>().HandleAsync(new UserCreateRequest(employeeId, roleId, status, password), default);

    public static string Version(byte[] rowVersion) => Requests.Version(rowVersion);

    public GeneralSettings SeedGeneralSettings()
    {
        var settings = (GeneralSettings)Activator.CreateInstance(typeof(GeneralSettings), nonPublic: true)!;
        Assert.True(settings.Update(ValidSettings()).IsSuccess);
        Host.Repository<GeneralSettings>().Add(settings);
        return settings;
    }

    public static GeneralSettingsDetails ValidSettings() =>
        new("APEX Group", "hr@example.test", "Asia/Yangon", "yyyy-MM-dd", "MMK", 1, EmailNotifications: true, WeeklyDigest: false);
}
