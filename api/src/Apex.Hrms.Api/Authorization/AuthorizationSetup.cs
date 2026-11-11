using Microsoft.AspNetCore.Authorization;

namespace Apex.Hrms.Api.Authorization;

internal static class AuthorizationSetup
{
    /// <summary>Needs a signed-in user and nothing else (notifications). Unlike the module policies it is final, not a placeholder.</summary>
    public const string SignedInPolicy = "signed-in";

    /// <summary>The caller must hold the HR Administrator role (a real check, see <see cref="AdministratorRequirement"/>). Added to the settings endpoints on top of their module policy.</summary>
    public const string AdministratorPolicy = "hr-administrator";

    /// <summary>
    /// Registers one named policy per module and verb, and the fallback policy.
    /// TODO(Permissions, phase 3): the module policies still check no permission. They only require a signed-in user;
    /// replace <see cref="Placeholder"/> with a requirement that checks the permission of the caller for the module and
    /// verb (the permissions are already returned by <c>GET /api/auth/me</c>).
    /// </summary>
    public static IServiceCollection AddHrmsAuthorization(this IServiceCollection services) =>
        services.AddScoped<IAuthorizationHandler, AdministratorHandler>().AddAuthorization(options =>
        {
            // Notifications are the signed-in user's own: no permission, only a signed-in user (a named policy, never a placeholder).
            options.AddPolicy(SignedInPolicy, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(AdministratorPolicy, policy => policy.RequireAuthenticatedUser().AddRequirements(new AdministratorRequirement()));

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
    /// Deny by default: an endpoint without its own authorization metadata requires a signed-in user. The auth endpoints
    /// that must work anonymously (antiforgery, login, forgot-password, reset-password) and the OpenAPI document opt out
    /// with AllowAnonymous.
    /// </summary>
    private static void ConfigureFallbackPolicy(AuthorizationOptions options) =>
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    /// <summary>
    /// A named policy replaces the fallback for its endpoint, so each placeholder must require sign-in itself or the
    /// endpoint would be open. The permission check comes in phase 3.
    /// </summary>
    private static void Placeholder(AuthorizationPolicyBuilder policy) =>
        policy.RequireAuthenticatedUser().RequireAssertion(_ => true);
}
