using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Balances;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

internal static class LeaveEndpoints
{
    private const string Tag = "Leave";

    private static readonly PermissionModule Module = PermissionModule.Leave;

    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<LeaveTypeDto, LeaveTypeCreateRequest, LeaveTypeUpdateRequest, LeaveTypeFilter>("/api/leave-types", Tag, Module)
            .MapStatusCommands<LeaveTypeDto>(Module);
        app.MapCrud<LeavePolicyDto, LeavePolicyCreateRequest, LeavePolicyUpdateRequest, LeavePolicyFilter>("/api/leave-policies", Tag, Module)
            .MapStatusCommands<LeavePolicyDto>(Module);

        MapApplications(app);
        MapBalancesAndCalendar(app);
        return app;
    }

    /// <summary>
    /// Create, update and delete come from the shared CRUD mapping. Status never changes through PUT: each move is a
    /// command. Submitting and cancelling belong to the person who makes the request (leave:Create, which every
    /// employee has); deciding needs leave:Approve.
    /// </summary>
    private static void MapApplications(IEndpointRouteBuilder app)
    {
        // There is deliberately no "get all": applications carry reasons (sometimes medical), so they are only listed
        // through /query, in pages of a limited size.
        var group = app.MapCrud<LeaveApplicationDto, LeaveApplicationCreateRequest, LeaveApplicationUpdateRequest, LeaveApplicationFilter>(
            "/api/leave-applications",
            Tag,
            Module,
            CrudOperations.Create | CrudOperations.Update | CrudOperations.Delete);

        var view = Module.PolicyName(Permission.View);
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] LeaveApplicationFilter filter,
                IQueryHandler<LeaveApplicationDto, LeaveApplicationFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);
        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<LeaveApplicationDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        MapApplicationCommands(group);
    }

    private static void MapApplicationCommands(RouteGroupBuilder group) => group
        .MapCommand<LeaveApplicationDto, SubmitLeaveCommand>("submit", Module, Permission.Create)
        .MapCommand<LeaveApplicationDto, RemarksBody, ApproveLeaveCommand>(
            "approve",
            Module,
            (id, body) => new ApproveLeaveCommand(id, body?.RowVersion, body?.Remarks),
            Permission.Approve)
        .MapCommand<LeaveApplicationDto, RemarksBody, RejectLeaveCommand>(
            "reject",
            Module,
            (id, body) => new RejectLeaveCommand(id, body?.RowVersion, body?.Remarks),
            Permission.Approve)
        .MapCommand<LeaveApplicationDto, RemarksBody, CancelLeaveCommand>(
            "cancel",
            Module,
            (id, body) => new CancelLeaveCommand(id, body?.RowVersion, body?.Remarks),
            Permission.Create);

    private static void MapBalancesAndCalendar(IEndpointRouteBuilder app)
    {
        var view = Module.PolicyName(Permission.View);

        app.MapGet(
            "/api/leave/balances",
            async ([FromQuery] int employeeId, [FromQuery] int? year, ILeaveBalanceHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(employeeId, year, cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(view);

        app.MapGet(
            "/api/leave/calendar",
            async ([AsParameters] LeaveCalendarRequest request, ILeaveCalendarHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(view);
    }

    /// <summary>Body of approve, reject and cancel: <c>{ "rowVersion": "...", "remarks": "..." }</c>. Remarks are required to reject.</summary>
    private sealed record RemarksBody(string? RowVersion, string? Remarks);
}
