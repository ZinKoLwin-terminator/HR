using Apex.Hrms.Api.Authorization;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Dashboard;
using Apex.Hrms.Application.Notifications;

namespace Apex.Hrms.Api.Endpoints;

/// <summary>
/// The dashboard summary, the activity log and the signed-in user's notifications. All three are read-mostly: the activity
/// log has no create, update or delete route (entries are appended by the use cases that make the changes), and the only
/// write of notifications is marking the caller's own notifications read.
/// </summary>
internal static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        MapDashboard(app);
        MapActivity(app);
        MapNotifications(app);
        return app;
    }

    private static void MapDashboard(IEndpointRouteBuilder app) =>
        app.MapGet(
            "/api/dashboard/summary",
            async (IDashboardHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken)).ToOk())
            .WithTags("Dashboard")
            .RequireAuthorization(PermissionModule.Dashboard.PolicyName(Permission.View));

    private static void MapActivity(IEndpointRouteBuilder app)
    {
        var view = PermissionModule.Activity.PolicyName(Permission.View);
        var group = app.MapGroup("/api/activity").WithTags("Activity");
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] ActivityFilter filter,
                IActivityQueryHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(view);
        group.MapGet(
            "/{id:long}",
            async (long id, IActivityGetHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(view);
    }

    private static void MapNotifications(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").WithTags("Notifications");
        group.MapGet(
            "/query",
            async (
                [AsParameters] PageQuery page,
                [AsParameters] NotificationFilter filter,
                INotificationQueryHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.HandleAsync(page.ToRequest(filter), cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.SignedInPolicy);
        group.MapPost(
            "/{id:long}/read",
            async (long id, IMarkNotificationReadHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(AuthorizationSetup.SignedInPolicy);
        group.MapPost(
            "/read-all",
            async (IMarkAllNotificationsReadHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(cancellationToken)).ToNoContent())
            .RequireAuthorization(AuthorizationSetup.SignedInPolicy);
    }
}
