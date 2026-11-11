using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Activity;

/// <summary>
/// Notifications of the signed-in user: their own rows and the broadcasts, never another user's, and read marking. The
/// read flag is one per row, so reading a broadcast marks it for everyone (a limit of the schema, asserted here so a change shows).
/// </summary>
public sealed class NotificationTests : IDisposable
{
    private const int Me = 1;
    private const int Other = 2;

    private readonly TestHost host = new();

    public NotificationTests() => host.User.UserId = Me;

    public void Dispose() => host.Dispose();

    private static PageRequest<NotificationFilter> Page(bool unreadOnly = false) => new(0, 50, null, null, new NotificationFilter(unreadOnly));

    private async Task<IReadOnlyList<NotificationDto>> ListAsync(bool unreadOnly = false) =>
        ResultAssert.Succeeded(await host.Get<INotificationQueryHandler>().HandleAsync(Page(unreadOnly), default)).Items;

    [Fact]
    public async Task A_user_sees_their_own_notifications_and_the_broadcasts_only()
    {
        host.Notifications.Add(Me, "Mine");
        host.Notifications.Add(Other, "Theirs");
        host.Notifications.Add(null, "Everyone");

        var titles = (await ListAsync()).Select(n => n.Title).Order().ToList();

        Assert.Equal(["Everyone", "Mine"], titles);
    }

    [Fact]
    public async Task The_list_is_newest_first_and_has_the_shape_of_the_frontend_model()
    {
        host.Notifications.Add(Me, "Older", new DateTime(2026, 11, 8, 8, 0, 0, DateTimeKind.Utc));
        host.Notifications.Add(Me, "Newer", new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc));

        var items = await ListAsync();

        Assert.Equal(["Newer", "Older"], items.Select(n => n.Title));
        var first = items[0];
        Assert.Equal("event_available", first.Icon);
        Assert.Equal("/leave/approvals", first.Link);
        Assert.False(first.Read);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero), first.Timestamp);
    }

    [Fact]
    public async Task Reading_a_notification_marks_it_read_and_keeps_the_time()
    {
        var mine = host.Notifications.Add(Me);

        var read = ResultAssert.Succeeded(await host.Get<IMarkNotificationReadHandler>().HandleAsync(mine.Id, default));

        Assert.True(read.Read);
        Assert.True(mine.IsRead);
        Assert.Equal(host.Clock.UtcNow.UtcDateTime, mine.ReadAtUtc);

        host.Clock.TimeNow = new TimeOnly(15, 0);
        ResultAssert.Succeeded(await host.Get<IMarkNotificationReadHandler>().HandleAsync(mine.Id, default));
        Assert.Equal(new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc), mine.ReadAtUtc);
    }

    [Fact]
    public async Task Another_users_notification_cannot_be_read_and_is_not_found()
    {
        var theirs = host.Notifications.Add(Other);

        var result = await host.Get<IMarkNotificationReadHandler>().HandleAsync(theirs.Id, default);

        ResultAssert.Failed(result, ErrorType.NotFound);
        Assert.False(theirs.IsRead);
    }

    [Fact]
    public async Task A_missing_notification_is_not_found()
    {
        ResultAssert.Failed(await host.Get<IMarkNotificationReadHandler>().HandleAsync(404, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task Reading_a_broadcast_marks_the_shared_row_read_for_everyone()
    {
        var broadcast = host.Notifications.Add(null, "Holiday notice");

        ResultAssert.Succeeded(await host.Get<IMarkNotificationReadHandler>().HandleAsync(broadcast.Id, default));
        host.User.UserId = Other;

        Assert.True((await ListAsync()).Single().Read);
    }

    [Fact]
    public async Task The_unread_filter_hides_read_notifications()
    {
        var read = host.Notifications.Add(Me, "Read");
        host.Notifications.Add(Me, "Unread");
        ResultAssert.Succeeded(await host.Get<IMarkNotificationReadHandler>().HandleAsync(read.Id, default));

        Assert.Equal(["Unread"], (await ListAsync(unreadOnly: true)).Select(n => n.Title));
    }

    [Fact]
    public async Task Read_all_marks_only_what_the_user_can_see()
    {
        var mine = host.Notifications.Add(Me);
        var broadcast = host.Notifications.Add(null);
        var theirs = host.Notifications.Add(Other);

        Assert.True((await host.Get<IMarkAllNotificationsReadHandler>().HandleAsync(default)).IsSuccess);

        Assert.True(mine.IsRead);
        Assert.True(broadcast.IsRead);
        Assert.False(theirs.IsRead);
    }

    [Fact]
    public async Task Nobody_signed_in_is_unauthorized_everywhere()
    {
        host.User.UserId = null;
        host.User.IsAuthenticated = false;

        ResultAssert.Failed(await host.Get<INotificationQueryHandler>().HandleAsync(Page(), default), ErrorType.Unauthorized);
        ResultAssert.Failed(await host.Get<IMarkNotificationReadHandler>().HandleAsync(1, default), ErrorType.Unauthorized);
        ResultAssert.Failed(await host.Get<IMarkAllNotificationsReadHandler>().HandleAsync(default), ErrorType.Unauthorized);
    }
}
