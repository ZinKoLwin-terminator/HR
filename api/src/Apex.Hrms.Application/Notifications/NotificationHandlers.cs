using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Notifications;

namespace Apex.Hrms.Application.Notifications;

/// <summary>Mirrors AppNotification in activity.models.ts. <c>Read</c> is the single flag of the row.</summary>
public sealed record NotificationDto(long Id, string Title, string Message, string Icon, string? Link, DateTimeOffset Timestamp, bool Read)
{
    public static NotificationDto From(Notification notification) => new(
        notification.Id,
        notification.Title,
        notification.Message,
        notification.Icon,
        notification.Link,
        new DateTimeOffset(DateTime.SpecifyKind(notification.CreatedAtUtc, DateTimeKind.Utc)),
        notification.IsRead);
}

/// <param name="UnreadOnly">Only notifications that are not read yet.</param>
public sealed record NotificationFilter(bool UnreadOnly = false);

/// <summary>
/// Persistence port for notifications. Every method takes the user: a caller can only ever reach the rows addressed to
/// the user and the broadcasts (RecipientUserId NULL), so another user's notification behaves like a missing one.
/// </summary>
public interface INotificationStore
{
    Task<PagedResult<Notification>> QueryVisibleAsync(int userId, bool unreadOnly, int pageIndex, int pageSize, CancellationToken cancellationToken);

    /// <summary>The notification when it is addressed to <paramref name="userId"/> or is a broadcast; tracked for change.</summary>
    Task<Notification?> FindVisibleAsync(long id, int userId, CancellationToken cancellationToken);

    /// <summary>All unread notifications visible to the user; tracked for change.</summary>
    Task<IReadOnlyList<Notification>> ListUnreadVisibleAsync(int userId, CancellationToken cancellationToken);
}

public interface INotificationQueryHandler
{
    Task<Result<PagedResult<NotificationDto>>> HandleAsync(PageRequest<NotificationFilter> request, CancellationToken cancellationToken);
}

public interface IMarkNotificationReadHandler
{
    Task<Result<NotificationDto>> HandleAsync(long id, CancellationToken cancellationToken);
}

public interface IMarkAllNotificationsReadHandler
{
    Task<Result> HandleAsync(CancellationToken cancellationToken);
}

internal static class NotificationCaller
{
    public static Result<int> UserOf(ICurrentUser user) =>
        user is { IsAuthenticated: true, UserId: { } id }
            ? Result.Success(id)
            : Result.Failure<int>(AppError.Unauthorized("Sign in to see notifications."));
}

/// <summary>The signed-in user's notifications (their own and the broadcasts), newest first.</summary>
internal sealed class NotificationQueryHandler(INotificationStore store, ICurrentUser user) : INotificationQueryHandler
{
    public async Task<Result<PagedResult<NotificationDto>>> HandleAsync(
        PageRequest<NotificationFilter> request,
        CancellationToken cancellationToken)
    {
        var caller = NotificationCaller.UserOf(user);
        if (caller.IsFailure)
        {
            return Result.Failure<PagedResult<NotificationDto>>(caller.Error);
        }

        var page = await store.QueryVisibleAsync(
            caller.Value,
            request.Filters.UnreadOnly,
            Math.Max(0, request.PageIndex),
            Paging.ClampPageSize(request.PageSize),
            cancellationToken);
        return Result.Success(page.Map(NotificationDto.From));
    }
}

/// <summary>
/// Marks one notification read (idempotent). Reading a broadcast marks the shared row, so it is read for everyone: the
/// table has one read flag per row.
/// </summary>
internal sealed class MarkNotificationReadHandler(
    INotificationStore store,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork unitOfWork) : IMarkNotificationReadHandler
{
    public async Task<Result<NotificationDto>> HandleAsync(long id, CancellationToken cancellationToken)
    {
        var caller = NotificationCaller.UserOf(user);
        if (caller.IsFailure)
        {
            return Result.Failure<NotificationDto>(caller.Error);
        }

        var notification = await store.FindVisibleAsync(id, caller.Value, cancellationToken);
        if (notification is null)
        {
            return Result.Failure<NotificationDto>(AppError.NotFound($"Notification #{id} was not found."));
        }

        notification.MarkRead(clock.UtcNow.UtcDateTime);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<NotificationDto>(saved.Error)
            : Result.Success(NotificationDto.From(notification));
    }
}

internal sealed class MarkAllNotificationsReadHandler(
    INotificationStore store,
    ICurrentUser user,
    IClock clock,
    IUnitOfWork unitOfWork) : IMarkAllNotificationsReadHandler
{
    public async Task<Result> HandleAsync(CancellationToken cancellationToken)
    {
        var caller = NotificationCaller.UserOf(user);
        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var unread = await store.ListUnreadVisibleAsync(caller.Value, cancellationToken);
        var now = clock.UtcNow.UtcDateTime;
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        return unread.Count == 0 ? Result.Success() : await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
