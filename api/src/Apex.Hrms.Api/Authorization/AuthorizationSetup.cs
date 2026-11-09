using Microsoft.AspNetCore.Authorization;

namespace Apex.Hrms.Api.Authorization;

internal static class AuthorizationSetup
{
    /// <summary>
    /// Registers one named policy per module and verb, and the fallback policy hook.
    /// TODO(Identity): the policies are permissive placeholders and enforce nothing. When ASP.NET Core Identity with a
    /// same-site cookie is added, replace <see cref="Placeholder"/> with a requirement that checks the permission of
    /// the caller for the module and verb, and register the ICurrentUser implementation of the Api layer.
    /// </summary>
    public static IServiceCollection AddHrmsAuthorization(this IServiceCollection services) =>
        services.AddAuthorization(options =>
        {
            foreach (var module in PermissionModule.All)
            {
                foreach (var permission in module.Permissions)
                {
                    options.AddPolicy(module.PolicyName(permission), Placeholder);
                }
            }

            ConfigureFallbackPolicy(options);
        });

    /// <summary>
    /// The place to make "deny by default" real: endpoints without an explicit policy fall back to it.
    /// TODO(Identity): set <c>options.FallbackPolicy</c> to a policy that requires an authenticated user (health and
    /// OpenAPI endpoints opt out with AllowAnonymous). It stays unset today because no authentication exists yet.
    /// </summary>
    private static void ConfigureFallbackPolicy(AuthorizationOptions options) =>
        options.FallbackPolicy = null;

    private static void Placeholder(AuthorizationPolicyBuilder policy) => policy.RequireAssertion(_ => true);
}
