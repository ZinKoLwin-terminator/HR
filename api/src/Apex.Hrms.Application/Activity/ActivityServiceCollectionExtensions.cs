using Apex.Hrms.Application.Dashboard;
using Apex.Hrms.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Activity;

internal static class ActivityServiceCollectionExtensions
{
    /// <summary>The activity feed, the notifications and the dashboard: read handlers plus the notification read marking.</summary>
    public static IServiceCollection AddActivityAndDashboard(this IServiceCollection services)
    {
        services.AddScoped<IActivityQueryHandler, ActivityQueryHandler>();
        services.AddScoped<IActivityGetHandler, ActivityGetHandler>();
        services.AddScoped<INotificationQueryHandler, NotificationQueryHandler>();
        services.AddScoped<IMarkNotificationReadHandler, MarkNotificationReadHandler>();
        services.AddScoped<IMarkAllNotificationsReadHandler, MarkAllNotificationsReadHandler>();
        services.AddScoped<IDashboardHandler, DashboardHandler>();
        return services;
    }
}
