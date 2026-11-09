using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.UseCases;

namespace Apex.Hrms.Api.Endpoints;

internal static class CrudEndpoints
{
    /// <summary>
    /// Maps the six CrudDataService contract methods of the frontend to REST routes under
    /// <paramref name="route"/>. Endpoints only translate HTTP; every rule lives in the use-case handlers.
    /// </summary>
    public static RouteGroupBuilder MapCrud<TDto, TRequest, TFilter>(
        this IEndpointRouteBuilder routes,
        string route,
        string tag)
        where TDto : IIdentified
        where TRequest : IVersionedRequest
    {
        var group = routes.MapGroup(route).WithTags(tag);

        group.MapGet("/", async (IGetAllHandler<TDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(cancellationToken)).ToOk());

        // Declared before "/{id:int}" for readability; the int constraint keeps the two routes apart.
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] TFilter filter,
                IQueryHandler<TDto, TFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk());

        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<TDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk());

        group.MapPost("/", async (TRequest request, ICreateHandler<TRequest, TDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(request, cancellationToken)).ToCreated(dto => $"{route}/{dto.Id}"));

        group.MapPut(
            "/{id:int}",
            async (int id, TRequest request, IUpdateHandler<TRequest, TDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk());

        group.MapDelete("/{id:int}", async (int id, IDeleteHandler<TDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToNoContent());

        return group;
    }
}
