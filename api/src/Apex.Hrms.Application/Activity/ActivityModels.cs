using Apex.Hrms.Domain.Activity;

namespace Apex.Hrms.Application.Activity;

/// <summary>Mirrors ActivityEntry in activity.models.ts. <c>Timestamp</c> is the UTC time of the entry.</summary>
public sealed record ActivityEntryDto(long Id, string Category, string Message, string Actor, DateTimeOffset Timestamp)
{
    public static ActivityEntryDto From(ActivityLogEntry entry) => new(
        entry.Id,
        entry.Category,
        entry.Message,
        entry.ActorName,
        new DateTimeOffset(DateTime.SpecifyKind(entry.OccurredAtUtc, DateTimeKind.Utc)));
}

/// <summary>
/// Query-string filters of the activity feed. <c>Category</c> is one of the seven categories; <c>Actor</c> matches part
/// of the actor name; <c>DateFrom</c> and <c>DateTo</c> are inclusive calendar days in the business time zone.
/// </summary>
public sealed record ActivityFilter(string? Category = null, string? Actor = null, DateOnly? DateFrom = null, DateOnly? DateTo = null);

/// <summary>What the store is asked for. Dates are already instants: <c>FromUtc</c> inclusive, <c>ToUtc</c> exclusive.</summary>
public sealed record ActivityLogQuery(
    string? Category,
    string? Actor,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Search,
    bool Descending,
    string SortKey,
    int PageIndex,
    int PageSize);
