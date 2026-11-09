using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

internal static class CrudEndpoints
{
    /// <summary>
    /// Maps the CrudDataService contract methods of the frontend to REST routes under <paramref name="route"/>. Only
    /// the operations in <paramref name="operations"/> are mapped; a workflow resource (such as a payroll run) leaves
    /// out Update and Delete and passes any type for the request it does not use. Every endpoint requires the policy
    /// of its verb for <paramref name="module"/>. Endpoints only translate HTTP; every rule lives in the use-case handlers.
    /// </summary>
    public static RouteGroupBuilder MapCrud<TDto, TCreate, TUpdate, TFilter>(
        this IEndpointRouteBuilder routes,
        string route,
        string tag,
        PermissionModule module,
        CrudOperations operations = CrudOperations.All)
        where TDto : IIdentified
    {
        var group = routes.MapGroup(route).WithTags(tag);

        if (operations.HasFlag(CrudOperations.Read))
        {
            MapReads<TDto, TFilter>(group, module);
        }

        if (operations.HasFlag(CrudOperations.Create))
        {
            group.MapPost("/", async (TCreate request, ICreateHandler<TCreate, TDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToCreated(dto => $"{route}/{dto.Id}"))
                .RequireAuthorization(module.PolicyName(Permission.Create));
        }

        if (operations.HasFlag(CrudOperations.Update))
        {
            group.MapPut(
                "/{id:int}",
                async (int id, TUpdate request, IUpdateHandler<TUpdate, TDto> handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
                .RequireAuthorization(module.PolicyName(Permission.Edit));
        }

        if (operations.HasFlag(CrudOperations.Delete))
        {
            // The row version travels in a JSON body, like for every other command, not in the URL: base64 contains
            // characters that need escaping in a query string, and URLs end up in logs. ASP.NET Core binds a body on
            // DELETE when it is marked [FromBody]. It is nullable so that a missing body becomes a Validation error
            // on rowVersion instead of a generic bad request.
            group.MapDelete(
                "/{id:int}",
                async (int id, [FromBody] VersionedBody? body, IDeleteHandler<TDto> handler, CancellationToken cancellationToken) =>
                    (await handler.HandleAsync(id, body?.RowVersion, cancellationToken)).ToNoContent())
                .RequireAuthorization(module.PolicyName(Permission.Delete));
        }

        return group;
    }

    private static void MapReads<TDto, TFilter>(RouteGroupBuilder group, PermissionModule module)
    {
        var view = module.PolicyName(Permission.View);

        group.MapGet("/", async (IGetAllHandler<TDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(cancellationToken)).ToOk())
            .RequireAuthorization(view);

        // Declared before "/{id:int}" for readability; the int constraint keeps the two routes apart.
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] TFilter filter,
                IQueryHandler<TDto, TFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<TDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);
    }
}
