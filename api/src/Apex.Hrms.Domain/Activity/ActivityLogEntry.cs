using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Activity;

/// <summary>
/// One line of the activity feed (app.ActivityLog). The log is append-only: an entry is created once and has no
/// method, and no public setter, that changes it. The actor name is a snapshot, kept even if the user is removed later.
/// Messages carry ids and codes, never personal data (no national id, bank detail or salary).
/// </summary>
public sealed class ActivityLogEntry
{
    public const int MessageMaxLength = 500;
    public const int ActorNameMaxLength = 130;

    private ActivityLogEntry()
    {
    }

    /// <summary>The table key is a bigint.</summary>
    public long Id { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    /// <summary>The sec.AppUser of the actor; null for the system identity.</summary>
    public int? ActorUserId { get; private set; }

    public string ActorName { get; private set; } = string.Empty;

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// Builds an entry. Text longer than the columns is cut (with an ellipsis for the message) instead of failing the
    /// business change that is being logged.
    /// </summary>
    public static Result<ActivityLogEntry> Record(
        string category,
        string message,
        int? actorUserId,
        string actorName,
        DateTime occurredAtUtc)
    {
        var violations = new Violations()
            .AddIf(!ActivityCategories.IsValid(category), "category", "Category is not one of the known activity categories.")
            .Require("message", "Message", message)
            .Require("actorName", "Actor name", actorName);
        if (violations.Any)
        {
            return Result.Failure<ActivityLogEntry>(violations.ToError());
        }

        return Result.Success(new ActivityLogEntry
        {
            Category = category,
            Message = Truncate(message.Trim(), MessageMaxLength, ellipsis: true),
            ActorUserId = actorUserId,
            ActorName = Truncate(actorName.Trim(), ActorNameMaxLength, ellipsis: false),
            OccurredAtUtc = occurredAtUtc,
        });
    }

    private static string Truncate(string text, int maxLength, bool ellipsis) =>
        text.Length <= maxLength
            ? text
            : ellipsis ? string.Concat(text.AsSpan(0, maxLength - 1), "…") : text[..maxLength];
}
