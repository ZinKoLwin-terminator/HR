using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Security;

/// <summary>
/// The "module:Action" grants of the signed-in caller's role for the current request. The host fills it when it checks a
/// permission (the Api permission handler reads the grants of the role live and through <see cref="RoleGrantCache"/>), so the
/// access scopes can tell an approver (sees everything of a module) from an ordinary employee (sees only their own rows)
/// inside a database predicate. Nothing is filled until a permission was checked: then nobody holds anything, which is the
/// safe side (own rows only).
/// </summary>
public interface ICallerPermissions
{
    bool Has(string moduleKey, string action);
}

/// <summary>The per-request holder behind <see cref="ICallerPermissions"/>.</summary>
public sealed class CallerPermissions : ICallerPermissions
{
    private IReadOnlySet<string> held = new HashSet<string>();

    /// <summary>Replaces the grants the caller holds (set by the host's permission check).</summary>
    public void Set(IReadOnlySet<string> keys) => held = keys;

    public bool Has(string moduleKey, string action) => held.Contains($"{moduleKey}:{action}");
}

internal static class SecurityScopingExtensions
{
    public static IServiceCollection AddSecurityScoping(this IServiceCollection services)
    {
        services.TryAddScoped<CallerPermissions>();
        services.TryAddScoped<ICallerPermissions>(provider => provider.GetRequiredService<CallerPermissions>());
        services.TryAddScoped<SelfService>();
        return services;
    }
}
