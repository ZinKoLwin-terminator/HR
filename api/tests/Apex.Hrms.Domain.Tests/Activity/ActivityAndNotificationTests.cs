using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Notifications;

namespace Apex.Hrms.Domain.Tests.Activity;

public sealed class ActivityLogEntryTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("employee")]
    [InlineData("organization")]
    [InlineData("attendance")]
    [InlineData("leave")]
    [InlineData("payroll")]
    [InlineData("recruitment")]
    [InlineData("system")]
    public void Every_category_of_the_database_check_is_accepted(string category)
    {
        var result = ActivityLogEntry.Record(category, "Something happened", 3, "Aye Test", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(category, result.Value.Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Leave")]
    [InlineData("payslips")]
    public void An_unknown_category_is_a_validation_error_on_category(string category)
    {
        var result = ActivityLogEntry.Record(category, "Something happened", 3, "Aye Test", Now);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("category", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void The_entry_keeps_what_it_was_given_trimmed_and_the_actor_snapshot()
    {
        var entry = ActivityLogEntry.Record("leave", "  LV-0001 approved  ", 3, " Aye Test ", Now).Value;

        Assert.Equal("LV-0001 approved", entry.Message);
        Assert.Equal(3, entry.ActorUserId);
        Assert.Equal("Aye Test", entry.ActorName);
        Assert.Equal(Now, entry.OccurredAtUtc);
    }

    [Fact]
    public void The_system_identity_has_no_user_id()
    {
        var entry = ActivityLogEntry.Record("system", "Seeded", null, "System", Now).Value;

        Assert.Null(entry.ActorUserId);
    }

    [Fact]
    public void A_message_and_an_actor_name_are_required()
    {
        var result = ActivityLogEntry.Record("leave", " ", 1, "", Now);

        Assert.True(result.IsFailure);
        Assert.Contains("message", result.Error.FieldErrors!.Keys);
        Assert.Contains("actorName", result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void Long_text_is_cut_to_the_column_sizes_so_the_logged_change_does_not_fail()
    {
        var entry = ActivityLogEntry.Record("leave", new string('m', 600), 1, new string('a', 200), Now).Value;

        Assert.Equal(ActivityLogEntry.MessageMaxLength, entry.Message.Length);
        Assert.EndsWith("…", entry.Message, StringComparison.Ordinal);
        Assert.Equal(ActivityLogEntry.ActorNameMaxLength, entry.ActorName.Length);
    }
}

public sealed class NotificationTests
{
    private static readonly DateTime Created = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ReadAt = new(2026, 11, 9, 10, 0, 0, DateTimeKind.Utc);

    private static Notification New(int? recipient = 5) =>
        Notification.Create(recipient, " Leave request ", "A request needs your attention.", "event_available", " /leave/approvals ", Created).Value;

    [Fact]
    public void A_new_notification_is_unread_and_trimmed()
    {
        var notification = New();

        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
        Assert.Equal("Leave request", notification.Title);
        Assert.Equal("/leave/approvals", notification.Link);
    }

    [Fact]
    public void A_blank_link_is_stored_as_null()
    {
        var notification = Notification.Create(5, "T", "M", "icon", "  ", Created).Value;

        Assert.Null(notification.Link);
    }

    [Fact]
    public void Title_message_and_icon_are_required_and_limited()
    {
        var result = Notification.Create(5, " ", new string('m', 501), "", new string('l', 301), Created);

        Assert.True(result.IsFailure);
        Assert.Equal(["icon", "link", "message", "title"], result.Error.FieldErrors!.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_user_sees_their_own_notification_and_broadcasts_but_not_another_users()
    {
        var mine = New(recipient: 5);
        var theirs = New(recipient: 6);
        var broadcast = New(recipient: null);

        Assert.True(mine.IsVisibleTo(5));
        Assert.False(theirs.IsVisibleTo(5));
        Assert.True(broadcast.IsVisibleTo(5));
        Assert.True(broadcast.IsVisibleTo(6));
        Assert.True(broadcast.IsBroadcast);
        Assert.False(mine.IsBroadcast);
    }

    [Fact]
    public void Marking_read_sets_the_flag_and_time_and_a_second_read_keeps_the_first_time()
    {
        var notification = New();

        notification.MarkRead(ReadAt);
        notification.MarkRead(ReadAt.AddHours(5));

        Assert.True(notification.IsRead);
        Assert.Equal(ReadAt, notification.ReadAtUtc);
    }
}
