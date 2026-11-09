using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

internal static class CommandEndpoints
{
    /// <summary>
    /// Maps <c>POST {group}/{id}/{action}</c> with body <c>{ "rowVersion": "..." }</c> to a command handler: load by
    /// id, check the row version, run the domain method, save, and return the record with its new row version.
    /// </summary>
    public static RouteGroupBuilder MapCommand<TDto, TCommand>(
        this RouteGroupBuilder group,
        string action,
        PermissionModule module,
        Permission permission = Permission.Edit)
        where TCommand : IEntityCommand<TCommand>
    {
        group.MapPost(
            $"/{{id:int}}/{action}",
            async (int id, [FromBody] VersionedBody? body, ICommandHandler<TCommand, TDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(TCommand.From(id, body?.RowVersion), cancellationToken)).ToOk())
            .RequireAuthorization(module.PolicyName(permission));
        return group;
    }

    /// <summary>
    /// Like <see cref="MapCommand{TDto, TCommand}"/> for a command whose body carries more than the row version (for
    /// example the exit date). <paramref name="build"/> turns the route id and the body into the command; a missing
    /// body is passed as null and ends as a Validation error on <c>rowVersion</c>.
    /// </summary>
    public static RouteGroupBuilder MapCommand<TDto, TBody, TCommand>(
        this RouteGroupBuilder group,
        string action,
        PermissionModule module,
        Func<int, TBody?, TCommand> build,
        Permission permission = Permission.Edit)
        where TBody : class
        where TCommand : IEntityCommand
    {
        group.MapPost(
            $"/{{id:int}}/{action}",
            async (int id, [FromBody] TBody? body, ICommandHandler<TCommand, TDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(build(id, body), cancellationToken)).ToOk())
            .RequireAuthorization(module.PolicyName(permission));
        return group;
    }

    /// <summary>The activate and deactivate commands every coded record (the organization resources) has.</summary>
    public static RouteGroupBuilder MapStatusCommands<TDto>(this RouteGroupBuilder group, PermissionModule module) => group
        .MapCommand<TDto, ActivateCommand>("activate", module)
        .MapCommand<TDto, DeactivateCommand>("deactivate", module);
}
