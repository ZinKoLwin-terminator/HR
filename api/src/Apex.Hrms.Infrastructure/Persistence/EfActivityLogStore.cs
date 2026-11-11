using Apex.Hrms.Application;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Domain.Activity;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>Reads app.ActivityLog. Read only: rows are appended by <see cref="EfActivityRecorder"/> and never changed.</summary>
internal sealed class EfActivityLogStore(HrmsDbContext context) : IActivityLogStore
{
    private IQueryable<ActivityLogEntry> Entries => context.Set<ActivityLogEntry>().AsNoTracking();

    public async Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityLogQuery query, CancellationToken cancellationToken)
    {
        var filtered = Entries;
        if (query.Category is { } category)
        {
            filtered = filtered.Where(entry => entry.Category == category);
        }

        if (query.Actor is { } actor)
        {
            filtered = filtered.Where(entry => entry.ActorName.Contains(actor));
        }

        if (query.FromUtc is { } from)
        {
            filtered = filtered.Where(entry => entry.OccurredAtUtc >= from);
        }

        if (query.ToUtc is { } to)
        {
            filtered = filtered.Where(entry => entry.OccurredAtUtc < to);
        }

        if (query.Search is { } term)
        {
            filtered = filtered.Where(entry => entry.Message.Contains(term) || entry.ActorName.Contains(term));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var pageIndex = Paging.ClampPageIndex(query.PageIndex, total, query.PageSize);
        var items = await Order(filtered, query)
            .Skip(pageIndex * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ActivityLogEntry>(items, total, pageIndex, query.PageSize);
    }

    public Task<ActivityLogEntry?> FindAsync(long id, CancellationToken cancellationToken) =>
        Entries.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int limit, CancellationToken cancellationToken) =>
        await Entries
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Take(Math.Clamp(limit, 1, Paging.MaxPageSize))
            .ToListAsync(cancellationToken);

    /// <summary>Id is always the last key so paging stays stable when the chosen column has ties.</summary>
    private static IOrderedQueryable<ActivityLogEntry> Order(IQueryable<ActivityLogEntry> source, ActivityLogQuery query) =>
        query.SortKey switch
        {
            "category" => query.Descending
                ? source.OrderByDescending(entry => entry.Category).ThenByDescending(entry => entry.Id)
                : source.OrderBy(entry => entry.Category).ThenBy(entry => entry.Id),
            "actor" => query.Descending
                ? source.OrderByDescending(entry => entry.ActorName).ThenByDescending(entry => entry.Id)
                : source.OrderBy(entry => entry.ActorName).ThenBy(entry => entry.Id),
            _ => query.Descending
                ? source.OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id)
                : source.OrderBy(entry => entry.OccurredAtUtc).ThenBy(entry => entry.Id),
        };
}
