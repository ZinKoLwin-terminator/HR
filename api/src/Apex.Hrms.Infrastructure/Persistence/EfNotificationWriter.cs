using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Notifications;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Stages app.Notification rows in the request's context; the caller's unit of work saves them with the change they announce.
/// The content is never logged.
/// </summary>
internal sealed class EfNotificationWriter(HrmsDbContext context, IClock clock) : INotificationWriter
{
    public Result Add(IEnumerable<int> recipientUserIds, NotificationContent content)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);
        ArgumentNullException.ThrowIfNull(content);

        var now = clock.UtcNow.UtcDateTime;
        var staged = new List<Notification>();
        foreach (var recipient in recipientUserIds.Distinct())
        {
            var notification = Notification.Create(recipient, content.Title, content.Message, content.Icon, content.Link, now);
            if (notification.IsFailure)
            {
                return notification;
            }

            staged.Add(notification.Value);
        }

        context.AddRange(staged);
        return Result.Success();
    }
}
