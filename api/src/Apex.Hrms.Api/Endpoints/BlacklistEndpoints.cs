using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Employees.Blacklist;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// The do-not-rehire list (reads need employees:ViewBlacklist, changes employees:EditBlacklist; only HR Administrator holds
/// them) and the minimal employee picker. Entries carry the reason, so there is no unpaged list.
/// </summary>
internal static class BlacklistEndpoints
{
    private const string Route = "/api/employee-blacklist";

    private static readonly PermissionModule Module = PermissionModule.Employees;

    public static IEndpointRouteBuilder MapBlacklistEndpoints(this IEndpointRouteBuilder app)
    {
        var view = Module.PolicyName(Permission.ViewBlacklist);
        var edit = Module.PolicyName(Permission.EditBlacklist);
        var group = app.MapGroup(Route).WithTags("Employees");

        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] BlacklistFilter filter,
                IBlacklistHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.QueryAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/{id:int}", async (int id, IBlacklistHandler handler, CancellationToken cancellationToken) =>
            (await handler.GetAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapPost(
            "/",
            async ([FromBody] BlacklistAddRequest? request, IBlacklistHandler handler, CancellationToken cancellationToken) =>
                (await handler.AddAsync(request ?? new BlacklistAddRequest(0, null), cancellationToken)).ToCreated(dto => $"{Route}/{dto.Id}"))
            .RequireAuthorization(edit);

        group.MapPost(
            "/{id:int}/lift",
            async (int id, [FromBody] BlacklistLiftRequest? request, IBlacklistHandler handler, CancellationToken cancellationToken) =>
                (await handler.LiftAsync(id, request ?? new BlacklistLiftRequest(null, null), cancellationToken)).ToOk())
            .RequireAuthorization(edit);

        app.MapGet(
            "/api/employees/{id:int}/blacklist",
            async (int id, IBlacklistHandler handler, CancellationToken cancellationToken) =>
                (await handler.GetActiveOfEmployeeAsync(id, cancellationToken)).ToOk())
            .WithTags("Employees")
            .RequireAuthorization(view);

        // Only id, code, display name, job title and department: no personal data. Recruitment:Edit (job opening form) or employees:View.
        app.MapGet(
            "/api/employees/lookup",
            async (string? search, int? pageIndex, int? pageSize, IEmployeeLookupHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(search, pageIndex ?? 0, pageSize ?? 20, cancellationToken)).ToOk())
            .WithTags("Employees")
            .RequireAuthorization(AuthorizationSetup.EmployeeLookupPolicy);

        return app;
    }
}
