using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Notifications;

/// <summary>
/// An in-app notification (app.Notification). A row with no recipient is a broadcast shown to every user. The table
/// has a single read flag per row, so reading a broadcast marks it read for everyone: a known limit of the schema.
/// </summary>
public sealed class Notification
{
    public const int TitleMaxLength = 120;
    public const int MessageMaxLength = 500;
    public const int IconMaxLength = 40;
    public const int LinkMaxLength = 300;

    private Notification()
    {
    }

    /// <summary>The table key is a bigint.</summary>
    public long Id { get; private set; }

    /// <summary>The sec.AppUser it is for; null for a broadcast.</summary>
    public int? RecipientUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    /// <summary>A Material icon name, for example "event_available".</summary>
    public string Icon { get; private set; } = string.Empty;

    /// <summary>An in-app route, for example "/leave/approvals".</summary>
    public string? Link { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsRead { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public bool IsBroadcast => RecipientUserId is null;

    /// <summary>The caller's own notifications and the broadcasts; nobody sees another user's.</summary>
    public bool IsVisibleTo(int userId) => RecipientUserId is null || RecipientUserId == userId;

    public static Result<Notification> Create(
        int? recipientUserId,
        string title,
        string message,
        string icon,
        string? link,
        DateTime createdAtUtc)
    {
        var violations = new Violations()
            .Require("title", "Title", title)
            .AddIf(title.Trim().Length > TitleMaxLength, "title", $"Title must be at most {TitleMaxLength} characters.")
            .Require("message", "Message", message)
            .AddIf(message.Trim().Length > MessageMaxLength, "message", $"Message must be at most {MessageMaxLength} characters.")
            .Require("icon", "Icon", icon)
            .AddIf(icon.Trim().Length > IconMaxLength, "icon", $"Icon must be at most {IconMaxLength} characters.")
            .AddIf(link is { } l && l.Trim().Length > LinkMaxLength, "link", $"Link must be at most {LinkMaxLength} characters.");
        if (violations.Any)
        {
            return Result.Failure<Notification>(violations.ToError());
        }

        return Result.Success(new Notification
        {
            RecipientUserId = recipientUserId,
            Title = title.Trim(),
            Message = message.Trim(),
            Icon = icon.Trim(),
            Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim(),
            CreatedAtUtc = createdAtUtc,
        });
    }

    /// <summary>Marks the notification read. Reading again changes nothing (the first read time is kept).</summary>
    public void MarkRead(DateTime nowUtc)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = nowUtc;
    }
}
