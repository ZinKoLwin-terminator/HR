using Apex.Hrms.Application;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Reads and tracks app.Notification rows for one user: their own rows and the broadcasts (RecipientUserId NULL). Every
/// query carries that visibility filter, so another user's row cannot be reached by id either.
/// </summary>
internal sealed class EfNotificationStore(HrmsDbContext context) : INotificationStore
{
    private IQueryable<Notification> VisibleTo(int userId) =>
        context.Set<Notification>().Where(n => n.RecipientUserId == null || n.RecipientUserId == userId);

    public async Task<PagedResult<Notification>> QueryVisibleAsync(
        int userId,
        bool unreadOnly,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var visible = VisibleTo(userId).AsNoTracking();
        if (unreadOnly)
        {
            visible = visible.Where(n => !n.IsRead);
        }

        var total = await visible.CountAsync(cancellationToken);
        var page = Paging.ClampPageIndex(pageIndex, total, pageSize);
        var items = await visible
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Notification>(items, total, page, pageSize);
    }

    public Task<Notification?> FindVisibleAsync(long id, int userId, CancellationToken cancellationToken) =>
        VisibleTo(userId).SingleOrDefaultAsync(n => n.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListUnreadVisibleAsync(int userId, CancellationToken cancellationToken) =>
        await VisibleTo(userId).Where(n => !n.IsRead).ToListAsync(cancellationToken);
}
