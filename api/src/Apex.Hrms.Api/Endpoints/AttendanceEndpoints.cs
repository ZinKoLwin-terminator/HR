using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Attendance.Adjustments;
using Apex.Hrms.Application.Attendance.Approvals;
using Apex.Hrms.Application.Attendance.Holidays;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Attendance.Views;
using Apex.Hrms.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Hrms.Api.Endpoints;

internal static class AttendanceEndpoints
{
    private const string Tag = "Attendance";

    private static readonly PermissionModule Module = PermissionModule.Attendance;

    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapCrud<ShiftDto, ShiftCreateRequest, ShiftUpdateRequest, ShiftFilter>("/api/shifts", Tag, Module, CrudOperations.All, Permission.Edit)
            .MapStatusCommands<ShiftDto>(Module);
        app.MapCrud<HolidayDto, HolidayCreateRequest, HolidayUpdateRequest, HolidayFilter>("/api/holidays", Tag, Module, CrudOperations.All, Permission.Edit);

        MapRecords(app);
        MapAdjustments(app);
        MapOvertime(app);
        MapViews(app);
        return app;
    }

    /// <summary>
    /// Attendance records are read-only here: they are written by punches (and by approved adjustments), never directly.
    /// A punch carries no date and no time; the server stamps it. Punching is the employee's own action, so it needs only
    /// attendance:Create, which every employee has.
    /// </summary>
    private static void MapRecords(IEndpointRouteBuilder app)
    {
        var view = Module.PolicyName(Permission.View);
        var group = app.MapGroup("/api/attendance-records").WithTags(Tag);
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] AttendanceFilter filter,
                IQueryHandler<AttendanceListItemDto, AttendanceFilter> handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);
        group.MapGet("/{id:int}", async (int id, IGetByIdHandler<AttendanceListItemDto> handler, CancellationToken cancellationToken) =>
            (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);

        app.MapPost(
            "/api/attendance/punches",
            async ([FromBody] PunchRequest? request, IPunchHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request ?? new PunchRequest(0, null), cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(Module.PolicyName(Permission.Create));
    }

    /// <summary>
    /// Requests are made with attendance:Create (the employee's own action) and decided with attendance:Approve. There is no
    /// "get all" and no update: a pending request is deleted and made again, a decided one stays as the record.
    /// </summary>
    private static void MapAdjustments(IEndpointRouteBuilder app)
    {
        var group = app.MapCrud<AdjustmentDto, AdjustmentCreateRequest, object, AdjustmentFilter>(
            "/api/attendance-adjustments",
            Tag,
            Module,
            CrudOperations.Create | CrudOperations.Delete);
        MapReads<AdjustmentDto, AdjustmentFilter>(group);
        group
            .MapCommand<AdjustmentDto, RemarksBody, ApproveAdjustmentCommand>(
                "approve",
                Module,
                (id, body) => new ApproveAdjustmentCommand(id, body?.RowVersion, body?.Remarks),
                Permission.Approve)
            .MapCommand<AdjustmentDto, RemarksBody, RejectAdjustmentCommand>(
                "reject",
                Module,
                (id, body) => new RejectAdjustmentCommand(id, body?.RowVersion, body?.Remarks),
                Permission.Approve);
    }

    private static void MapOvertime(IEndpointRouteBuilder app)
    {
        var group = app.MapCrud<OvertimeDto, OvertimeCreateRequest, object, OvertimeFilter>(
            "/api/overtime-records",
            Tag,
            Module,
            CrudOperations.Create | CrudOperations.Delete);
        MapReads<OvertimeDto, OvertimeFilter>(group);
        group
            .MapCommand<OvertimeDto, RemarksBody, ApproveOvertimeCommand>(
                "approve",
                Module,
                (id, body) => new ApproveOvertimeCommand(id, body?.RowVersion, body?.Remarks),
                Permission.Approve)
            .MapCommand<OvertimeDto, RemarksBody, RejectOvertimeCommand>(
                "reject",
                Module,
                (id, body) => new RejectOvertimeCommand(id, body?.RowVersion, body?.Remarks),
                Permission.Approve);
    }

    private static void MapReads<TDto, TFilter>(RouteGroupBuilder group)
    {
        var view = Module.PolicyName(Permission.View);
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

    private static void MapViews(IEndpointRouteBuilder app)
    {
        var view = Module.PolicyName(Permission.View);

        app.MapGet(
            "/api/attendance/overview",
            async ([FromQuery] DateOnly date, [FromQuery] int? branchId, IAttendanceOverviewHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(date, branchId, cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(view);

        app.MapGet(
            "/api/attendance/schedule",
            async ([AsParameters] WorkScheduleRequest request, IWorkScheduleHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToOk())
            .WithTags(Tag)
            .RequireAuthorization(view);
    }

    /// <summary>Body of approve and reject: <c>{ "rowVersion": "...", "remarks": "..." }</c>. Remarks are required to reject.</summary>
    private sealed record RemarksBody(string? RowVersion, string? Remarks);
}
