using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.Notifications;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The dashboard, activity and notification routes over HTTP: the JSON shapes the frontend adapters will read, the
/// sign-in requirement, the error mapping and the absence of any route that changes the log.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ActivityHttpTests : DatabaseTest, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public ActivityHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        client = factory.CreateBrowserClient(out _);
    }

    public override Task InitializeAsync() => client.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        client.Dispose();
        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private async Task<long> InsertNotificationAsync(int? recipientUserId, string suffix) =>
        await Sql.ScalarAsync<long>(
            """
            INSERT INTO app.Notification (RecipientUserId, Title, Message, Icon, Link)
            OUTPUT INSERTED.Id
            VALUES (@recipient, @title, N'A fictional notification.', 'event_available', N'/leave/approvals');
            """,
            ("@recipient", recipientUserId),
            ("@title", $"{Tag} {suffix}"));

    // ---- Dashboard ----

    [IntegrationFact]
    public async Task The_summary_has_the_shape_of_the_frontend_model_and_counts_the_rows_of_the_test()
    {
        var org = await SeedOrganizationAsync();
        await SeedEmployeeAsync(org);

        var response = await client.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadAsync(response);
        foreach (var name in new[]
        {
            "generatedAt", "headcount", "attendanceToday", "pendingLeaveApprovals", "pendingLeaves", "payroll", "recruitment",
            "departmentDistribution", "attendanceTrend", "headcountGrowth", "recentActivities", "upcomingEvents",
        })
        {
            Assert.True(json.TryGetProperty(name, out _), $"Missing property '{name}'.");
        }

        var headcount = json.GetProperty("headcount");
        foreach (var name in new[] { "total", "active", "probation", "newThisMonth", "onLeaveToday", "exitsThisMonth" })
        {
            Assert.Equal(JsonValueKind.Number, headcount.GetProperty(name).ValueKind);
        }

        Assert.True(headcount.GetProperty("total").GetInt32() >= 1);
        var attendance = json.GetProperty("attendanceToday");
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", attendance.GetProperty("date").GetString());
        foreach (var name in new[] { "scheduled", "present", "late", "absent", "onLeave", "halfDay", "attendanceRate" })
        {
            Assert.Equal(JsonValueKind.Number, attendance.GetProperty(name).ValueKind);
        }

        var recruitment = json.GetProperty("recruitment");
        Assert.Equal(["Applied", "Screening", "Interview", "Selected"], recruitment.GetProperty("funnel").EnumerateArray().Select(f => f.GetProperty("label").GetString()));
        foreach (var name in new[] { "openJobs", "openPositions", "activeCandidates", "upcomingInterviews" })
        {
            Assert.Equal(JsonValueKind.Number, recruitment.GetProperty(name).ValueKind);
        }

        Assert.Equal(12, json.GetProperty("headcountGrowth").GetArrayLength());
        Assert.Contains(
            json.GetProperty("departmentDistribution").EnumerateArray(),
            item => item.GetProperty("label").GetString() == org.Department.Name && item.GetProperty("count").GetInt32() == 1);
        Assert.True(json.GetProperty("payroll").ValueKind is JsonValueKind.Null or JsonValueKind.Object);
        Assert.True(json.GetProperty("recentActivities").GetArrayLength() <= 6);
    }

    [IntegrationFact]
    public async Task The_summary_needs_a_signed_in_user()
    {
        using var anonymous = factory.CreateBrowserClient(out _);

        var response = await anonymous.GetAsync("/api/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Activity ----

    [IntegrationFact]
    public async Task Creating_a_record_over_http_appears_in_the_feed_and_by_id()
    {
        var created = await client.PostAsJsonAsync("/api/companies", Requests.Company("A", "Test Company A"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var page = await client.GetFromJsonAsync<PagedResult<ActivityEntryDto>>($"/api/activity/query?search={Tag}&category=organization&pageSize=5");

        var entry = Assert.Single(page!.Items);
        Assert.Equal($"Company \"{Name("Test Company A")}\" created", entry.Message);
        Assert.Equal("organization", entry.Category);
        var json = await ReadAsync(await client.GetAsync($"/api/activity/{entry.Id}"));
        Assert.Equal(entry.Message, json.GetProperty("message").GetString());
        Assert.Contains("Test Person", json.GetProperty("actor").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.String, json.GetProperty("timestamp").ValueKind);
        Assert.Equal(JsonValueKind.Number, json.GetProperty("id").ValueKind);
    }

    [IntegrationFact]
    public async Task A_missing_entry_is_404_and_a_bad_filter_is_400_with_the_field()
    {
        var missing = await client.GetAsync($"/api/activity/{long.MaxValue}");
        var bad = await client.GetAsync("/api/activity/query?category=payslips");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await ReadAsync(bad)).GetProperty("errors").TryGetProperty("category", out _));
    }

    [IntegrationFact]
    public async Task The_log_cannot_be_written_changed_or_deleted_over_http()
    {
        var entry = new { category = "system", message = "Forged", actor = "Nobody" };
        await client.PostAsJsonAsync("/api/companies", Requests.Company("A", "Test Company A"));
        var page = await client.GetFromJsonAsync<PagedResult<ActivityEntryDto>>($"/api/activity/query?search={Tag}");
        var id = Assert.Single(page!.Items).Id;

        var post = await client.PostAsJsonAsync("/api/activity", entry);
        var put = await client.PutAsJsonAsync($"/api/activity/{id}", entry);
        var delete = await client.DeleteAsync($"/api/activity/{id}");

        Assert.All([post, put, delete], response => Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, response.StatusCode.ToString()));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Id = @id AND Message NOT LIKE N'Forged'", ("@id", id)));
    }

    // ---- Notifications ----

    [IntegrationFact]
    public async Task Notifications_over_http_show_mine_and_broadcasts_and_hide_another_users()
    {
        var other = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "other"));
        await InsertNotificationAsync(TestAdministrator.AccountId, "mine");
        await InsertNotificationAsync(other.AccountId, "theirs");
        await InsertNotificationAsync(null, "broadcast");

        var page = await client.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/notifications/query?pageSize=200");

        var titles = page!.Items.Where(n => n.Title.StartsWith(Tag, StringComparison.Ordinal)).Select(n => n.Title).Order(StringComparer.Ordinal);
        Assert.Equal([$"{Tag} broadcast", $"{Tag} mine"], titles);
        var json = await ReadAsync(await client.GetAsync("/api/notifications/query?pageSize=200"));
        var first = json.GetProperty("items").EnumerateArray().First(n => n.GetProperty("title").GetString()!.StartsWith(Tag, StringComparison.Ordinal));
        foreach (var name in new[] { "id", "title", "message", "icon", "link", "timestamp", "read" })
        {
            Assert.True(first.TryGetProperty(name, out _), $"Missing property '{name}'.");
        }
    }

    [IntegrationFact]
    public async Task Marking_read_over_http_returns_the_notification_and_another_users_is_404()
    {
        var other = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "other"));
        var mine = await InsertNotificationAsync(TestAdministrator.AccountId, "mine");
        var theirs = await InsertNotificationAsync(other.AccountId, "theirs");

        var read = await client.PostAsync($"/api/notifications/{mine}/read", content: null);
        var refused = await client.PostAsync($"/api/notifications/{theirs}/read", content: null);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.True((await ReadAsync(read)).GetProperty("read").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.False(await Sql.ScalarAsync<bool>("SELECT IsRead FROM app.Notification WHERE Id = @id", ("@id", theirs)));
    }

    [IntegrationFact]
    public async Task Read_all_over_http_is_204_and_notifications_need_a_signed_in_user()
    {
        var mine = await InsertNotificationAsync(TestAdministrator.AccountId, "mine");
        using var anonymous = factory.CreateBrowserClient(out _);

        var readAll = await client.PostAsync("/api/notifications/read-all", content: null);
        var anonymousList = await anonymous.GetAsync("/api/notifications/query");

        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
        Assert.True(await Sql.ScalarAsync<bool>("SELECT IsRead FROM app.Notification WHERE Id = @id", ("@id", mine)));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);
    }
}
