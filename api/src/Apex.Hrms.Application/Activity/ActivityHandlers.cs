using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Activity;

/// <summary>
/// Read side of app.ActivityLog. There is deliberately no method that adds, changes or removes an entry: entries are
/// appended through <see cref="IActivityRecorder"/> in the unit of work of the change they describe, and never edited.
/// </summary>
public interface IActivityLogStore
{
    Task<PagedResult<ActivityLogEntry>> QueryAsync(ActivityLogQuery query, CancellationToken cancellationToken);

    Task<ActivityLogEntry?> FindAsync(long id, CancellationToken cancellationToken);

    /// <summary>The newest entries first (the dashboard shows the latest six).</summary>
    Task<IReadOnlyList<ActivityLogEntry>> RecentAsync(int limit, CancellationToken cancellationToken);
}

public interface IActivityQueryHandler
{
    Task<Result<PagedResult<ActivityEntryDto>>> HandleAsync(PageRequest<ActivityFilter> request, CancellationToken cancellationToken);
}

public interface IActivityGetHandler
{
    Task<Result<ActivityEntryDto>> HandleAsync(long id, CancellationToken cancellationToken);
}

/// <summary>
/// The activity feed, newest first. Sort by <c>timestamp</c> (default, descending), <c>category</c> or <c>actor</c>.
/// Filters: category (exact), actor (part of the name), dateFrom and dateTo (inclusive days in the business time zone);
/// search matches the message or the actor name.
/// </summary>
internal sealed class ActivityQueryHandler(IActivityLogStore store, IClock clock) : IActivityQueryHandler
{
    public const string TimestampKey = "timestamp";

    private static readonly string[] SortKeys = [TimestampKey, "category", "actor"];

    public async Task<Result<PagedResult<ActivityEntryDto>>> HandleAsync(
        PageRequest<ActivityFilter> request,
        CancellationToken cancellationToken)
    {
        var filter = request.Filters;
        var violations = new Violations()
            .AddIf(
                !string.IsNullOrWhiteSpace(filter.Category) && !ActivityCategories.IsValid(filter.Category.Trim()),
                "category",
                $"Category must be one of: {string.Join(", ", ActivityCategories.All)}.")
            .AddIf(
                request.Search is { Length: > Paging.MaxSearchLength },
                "search",
                $"Search must be at most {Paging.MaxSearchLength} characters.")
            .AddIf(
                filter.Actor is { Length: > Paging.MaxSearchLength },
                "actor",
                $"Actor must be at most {Paging.MaxSearchLength} characters.")
            .AddIf(
                filter.DateFrom is { } from && filter.DateTo is { } to && to < from,
                "dateTo",
                "The end date must be on or after the start date.");

        var sortKey = TimestampKey;
        var descending = true;
        if (request.Sort is { Active: { Length: > 0 } active, Direction: { Length: > 0 } direction })
        {
            var known = SortKeys.FirstOrDefault(key => key.Equals(active, StringComparison.OrdinalIgnoreCase));
            if (known is null)
            {
                violations.Add("sort", "Choose a sortable field.", $"Cannot sort by \"{active}\".");
            }
            else
            {
                sortKey = known;
                descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            }
        }

        if (violations.Any)
        {
            return Result.Failure<PagedResult<ActivityEntryDto>>(violations.ToError());
        }

        var query = new ActivityLogQuery(
            string.IsNullOrWhiteSpace(filter.Category) ? null : filter.Category.Trim(),
            string.IsNullOrWhiteSpace(filter.Actor) ? null : filter.Actor.Trim(),
            filter.DateFrom is { } dateFrom ? clock.StartOfDayUtc(dateFrom) : null,
            filter.DateTo is { } dateTo ? clock.StartOfDayUtc(dateTo.AddDays(1)) : null,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            descending,
            sortKey,
            Math.Max(0, request.PageIndex),
            Paging.ClampPageSize(request.PageSize));

        var page = await store.QueryAsync(query, cancellationToken);
        return Result.Success(page.Map(ActivityEntryDto.From));
    }
}

internal sealed class ActivityGetHandler(IActivityLogStore store) : IActivityGetHandler
{
    public async Task<Result<ActivityEntryDto>> HandleAsync(long id, CancellationToken cancellationToken)
    {
        var entry = await store.FindAsync(id, cancellationToken);
        return entry is null
            ? Result.Failure<ActivityEntryDto>(AppError.NotFound($"Activity entry #{id} was not found."))
            : Result.Success(ActivityEntryDto.From(entry));
    }
}
