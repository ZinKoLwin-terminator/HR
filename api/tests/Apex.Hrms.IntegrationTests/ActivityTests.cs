using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Attendance.Approvals;
using Apex.Hrms.Application.Attendance.Overtime;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The activity log and the notifications against SQL Server, through the handlers. Activity rows are written in the same
/// save as the change (and removed again by the tag of the test: its actor name, the names and codes in the messages);
/// notification rows are written with plain SQL (nothing in the API creates one yet) and tagged by title.
/// The clock is 08:00 UTC on Monday 2026-11-09, which is 14:30 on that day in the business time zone.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ActivityTests : DatabaseTest
{
    private static readonly DateTime FixedNowUtc = new(2026, 11, 9, 8, 0, 0, 123, DateTimeKind.Utc);

    private static PageRequest<ActivityFilter> Page(ActivityFilter? filter = null, string? search = null) =>
        new(0, 50, null, search, filter ?? new ActivityFilter());

    private Task<Result<PagedResult<ActivityEntryDto>>> QueryActivity(ActivityFilter? filter = null) =>
        InScopeAsync(sp => sp.GetRequiredService<IActivityQueryHandler>().HandleAsync(Page(filter, search: Tag), default));

    private async Task<int> CountRowsAsync(string whereMessageLike) =>
        await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM app.ActivityLog WHERE ActorName LIKE @tag AND Message LIKE @message",
            ("@tag", Tag + "%"),
            ("@message", whereMessageLike));

    // ---- Writing: the same save as the change ----

    [IntegrationFact]
    public async Task Creating_an_organization_record_writes_the_activity_row_with_actor_category_and_time()
    {
        var company = await SeedCompanyAsync();

        var row = await ReadSingleRowAsync($"Company \"{company.Name}\" created");

        Assert.Equal("organization", row.Category);
        Assert.Equal($"{Tag} Test User", row.ActorName);
        Assert.Equal(TestAdministrator.AccountId, row.ActorUserId);
        Assert.Equal(FixedNowUtc, row.OccurredAtUtc);
    }

    [IntegrationFact]
    public async Task Update_status_change_and_delete_each_write_a_row()
    {
        var company = await SeedCompanyAsync();
        var inactive = Succeeded(await Deactivate<CompanyDto>(company.Id, Requests.Version(company.RowVersion)));
        Succeeded(await Activate<CompanyDto>(company.Id, Requests.Version(inactive.RowVersion)));

        Assert.Equal(2, await CountRowsAsync($"Company \"{company.Name}\" updated"));

        var current = Succeeded(await Get<CompanyDto>(company.Id));
        Assert.True((await Delete<CompanyDto>(company.Id, Requests.Version(current.RowVersion))).IsSuccess);
        Assert.Equal(1, await CountRowsAsync($"Company \"{company.Name}\" deleted"));
    }

    [IntegrationFact]
    public async Task A_save_that_fails_leaves_no_activity_row()
    {
        BeforeNextSave = () => RawRows.CompanyAsync(Code("A"), Name("Raw Winner Company"));

        var result = await Create(Requests.Company("A", "Test Company A"));

        FailedOnField(result, "code");
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Message LIKE @message", ("@message", $"%{Tag}%")));
    }

    [IntegrationFact]
    public async Task An_employee_entry_holds_the_code_and_neither_the_name_nor_the_national_id()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);

        var row = await ReadSingleRowAsync($"Employee \"{employee.EmployeeCode}\" created");

        Assert.Equal("employee", row.Category);
        var personal = await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM app.ActivityLog WHERE ActorName LIKE @tag AND (Message LIKE '%Mia%' OR Message LIKE '%Testwell%' OR Message LIKE '%NRC%')",
            ("@tag", Tag + "%"));
        Assert.Equal(0, personal);
    }

    [IntegrationFact]
    public async Task An_overtime_request_logs_the_id_the_database_assigned_and_the_row_and_the_entry_are_saved_together()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var created = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<OvertimeCreateRequest, OvertimeDto>>()
            .HandleAsync(new OvertimeCreateRequest(employee.Id, new DateOnly(2026, 11, 9), new TimeOnly(18, 0), new TimeOnly(20, 0), "Month end close"), default)));

        var row = await ReadSingleRowAsync($"Overtime entry \"OT-{created.Id:0000}\" created");
        Assert.Equal("attendance", row.Category);

        Succeeded(await RunCommand<ApproveOvertimeCommand, OvertimeDto>(new ApproveOvertimeCommand(created.Id, Requests.Version(created.RowVersion), null)));
        Assert.Equal(1, await CountRowsAsync($"OT-{created.Id:0000} approved"));
    }

    // ---- Reading ----

    [IntegrationFact]
    public async Task The_feed_returns_the_entries_of_the_test_with_filters_and_by_id()
    {
        var company = await SeedCompanyAsync();

        var all = Succeeded(await QueryActivity());
        var byCategory = Succeeded(await QueryActivity(new ActivityFilter(Category: "organization", Actor: Tag)));
        var otherCategory = Succeeded(await QueryActivity(new ActivityFilter(Category: "leave")));

        var entry = Assert.Single(all.Items);
        Assert.Equal($"Company \"{company.Name}\" created", entry.Message);
        Assert.Equal(entry.Id, Assert.Single(byCategory.Items).Id);
        Assert.Empty(otherCategory.Items);
        var found = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IActivityGetHandler>().HandleAsync(entry.Id, default)));
        Assert.Equal(entry.Message, found.Message);
        Assert.Equal($"{Tag} Test User", found.Actor);
        Failed(await InScopeAsync(sp => sp.GetRequiredService<IActivityGetHandler>().HandleAsync(long.MaxValue, default)), ErrorType.NotFound);
    }

    [IntegrationFact]
    public async Task The_date_filter_follows_the_business_day()
    {
        await SeedCompanyAsync();

        var onTheDay = Succeeded(await QueryActivity(new ActivityFilter(DateFrom: new DateOnly(2026, 11, 9), DateTo: new DateOnly(2026, 11, 9))));
        var theDayBefore = Succeeded(await QueryActivity(new ActivityFilter(DateTo: new DateOnly(2026, 11, 8))));
        var theDayAfter = Succeeded(await QueryActivity(new ActivityFilter(DateFrom: new DateOnly(2026, 11, 10))));

        Assert.Single(onTheDay.Items);
        Assert.Empty(theDayBefore.Items);
        Assert.Empty(theDayAfter.Items);
    }

    // ---- Notifications ----

    private async Task<long> InsertNotificationAsync(int? recipientUserId, string suffix) =>
        await Sql.ScalarAsync<long>(
            """
            INSERT INTO app.Notification (RecipientUserId, Title, Message, Icon, Link)
            OUTPUT INSERTED.Id
            VALUES (@recipient, @title, N'A fictional notification.', 'event_available', N'/leave/approvals');
            """,
            ("@recipient", recipientUserId),
            ("@title", $"{Tag} {suffix}"));

    private async Task<IReadOnlyList<NotificationDto>> MyNotificationsAsync() =>
        Succeeded(await InScopeAsync(sp => sp.GetRequiredService<INotificationQueryHandler>()
            .HandleAsync(new PageRequest<NotificationFilter>(0, 200, null, null, new NotificationFilter()), default)))
            .Items.Where(n => n.Title.StartsWith(Tag, StringComparison.Ordinal)).ToList();

    [IntegrationFact]
    public async Task A_notification_is_visible_to_its_recipient_and_to_everyone_when_broadcast_but_not_to_another_user()
    {
        var other = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "other"));
        await InsertNotificationAsync(TestAdministrator.AccountId, "mine");
        await InsertNotificationAsync(other.AccountId, "theirs");
        await InsertNotificationAsync(null, "broadcast");

        var titles = (await MyNotificationsAsync()).Select(n => n.Title).Order(StringComparer.Ordinal).ToList();

        Assert.Equal([$"{Tag} broadcast", $"{Tag} mine"], titles);
    }

    [IntegrationFact]
    public async Task The_other_user_sees_their_own_row_and_the_broadcast()
    {
        var other = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "other"));
        await InsertNotificationAsync(TestAdministrator.AccountId, "mine");
        await InsertNotificationAsync(other.AccountId, "theirs");
        await InsertNotificationAsync(null, "broadcast");
        Customize(services => services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(other.AccountId, $"{Tag} Other User")));

        var titles = (await MyNotificationsAsync()).Select(n => n.Title).Order(StringComparer.Ordinal).ToList();

        Assert.Equal([$"{Tag} broadcast", $"{Tag} theirs"], titles);
    }

    [IntegrationFact]
    public async Task Reading_marks_the_row_read_with_the_time_and_another_users_row_is_not_found_and_stays_unread()
    {
        var other = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "other"));
        var mine = await InsertNotificationAsync(TestAdministrator.AccountId, "mine");
        var theirs = await InsertNotificationAsync(other.AccountId, "theirs");

        var read = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IMarkNotificationReadHandler>().HandleAsync(mine, default)));
        var refused = await InScopeAsync(sp => sp.GetRequiredService<IMarkNotificationReadHandler>().HandleAsync(theirs, default));

        Assert.True(read.Read);
        Assert.Equal(FixedNowUtc, await Sql.ScalarAsync<DateTime>("SELECT ReadAtUtc FROM app.Notification WHERE Id = @id", ("@id", mine)));
        Failed(refused, ErrorType.NotFound);
        Assert.False(await Sql.ScalarAsync<bool>("SELECT IsRead FROM app.Notification WHERE Id = @id", ("@id", theirs)));
    }

    [IntegrationFact]
    public async Task Read_all_marks_my_rows_and_the_broadcasts_and_leaves_another_users_rows_alone()
    {
        var other = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "other"));
        var mine = await InsertNotificationAsync(TestAdministrator.AccountId, "mine");
        var broadcast = await InsertNotificationAsync(null, "broadcast");
        var theirs = await InsertNotificationAsync(other.AccountId, "theirs");

        var result = await InScopeAsync(sp => sp.GetRequiredService<IMarkAllNotificationsReadHandler>().HandleAsync(default));

        Assert.True(result.IsSuccess);
        Assert.True(await Sql.ScalarAsync<bool>("SELECT IsRead FROM app.Notification WHERE Id = @id", ("@id", mine)));
        Assert.True(await Sql.ScalarAsync<bool>("SELECT IsRead FROM app.Notification WHERE Id = @id", ("@id", broadcast)));
        Assert.False(await Sql.ScalarAsync<bool>("SELECT IsRead FROM app.Notification WHERE Id = @id", ("@id", theirs)));
    }

    // ---- Helpers ----

    private sealed record LogRow(string Category, string ActorName, int? ActorUserId, DateTime OccurredAtUtc);

    private async Task<LogRow> ReadSingleRowAsync(string message)
    {
        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(SqlServerEnvironment.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Microsoft.Data.SqlClient.SqlCommand(
            "SELECT Category, ActorName, ActorUserId, OccurredAtUtc FROM app.ActivityLog WHERE ActorName LIKE @tag AND Message = @message",
            connection);
        command.Parameters.AddWithValue("@tag", Tag + "%");
        command.Parameters.AddWithValue("@message", message);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"No activity row with the message '{message}'.");
        var row = new LogRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.GetDateTime(3));
        Assert.False(await reader.ReadAsync(), $"More than one activity row with the message '{message}'.");
        return row;
    }
}
