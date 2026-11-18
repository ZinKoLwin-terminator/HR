using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Notifications;
using Apex.Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apex.Hrms.Infrastructure.Tests;

/// <summary>
/// The notification writer only stages rows (the caller's unit of work saves them), one per distinct recipient, never a broadcast;
/// the recipient queries are checked as SQL text. Nothing here opens a connection: the recipient queries themselves run in
/// Apex.Hrms.IntegrationTests.
/// </summary>
public sealed class NotificationWriterTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private static readonly NotificationContent Content = new("Grievance update", "GRV-2026-00001: a new message.", "gavel", "/grievances/my/1");

    private readonly HrmsDbContext context = new(
        new DbContextOptionsBuilder<HrmsDbContext>()
            .UseSqlServer("Server=.;Database=HR;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options);

    public void Dispose() => context.Dispose();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;

        public DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime);

        public TimeOnly TimeNow => TimeOnly.FromDateTime(Now.UtcDateTime);
    }

    private List<Notification> Staged() =>
        [.. context.ChangeTracker.Entries<Notification>().Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity)];

    [Fact]
    public void One_unread_notification_is_staged_per_distinct_recipient()
    {
        var writer = new EfNotificationWriter(context, new FixedClock());

        var result = writer.Add([7, 9, 7], Content);

        Assert.True(result.IsSuccess);
        var staged = Staged();
        Assert.Equal([7, 9], staged.Select(notification => notification.RecipientUserId!.Value).Order());
        Assert.All(staged, notification =>
        {
            Assert.False(notification.IsBroadcast);
            Assert.False(notification.IsRead);
            Assert.Equal(Now.UtcDateTime, notification.CreatedAtUtc);
            Assert.Equal(Content.Title, notification.Title);
            Assert.Equal(Content.Message, notification.Message);
            Assert.Equal(Content.Icon, notification.Icon);
            Assert.Equal(Content.Link, notification.Link);
        });
    }

    [Fact]
    public void No_recipient_stages_nothing()
    {
        Assert.True(new EfNotificationWriter(context, new FixedClock()).Add([], Content).IsSuccess);

        Assert.Empty(Staged());
    }

    [Fact]
    public void Content_that_breaks_the_column_limits_is_refused_and_nothing_is_staged()
    {
        var tooLong = Content with { Title = new string('x', Notification.TitleMaxLength + 1) };

        var result = new EfNotificationWriter(context, new FixedClock()).Add([7, 9], tooLong);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(Staged());
    }

    [Fact]
    public void The_holders_query_filters_on_the_grant_the_account_state_and_the_excluded_employees()
    {
        var sql = new EfNotificationRecipients(context).HoldersQuery("grievance-review", "Approve", [5, 6]).ToQueryString();

        Assert.Contains("[sec].[RolePermission]", sql, StringComparison.Ordinal);
        Assert.Contains("[sec].[AppUser]", sql, StringComparison.Ordinal);
        Assert.Contains("[sec].[Role]", sql, StringComparison.Ordinal);
        Assert.Contains("[ExitDate]", sql, StringComparison.Ordinal);
        Assert.Contains("[Status]", sql, StringComparison.Ordinal);
        Assert.Contains("NOT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void The_employee_account_query_needs_an_account_that_can_sign_in()
    {
        var sql = new EfNotificationRecipients(context).EmployeeAccountQuery(5).ToQueryString();

        Assert.Contains("[sec].[AppUser]", sql, StringComparison.Ordinal);
        Assert.Contains("[sec].[Role]", sql, StringComparison.Ordinal);
        Assert.Contains("[ExitDate]", sql, StringComparison.Ordinal);
    }
}
