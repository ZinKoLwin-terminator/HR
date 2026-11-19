using Microsoft.AspNetCore.Authorization;

namespace Apex.Hrms.Api.Authorization;

internal static class AuthorizationSetup
{
    /// <summary>Needs a signed-in user and nothing else (notifications): they are the signed-in user's own.</summary>
    public const string SignedInPolicy = "signed-in";

    /// <summary>The caller must hold the HR Administrator role (a real check, see <see cref="AdministratorRequirement"/>). Added to the settings endpoints on top of their module policy.</summary>
    public const string AdministratorPolicy = "hr-administrator";

    /// <summary>The minimal employee picker: <c>recruitment:Edit</c> or <c>employees:View</c> (see <see cref="AnyPermissionRequirement"/>).</summary>
    public const string EmployeeLookupPolicy = "employee-lookup";

    /// <summary>
    /// A grievance case-team read shared by the three tiers (case detail, history, a file): <c>grievance-management:View</c>,
    /// <c>grievance-investigation:View</c> or <c>grievance-review:View</c>. The application layer applies the scope of the tiers the caller holds.
    /// </summary>
    public const string GrievanceCaseTeamPolicy = "grievance-case-team";

    /// <summary>
    /// Grievance case work shared by management and the assigned investigators (start investigation, request information, escalate,
    /// propose a resolution, messages, notes, files): <c>grievance-management:Edit</c> or <c>grievance-investigation:Edit</c>. The
    /// application layer decides whether the caller is management or an investigator assigned to that case.
    /// </summary>
    public const string GrievanceCaseWorkPolicy = "grievance-case-work";

    /// <summary>The grievance summary report: <c>grievance-management:View</c> or <c>grievance-review:View</c>.</summary>
    public const string GrievanceInsightPolicy = "grievance-insight";

    /// <summary>
    /// The grievance form options (active categories and departments): <c>grievance:View</c> for the submitter's own form, or
    /// <c>grievance-management:Create</c> for HR recording a grievance for an employee.
    /// </summary>
    public const string GrievanceOptionsPolicy = "grievance-options";

    /// <summary>
    /// The case-team employee picker: <c>grievance-management:Edit</c> (the subject at triage) or <c>grievance-management:Create</c>
    /// (the employee HR records a grievance for).
    /// </summary>
    public const string GrievanceEmployeePickerPolicy = "grievance-employee-picker";

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
                options.AddPolicy(
                    EmployeeLookupPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new AnyPermissionRequirement("recruitment", "Edit", "employees:View")));
                options.AddPolicy(
                    GrievanceCaseTeamPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(
                        new AnyPermissionRequirement("grievance-management", "View", "grievance-investigation:View", "grievance-review:View")));
                options.AddPolicy(
                    GrievanceCaseWorkPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new AnyPermissionRequirement("grievance-management", "Edit", "grievance-investigation:Edit")));
                options.AddPolicy(
                    GrievanceInsightPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new AnyPermissionRequirement("grievance-management", "View", "grievance-review:View")));
                options.AddPolicy(
                    GrievanceOptionsPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new AnyPermissionRequirement("grievance", "View", "grievance-management:Create")));
                options.AddPolicy(
                    GrievanceEmployeePickerPolicy,
                    policy => policy.RequireAuthenticatedUser().AddRequirements(new AnyPermissionRequirement("grievance-management", "Edit", "grievance-management:Create")));

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
