using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Employees.Documents;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// Employee document metadata (no file contents). A document is personal data, so reads need employees:ViewSensitive and
/// every write employees:EditSensitive. There is deliberately no "get all": documents are only listed through /query.
/// </summary>
internal static class EmployeeDocumentEndpoints
{
    private const string Route = "/api/employee-documents";

    private static readonly PermissionModule Module = PermissionModule.Employees;

    public static IEndpointRouteBuilder MapEmployeeDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Route).WithTags("Employees");
        var view = Module.PolicyName(Permission.ViewSensitive);
        var edit = Module.PolicyName(Permission.EditSensitive);

        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] EmployeeDocumentFilter filter,
                IQueryHandler<EmployeeDocumentDto, EmployeeDocumentFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<EmployeeDocumentDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapPost(
            "/",
            async (EmployeeDocumentCreateRequest request, ICreateHandler<EmployeeDocumentCreateRequest, EmployeeDocumentDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToCreated(dto => $"{Route}/{dto.Id}"))
            .RequireAuthorization(edit);

        group.MapPut(
            "/{id:int}",
            async (int id, EmployeeDocumentUpdateRequest request, IUpdateHandler<EmployeeDocumentUpdateRequest, EmployeeDocumentDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(edit);

        group.MapDelete(
            "/{id:int}",
            async (int id, [FromBody] VersionedBody? body, IDeleteHandler<EmployeeDocumentDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, body?.RowVersion, cancellationToken)).ToNoContent())
            .RequireAuthorization(edit);

        return app;
    }
}
