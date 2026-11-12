using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Api.Tests;

/// <summary>Fictional accounts and grants for tests that run the real authorization pipeline without a database.</summary>
internal static class AccessFakes
{
    public const int AccountId = 1;
    public const int RoleId = 1;

    /// <summary>Every "module:Action" key of the permission model: what the HR Administrator role holds in the seed.</summary>
    public static IReadOnlyList<string> AllGrants { get; } =
        [.. PermissionModule.All.SelectMany(module => module.Permissions.Select(permission => module.PolicyName(permission)))];

    public static AccountSnapshot Account(
        string roleName = "HR Administrator",
        AccountStatus status = AccountStatus.Active,
        bool roleActive = true,
        bool employeeLeft = false) =>
        new(AccountId, null, "TST- Test User", "tst@example.test", "tst-subject", status, RoleId, roleName, roleActive, employeeLeft, []);
}

internal sealed class FakeAccountDirectory(AccountSnapshot? account) : IAccountDirectory
{
    public int Lookups { get; private set; }

    public Task<AccountSnapshot?> FindByExternalSubjectAsync(string externalSubject, bool includePermissions, CancellationToken cancellationToken) =>
        Task.FromResult(account);

    public Task<AccountSnapshot?> FindByIdAsync(int accountId, bool includePermissions, CancellationToken cancellationToken)
    {
        Lookups++;
        return Task.FromResult(account);
    }

    public Task RecordSignInAsync(int accountId, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>The grants of every role are the same fixed list; a test changes <see cref="Grants"/> to change what the role holds.</summary>
internal sealed class FakeGrantStore(IEnumerable<string> grants) : IPermissionStore
{
    public List<string> Grants { get; } = [.. grants];

    public int Reads { get; private set; }

    public Task<IReadOnlyList<PermissionModuleInfo>> ModulesAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RoleGrant>> GrantsAsync(int? roleId, CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult<IReadOnlyList<RoleGrant>>(
            [.. Grants.Select(key => key.Split(':')).Select(parts => RoleGrant.Create(roleId ?? AccessFakes.RoleId, parts[0], parts[1]))]);
    }

    public Task<Result> ReplaceGrantsAsync(AppRole role, IReadOnlyCollection<(string ModuleKey, string Action)> grants, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
