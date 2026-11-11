using System.Linq.Expressions;
using System.Reflection;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Dashboard;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Notifications;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>
/// The activity log in memory: it records like the EF recorder does (the entry is written only when the unit of work
/// saves, so a failed save leaves no entry) and reads like the store. Messages are worked out at commit, so a message that
/// needs an id the "database" assigns sees it.
/// </summary>
internal sealed class FakeActivityLog(IClock clock, ICurrentUser user) : IActivityRecorder, IActivityLogStore
{
    private static readonly PropertyInfo IdProperty = typeof(ActivityLogEntry).GetProperty(nameof(ActivityLogEntry.Id))!;

    private readonly List<(string Category, Func<string> Message)> pending = [];
    private readonly List<ActivityLogEntry> entries = [];
    private long lastId;

    public IReadOnlyList<ActivityLogEntry> Entries => entries;

    public IReadOnlyList<string> Messages => [.. entries.Select(entry => entry.Message)];

    public string LastMessage => entries[^1].Message;

    public void Record(string category, string message) => pending.Add((category, () => message));

    public void Record(string category, Func<string> message) => pending.Add((category, message));

    /// <summary>Called by the fake unit of work after a successful save.</summary>
    public void Commit()
    {
        foreach (var (category, message) in pending)
        {
            Append(category, message(), user.IsSystem ? null : user.UserId, user.IsSystem ? "System" : user.DisplayName, clock.UtcNow.UtcDateTime);
        }

        pending.Clear();
    }

    /// <summary>Called by the fake unit of work when the save failed.</summary>
    public void Discard() => pending.Clear();

    /// <summary>Puts an entry straight into the log, as if an earlier request had written it.</summary>
    public ActivityLogEntry Append(string category, string message, int? actorUserId, string actorName, DateTime occurredAtUtc)
    {
        var created = ActivityLogEntry.Record(category, message, actorUserId, actorName, occurredAtUtc);
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Message : null);
        IdProperty.SetValue(created.Value, ++lastId);
        entries.Add(created.Value);
        return created.Value;
    }

    public Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityLogQuery query, CancellationToken cancellationToken)
    {
        var filtered = entries.AsEnumerable();
        if (query.Category is { } category)
        {
            filtered = filtered.Where(entry => entry.Category == category);
        }

        if (query.Actor is { } actor)
        {
            filtered = filtered.Where(entry => entry.ActorName.Contains(actor, StringComparison.OrdinalIgnoreCase));
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
            filtered = filtered.Where(entry =>
                entry.Message.Contains(term, StringComparison.OrdinalIgnoreCase)
                || entry.ActorName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.ToList();
        Func<ActivityLogEntry, object> key = query.SortKey switch
        {
            "category" => entry => entry.Category,
            "actor" => entry => entry.ActorName,
            _ => entry => entry.OccurredAtUtc,
        };
        var ordered = query.Descending
            ? list.OrderByDescending(key).ThenByDescending(entry => entry.Id)
            : list.OrderBy(key).ThenBy(entry => entry.Id);
        var pageIndex = Paging.ClampPageIndex(query.PageIndex, list.Count, query.PageSize);
        var page = ordered.Skip(pageIndex * query.PageSize).Take(query.PageSize).ToList();
        return Task.FromResult(new PagedResult<ActivityLogEntry>(page, list.Count, pageIndex, query.PageSize));
    }

    public Task<ActivityLogEntry?> FindAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(entries.FirstOrDefault(entry => entry.Id == id));

    public Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ActivityLogEntry>>(
            [.. entries.OrderByDescending(entry => entry.OccurredAtUtc).ThenByDescending(entry => entry.Id).Take(limit)]);
}

/// <summary>Notifications in memory with the same visibility rule as the EF store: own rows and broadcasts only.</summary>
internal sealed class FakeNotificationStore : INotificationStore
{
    private static readonly PropertyInfo IdProperty = typeof(Notification).GetProperty(nameof(Notification.Id))!;

    private readonly List<Notification> items = [];
    private long lastId;

    public IReadOnlyList<Notification> Items => items;

    public Notification Add(int? recipientUserId, string title = "Leave request", DateTime? createdAtUtc = null)
    {
        var created = Notification.Create(
            recipientUserId,
            title,
            "A request needs your attention.",
            "event_available",
            "/leave/approvals",
            createdAtUtc ?? new DateTime(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc));
        Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Message : null);
        IdProperty.SetValue(created.Value, ++lastId);
        items.Add(created.Value);
        return created.Value;
    }

    public Task<PagedResult<Notification>> QueryVisibleAsync(int userId, bool unreadOnly, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var visible = items.Where(n => n.IsVisibleTo(userId) && (!unreadOnly || !n.IsRead))
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .ToList();
        var page = Paging.ClampPageIndex(pageIndex, visible.Count, pageSize);
        return Task.FromResult(new PagedResult<Notification>([.. visible.Skip(page * pageSize).Take(pageSize)], visible.Count, page, pageSize));
    }

    public Task<Notification?> FindVisibleAsync(long id, int userId, CancellationToken cancellationToken) =>
        Task.FromResult(items.FirstOrDefault(n => n.Id == id && n.IsVisibleTo(userId)));

    public Task<IReadOnlyList<Notification>> ListUnreadVisibleAsync(int userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>([.. items.Where(n => n.IsVisibleTo(userId) && !n.IsRead)]);
}

/// <summary>The dashboard source with rows the test sets; employees can also be derived from the in-memory repository.</summary>
internal sealed class FakeDashboardSource : IDashboardSource
{
    public List<EmployeeFigureRow> Employees { get; } = [];

    public RecruitmentRows Recruitment { get; set; } = new(0, 0, new Dictionary<string, int>(), 0);

    public List<PayrollRunRow> PayrollRuns { get; } = [];

    public Task<IReadOnlyList<EmployeeFigureRow>> EmployeesAsync(Expression<Func<Employee, bool>> scope, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeFigureRow>>([.. Employees]);

    public Task<RecruitmentRows> RecruitmentAsync(DateOnly today, CancellationToken cancellationToken) => Task.FromResult(Recruitment);

    public Task<IReadOnlyList<PayrollRunRow>> ProcessedPayrollRunsAsync(int count, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PayrollRunRow>>([.. PayrollRuns.Take(count)]);
}
