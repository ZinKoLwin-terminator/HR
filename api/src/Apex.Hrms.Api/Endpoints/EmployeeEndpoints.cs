using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Employees.Salary;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

internal static class EmployeeEndpoints
{
    private const string Route = "/api/employees";
    private const string Tag = "Employees";

    private static readonly PermissionModule Module = PermissionModule.Employees;

    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        // Create, update and delete come from the shared CRUD mapping. There is deliberately no "get all": employees
        // are only listed through /query (pages of at most 200), so no single call returns the whole workforce.
        var group = app.MapCrud<EmployeeDto, EmployeeCreateRequest, EmployeeUpdateRequest, EmployeeFilter>(
            Route,
            Tag,
            Module,
            CrudOperations.Create | CrudOperations.Update | CrudOperations.Delete);

        MapReads(group);
        MapPersonal(group);
        MapBankAccount(group);
        MapCommands(group);
        return app;
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        var view = Module.PolicyName(Permission.View);

        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] EmployeeFilter filter,
                IQueryHandler<EmployeeListItemDto, EmployeeFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<EmployeeDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        group.MapGet("/{id:int}/history", async (int id, IEmployeeHistoryHandler handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        // Suggests a code for the form. It consumes a number of the database sequence, so it is not a plain read:
        // the caller needs the right to create employees.
        group.MapGet("/next-code", async (INextEmployeeCodeHandler handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.Create));

        // DEMONSTRATION figures for the employee's current salary; a salary is personal data, so this is a payroll permission.
        group.MapGet("/{id:int}/salary-summary", async (int id, IGetByIdHandler<EmployeeSalarySummaryDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(PermissionModule.Payroll.PolicyName(Permission.View));
    }

    /// <summary>Personal data lives behind its own permission; no other employee endpoint returns it (security review H2).</summary>
    private static void MapPersonal(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}/personal", async (int id, IGetByIdHandler<EmployeePersonalDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.ViewSensitive));

        group.MapPut(
            "/{id:int}/personal",
            async (int id, EmployeePersonalUpdateRequest request, IUpdateHandler<EmployeePersonalUpdateRequest, EmployeePersonalDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.EditSensitive));
    }

    /// <summary>The salary bank account is a sub-resource with its own permissions; the account number is only returned in full here.</summary>
    private static void MapBankAccount(RouteGroupBuilder group)
    {
        group.MapGet("/{id:int}/bank-account", async (int id, IGetByIdHandler<BankAccountDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.ViewBank));

        group.MapPut(
            "/{id:int}/bank-account",
            async (int id, BankAccountUpdateRequest request, IUpdateHandler<BankAccountUpdateRequest, BankAccountDto> handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Module.PolicyName(Permission.EditBank));
    }

    /// <summary>Status never changes through PUT: leaving, returning and the other status moves are commands.</summary>
    private static void MapCommands(RouteGroupBuilder group) => group
        .MapCommand<EmployeeDto, ExitBody, ExitEmployeeCommand>(
            "exit",
            Module,
            (id, body) => new ExitEmployeeCommand(id, body?.RowVersion, body?.ExitDate, body?.Status))
        .MapCommand<EmployeeDto, StatusBody, ReinstateEmployeeCommand>(
            "reinstate",
            Module,
            (id, body) => new ReinstateEmployeeCommand(id, body?.RowVersion, body?.Status))
        .MapCommand<EmployeeDto, StatusBody, ChangeEmployeeStatusCommand>(
            "status",
            Module,
            (id, body) => new ChangeEmployeeStatusCommand(id, body?.RowVersion, body?.Status));

    /// <summary>Body of <c>POST /api/employees/{id}/exit</c>: <c>status</c> is Resigned or Terminated.</summary>
    private sealed record ExitBody(string? RowVersion, DateOnly? ExitDate, string? Status);

    /// <summary>Body of <c>reinstate</c> (status optional, default Active) and <c>status</c> (required).</summary>
    private sealed record StatusBody(string? RowVersion, string? Status);
}
