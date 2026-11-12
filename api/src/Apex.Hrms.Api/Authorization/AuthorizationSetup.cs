using Microsoft.AspNetCore.Authorization;

namespace Apex.Hrms.Api.Authorization;

internal static class AuthorizationSetup
{
    /// <summary>Needs a signed-in user and nothing else (notifications): they are the signed-in user's own.</summary>
    public const string SignedInPolicy = "signed-in";

    /// <summary>The caller must hold the HR Administrator role (a real check, see <see cref="AdministratorRequirement"/>). Added to the settings endpoints on top of their module policy.</summary>
    public const string AdministratorPolicy = "hr-administrator";

    /// <summary>
    /// Registers one named policy per module and action (<c>employees:ViewBank</c>), each a <see cref="PermissionRequirement"/>
    /// checked against sec.RolePermission by <see cref="PermissionHandler"/>, the administrator policy, the signed-in policy
    /// and the fallback policy. The same policy names are used by the endpoints and by code that asks the authorization
    /// service by name (the Excel module checks).
    /// </summary>
    public static IServiceCollection AddHrmsAuthorization(this IServiceCollection services) =>
        services
            .AddScoped<IAuthorizationHandler, AdministratorHandler>()
            .AddScoped<IAuthorizationHandler, PermissionHandler>()
            .AddAuthorization(options =>
            {
                options.AddPolicy(SignedInPolicy, policy => policy.RequireAuthenticatedUser());
                options.AddPolicy(AdministratorPolicy, policy => policy.RequireAuthenticatedUser().AddRequirements(new AdministratorRequirement()));

                foreach (var module in PermissionModule.All)
                {
                    foreach (var permission in module.Permissions)
                    {
                        options.AddPolicy(
                            module.PolicyName(permission),
                            policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(module.Key, permission.ToString())));
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
}
