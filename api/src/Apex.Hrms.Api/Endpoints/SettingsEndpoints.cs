using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Settings;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// <c>/api/users</c>, <c>/api/roles</c>, <c>/api/role-permissions</c> and <c>/api/general-settings</c>. Each endpoint needs the
/// policy of its verb for the settings module and, on top, the HR Administrator role (a real check): only an administrator
/// creates or changes accounts, roles and permissions. The one exception is reading the general settings, which every signed-in
/// user with settings:View may do. There is no public sign-up.
/// </summary>
internal static class SettingsEndpoints
{
    private const string Tag = "Settings";

    private static readonly PermissionModule Module = PermissionModule.Settings;

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        MapUsers(app);
        MapRoles(app);
        MapRolePermissions(app);
        MapGeneralSettings(app);
        return app;
    }

    private static void MapUsers(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags(Tag).RequireAuthorization(AuthorizationSetup.AdministratorPolicy);
        var view = Module.PolicyName(Permission.View);

        // There is deliberately no "get all": the list carries email addresses, so it is only read in pages.
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] UserFilter filter,
                IQueryHandler<UserDto, UserFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<UserDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapPost("/", async (UserCreateRequest request, ICreateHandler<UserCreateRequest, UserDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(request, cancellationToken)).ToCreated(dto => $"/api/users/{dto.Id}"))
            .RequireAuthorization(Module.PolicyName(Permission.Create));

        group.MapPut(
            "/{id:int}",
            async (int id, UserUpdateRequest request, IUpdateHandler<UserUpdateRequest, UserDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.Edit));

        group.MapDelete(
            "/{id:int}",
            async (int id, [FromBody] VersionedBody? body, IDeleteHandler<UserDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, body?.RowVersion, cancellationToken)).ToNoContent())
            .RequireAuthorization(Module.PolicyName(Permission.Delete));

        group.MapStatusCommands<UserDto>(Module)
            .MapCommand<UserDto, UnlockUserCommand>("unlock", Module);

        group.MapPost(
            "/{id:int}/password-reset",
            async (int id, IUserPasswordResetHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.Edit));
    }

    private static void MapRoles(IEndpointRouteBuilder app)
    {
        var group = app.MapCrud<RoleDto, RoleCreateRequest, RoleUpdateRequest, RoleFilter>("/api/roles", Tag, Module)
            .MapStatusCommands<RoleDto>(Module);
        group.RequireAuthorization(AuthorizationSetup.AdministratorPolicy);
    }

    private static void MapRolePermissions(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/role-permissions").WithTags(Tag).RequireAuthorization(AuthorizationSetup.AdministratorPolicy);

        group.MapGet("/", async (IPermissionMatrixHandler handler, CancellationToken cancellationToken) =>
            (await handler.GetMatrixAsync(cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.View));

        group.MapGet("/{roleId:int}", async (int roleId, IPermissionMatrixHandler handler, CancellationToken cancellationToken) =>
            (await handler.GetForRoleAsync(roleId, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.View));

        group.MapPut(
            "/{roleId:int}",
            async (int roleId, RolePermissionsUpdateRequest request, IPermissionMatrixHandler handler, CancellationToken cancellationToken) =>
                (await handler.SaveAsync(roleId, request, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.Edit));
    }

    private static void MapGeneralSettings(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/general-settings").WithTags(Tag);

        group.MapGet("/", async (IGeneralSettingsHandler handler, CancellationToken cancellationToken) =>
            (await handler.GetAsync(cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.View));

        group.MapPut("/", async (GeneralSettingsUpdateRequest request, IGeneralSettingsHandler handler, CancellationToken cancellationToken) =>
            (await handler.SaveAsync(request, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.Edit))
            .RequireAuthorization(AuthorizationSetup.AdministratorPolicy);
    }
}
