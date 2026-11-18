using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Notifications;

/// <summary>
/// The text of an in-app notification (app.Notification): a title, a message, a Material icon name and an optional in-app
/// route. Modules that notify about confidential records (grievances) put only a reference and the event in it, never content.
/// </summary>
public sealed record NotificationContent(string Title, string Message, string Icon, string? Link);

/// <summary>
/// Adds in-app notifications for named users. The rows are only staged: they are saved with the caller's unit of work, so a
/// notification exists exactly when the change it announces was saved. There is no broadcast here: every row has a recipient.
/// </summary>
public interface INotificationWriter
{
    /// <summary>
    /// Stages one notification per distinct recipient (none when the list is empty). A Validation failure, and nothing staged,
    /// when the content breaks the column limits of <see cref="Domain.Notifications.Notification"/>.
    /// </summary>
    Result Add(IEnumerable<int> recipientUserIds, NotificationContent content);
}

/// <summary>
/// Finds who a notification goes to. Only accounts that can sign in count: an Active account with an Active role, whose employee
/// (if any) has not left. Nothing here is visible to the caller of the request; it is used to address notifications only.
/// </summary>
public interface INotificationRecipients
{
    /// <summary>The account (sec.AppUser) of the employee, or null when the employee has none that can sign in.</summary>
    Task<int?> UserIdOfEmployeeAsync(int employeeId, CancellationToken cancellationToken);

    /// <summary>
    /// The accounts whose role holds <paramref name="moduleKey"/>:<paramref name="action"/> (HR Administrator holds every grant),
    /// leaving out the accounts of <paramref name="excludedEmployeeIds"/> (for example the employee a grievance is about).
    /// </summary>
    Task<IReadOnlyList<int>> UserIdsHoldingAsync(
        string moduleKey,
        string action,
        IReadOnlyCollection<int> excludedEmployeeIds,
        CancellationToken cancellationToken);
}
