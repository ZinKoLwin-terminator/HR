using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Leave.Applications;
using Apex.Hrms.Application.Leave.Balances;
using Apex.Hrms.Application.Leave.Calendar;
using Apex.Hrms.Application.Leave.LeavePolicies;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The leave routes over HTTP: routing, JSON bodies and shapes, the command bodies, the error mapping and the
/// own-approval refusal for a signed-in employee. The host uses the real clock, so the leave dates are computed from
/// today: a Monday several weeks ahead.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class LeaveHttpTests : DatabaseTest, IDisposable
{
    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public LeaveHttpTests()
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

    /// <summary>A Monday, <paramref name="weeksAhead"/> weeks from today or a little later.</summary>
    private static DateOnly Monday(int weeksAhead = 3)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7 * weeksAhead);
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private async Task<T> CreateAsync<T>(string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string url, object body) => http.PostAsJsonAsync(url, body);

    private async Task<LeaveApplicationDto> CreateSubmittedAsync(HttpClient http, int employeeId, int typeId, DateOnly start, DateOnly? end = null)
    {
        var draft = await CreateAsync<LeaveApplicationDto>("/api/leave-applications", Requests.LeaveApplication(employeeId, typeId, start, end));
        var response = await PostAsync(http, $"/api/leave-applications/{draft.Id}/submit", new { rowVersion = Requests.Version(draft.RowVersion) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LeaveApplicationDto>())!;
    }

    [IntegrationFact]
    public async Task Types_and_policies_have_the_organization_routes_and_shapes()
    {
        var created = await client.PostAsJsonAsync("/api/leave-types", Requests.LeaveType());
        var type = (await created.Content.ReadFromJsonAsync<LeaveTypeDto>())!;
        var policy = await CreateAsync<LeavePolicyDto>("/api/leave-policies", Requests.LeavePolicy(type.Id, employmentType: "Contract"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/leave-types/{type.Id}", created.Headers.Location?.OriginalString);
        var json = await ReadAsync(await client.GetAsync($"/api/leave-types/{type.Id}"));
        Assert.Equal("#3f6fd8", json.GetProperty("color").GetString());
        Assert.Equal(15m, json.GetProperty("defaultDaysPerYear").GetDecimal());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("eligibleGender").ValueKind);
        Assert.Equal(8, type.RowVersion.Length);

        var query = await client.GetFromJsonAsync<PagedResult<LeaveTypeDto>>($"/api/leave-types/query?search={Tag}&sort=code:desc&status=Active");
        Assert.Equal(type.Id, Assert.Single(query!.Items).Id);
        var policies = await client.GetFromJsonAsync<PagedResult<LeavePolicyDto>>($"/api/leave-policies/query?leaveTypeId={type.Id}&employmentType=Contract");
        Assert.Equal(policy.Id, Assert.Single(policies!.Items).Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/leave-types")).StatusCode);

        var deactivated = await PostAsync(client, $"/api/leave-policies/{policy.Id}/deactivate", new { rowVersion = Requests.Version(policy.RowVersion) });
        Assert.Equal("Inactive", (await ReadAsync(deactivated)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task A_duplicate_active_policy_and_a_bad_type_are_400s_with_field_errors()
    {
        var type = await CreateAsync<LeaveTypeDto>("/api/leave-types", Requests.LeaveType());
        await CreateAsync<LeavePolicyDto>("/api/leave-policies", Requests.LeavePolicy(type.Id, "One"));

        var clash = await client.PostAsJsonAsync("/api/leave-policies", Requests.LeavePolicy(type.Id, "Two"));
        var badType = await client.PostAsJsonAsync("/api/leave-types", Requests.LeaveType("BAD", maxConsecutive: 0));

        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        var clashProblem = await ReadAsync(clash);
        Assert.Equal("Validation", clashProblem.GetProperty("code").GetString());
        Assert.Equal("Another active policy covers the same group.", clashProblem.GetProperty("errors").GetProperty("leaveTypeId")[0].GetString());
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.True((await ReadAsync(badType)).GetProperty("errors").TryGetProperty("maxConsecutiveDays", out _));
    }

    [IntegrationFact]
    public async Task An_application_goes_through_submit_and_approve_with_the_command_bodies()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var start = Monday();

        var draft = await CreateAsync<LeaveApplicationDto>("/api/leave-applications", Requests.LeaveApplication(employee.Id, type.Id, start, start.AddDays(4)));
        Assert.Equal("Draft", draft.Status);
        Assert.Equal(5m, draft.Days);
        Assert.StartsWith("LV-", draft.ApplicationNo, StringComparison.Ordinal);

        var submitted = await PostAsync(client, $"/api/leave-applications/{draft.Id}/submit", new { rowVersion = Requests.Version(draft.RowVersion) });
        var submittedDto = (await submitted.Content.ReadFromJsonAsync<LeaveApplicationDto>())!;
        var approved = await PostAsync(client, $"/api/leave-applications/{draft.Id}/approve", new { rowVersion = Requests.Version(submittedDto.RowVersion), remarks = "Enjoy" });
        var json = await ReadAsync(approved);

        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        Assert.Equal("Submitted", submittedDto.Status);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Approved", json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("approverId").ValueKind);
        Assert.Equal("TST- Test Person", json.GetProperty("actionedByName").GetString());
        Assert.Equal("Enjoy", json.GetProperty("approverRemarks").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", json.GetProperty("actionedOn").GetString());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", json.GetProperty("startDate").GetString());
        Assert.NotEqual(submittedDto.RowVersion, json.GetProperty("rowVersion").GetBytesFromBase64());
    }

    [IntegrationFact]
    public async Task A_rejection_without_a_reason_is_a_400_on_remarks_and_with_one_it_is_a_200()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var submitted = await CreateSubmittedAsync(client, employee.Id, type.Id, Monday());

        var refused = await PostAsync(client, $"/api/leave-applications/{submitted.Id}/reject", new { rowVersion = Requests.Version(submitted.RowVersion), remarks = " " });
        var rejected = await PostAsync(client, $"/api/leave-applications/{submitted.Id}/reject", new { rowVersion = Requests.Version(submitted.RowVersion), remarks = "Team is short" });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("A reason is required.", (await ReadAsync(refused)).GetProperty("errors").GetProperty("remarks")[0].GetString());
        Assert.Equal("Rejected", (await ReadAsync(rejected)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task A_signed_in_employee_cannot_approve_or_reject_their_own_leave_but_the_administrator_can()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "self"), employeeId: employee.Id);
        using var own = factory.CreateBrowserClient(out _);
        await own.SignInAsync(account);

        var submitted = await CreateSubmittedAsync(own, employee.Id, type.Id, Monday());
        var approve = await PostAsync(own, $"/api/leave-applications/{submitted.Id}/approve", new { rowVersion = Requests.Version(submitted.RowVersion) });
        var reject = await PostAsync(own, $"/api/leave-applications/{submitted.Id}/reject", new { rowVersion = Requests.Version(submitted.RowVersion), remarks = "No" });
        var byAdministrator = await PostAsync(client, $"/api/leave-applications/{submitted.Id}/approve", new { rowVersion = Requests.Version(submitted.RowVersion) });

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        var problem = await ReadAsync(approve);
        Assert.Equal("Conflict", problem.GetProperty("code").GetString());
        Assert.Equal("You cannot decide your own leave request.", problem.GetProperty("detail").GetString() ?? problem.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Conflict, reject.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byAdministrator.StatusCode);
        Assert.Equal("Approved", (await ReadAsync(byAdministrator)).GetProperty("status").GetString());
        Assert.Equal("Approved", await Sql.ScalarAsync<string>("SELECT Status FROM lve.LeaveApplication WHERE Id = @id", ("@id", submitted.Id)));
    }

    [IntegrationFact]
    public async Task A_colleague_with_an_employee_account_can_approve_and_is_the_approver()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org, "1", "mia");
        var manager = await SeedEmployeeAsync(org, "2", "ben");
        var type = await SeedLeaveTypeAsync();
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "manager"), employeeId: manager.Id);
        using var managerClient = factory.CreateBrowserClient(out _);
        await managerClient.SignInAsync(account);
        var submitted = await CreateSubmittedAsync(client, employee.Id, type.Id, Monday());

        var approved = await PostAsync(managerClient, $"/api/leave-applications/{submitted.Id}/approve", new { rowVersion = Requests.Version(submitted.RowVersion) });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(manager.Id, (await ReadAsync(approved)).GetProperty("approverId").GetInt32());
    }

    [IntegrationFact]
    public async Task Update_delete_and_cancel_follow_the_rules_with_the_error_codes_of_the_frontend()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var start = Monday();
        var draft = await CreateAsync<LeaveApplicationDto>("/api/leave-applications", Requests.LeaveApplication(employee.Id, type.Id, start));

        var updated = await client.PutAsJsonAsync($"/api/leave-applications/{draft.Id}", Requests.LeaveApplicationUpdate(draft, start.AddDays(1)));
        var stale = await client.PutAsJsonAsync($"/api/leave-applications/{draft.Id}", Requests.LeaveApplicationUpdate(draft, start.AddDays(2)));
        var missingVersion = await client.PutAsJsonAsync($"/api/leave-applications/{draft.Id}", Requests.LeaveApplicationUpdate(draft, start) with { RowVersion = null });
        var cancelDraft = await PostAsync(client, $"/api/leave-applications/{draft.Id}/cancel", new { rowVersion = Requests.Version((await updated.Content.ReadFromJsonAsync<LeaveApplicationDto>())!.RowVersion) });
        var notFound = await PostAsync(client, "/api/leave-applications/2000000/submit", new { rowVersion = Requests.Version(draft.RowVersion) });
        var deleteWithoutBody = await client.DeleteAsync($"/api/leave-applications/{draft.Id}");

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, cancelDraft.StatusCode);
        Assert.Equal("Conflict", (await ReadAsync(cancelDraft)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, deleteWithoutBody.StatusCode);

        var current = (await client.GetFromJsonAsync<LeaveApplicationDto>($"/api/leave-applications/{draft.Id}"))!;
        var deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/leave-applications/{draft.Id}")
        {
            Content = JsonContent.Create(new { rowVersion = Requests.Version(current.RowVersion) }),
        });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/leave-applications/{draft.Id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task There_is_no_unpaged_application_list_and_the_query_pages_and_filters()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var start = Monday();
        var first = await CreateSubmittedAsync(client, employee.Id, type.Id, start);
        await CreateAsync<LeaveApplicationDto>("/api/leave-applications", Requests.LeaveApplication(employee.Id, type.Id, start.AddDays(7)));

        var unpaged = await client.GetAsync("/api/leave-applications");
        var submitted = await client.GetFromJsonAsync<PagedResult<LeaveApplicationDto>>($"/api/leave-applications/query?employeeId={employee.Id}&status=Submitted&pageSize=5");
        var overlap = await client.GetFromJsonAsync<PagedResult<LeaveApplicationDto>>(
            $"/api/leave-applications/query?employeeId={employee.Id}&dateFrom={start.AddDays(5):yyyy-MM-dd}&dateTo={start.AddDays(8):yyyy-MM-dd}&sort=startDate:desc");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, unpaged.StatusCode);
        Assert.Equal(first.Id, Assert.Single(submitted!.Items).Id);
        Assert.Equal(1, overlap!.Total);
        Assert.Equal(start.AddDays(7), overlap.Items[0].StartDate);
    }

    [IntegrationFact]
    public async Task The_balance_endpoint_returns_entitled_taken_pending_and_available()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var start = Monday();
        var approved = await CreateSubmittedAsync(client, employee.Id, type.Id, start, start.AddDays(2));
        (await PostAsync(client, $"/api/leave-applications/{approved.Id}/approve", new { rowVersion = Requests.Version(approved.RowVersion) })).EnsureSuccessStatusCode();
        await CreateSubmittedAsync(client, employee.Id, type.Id, start.AddDays(7), start.AddDays(8));

        var response = await client.GetAsync($"/api/leave/balances?employeeId={employee.Id}&year={start.Year}");
        var lines = (await response.Content.ReadFromJsonAsync<List<LeaveBalanceDto>>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var line = lines.Single(candidate => candidate.LeaveTypeId == type.Id);
        Assert.Equal((15m, 3m, 2m, 10m), (line.Entitled, line.Taken, line.Pending, line.Available));
        var raw = await ReadAsync(await client.GetAsync($"/api/leave/balances?employeeId={employee.Id}&year={start.Year}"));
        var names = raw.EnumerateArray().First(element => element.GetProperty("leaveTypeId").GetInt32() == type.Id).EnumerateObject().Select(property => property.Name).ToList();
        Assert.Equal(["leaveTypeId", "code", "name", "color", "entitled", "taken", "pending", "available"], names);
    }

    [IntegrationFact]
    public async Task The_balance_endpoint_maps_its_errors()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);

        var missing = await client.GetAsync("/api/leave/balances");
        var unknown = await client.GetAsync("/api/leave/balances?employeeId=2000000");
        var badYear = await client.GetAsync($"/api/leave/balances?employeeId={employee.Id}&year=1800");
        var notANumber = await client.GetAsync($"/api/leave/balances?employeeId={employee.Id}&year=abc");

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badYear.StatusCode);
        Assert.True((await ReadAsync(badYear)).GetProperty("errors").TryGetProperty("year", out _));
        Assert.Equal(HttpStatusCode.BadRequest, notANumber.StatusCode);
    }

    [IntegrationFact]
    public async Task The_calendar_endpoint_returns_the_days_of_the_range_with_leave_entries()
    {
        var org = await SeedOrganizationAsync();
        var employee = await SeedEmployeeAsync(org);
        var type = await SeedLeaveTypeAsync();
        var start = Monday();
        var submitted = await CreateSubmittedAsync(client, employee.Id, type.Id, start, start.AddDays(1));

        var response = await client.GetAsync($"/api/leave/calendar?from={start.AddDays(-2):yyyy-MM-dd}&to={start.AddDays(4):yyyy-MM-dd}&leaveTypeId={type.Id}");
        var calendar = (await response.Content.ReadFromJsonAsync<LeaveCalendarDto>())!;
        var withoutPending = (await client.GetFromJsonAsync<LeaveCalendarDto>(
            $"/api/leave/calendar?from={start:yyyy-MM-dd}&to={start:yyyy-MM-dd}&includePending=false"))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, calendar.Days.Count);
        Assert.True(calendar.Days[0].IsWeekend);
        Assert.Equal(start, calendar.Days[2].Date);
        var entry = Assert.Single(calendar.Days[2].Entries);
        Assert.Equal(submitted.Id, entry.ApplicationId);
        Assert.Equal("Submitted", entry.Status);
        Assert.Equal("Mia Testwell", entry.EmployeeName);
        Assert.Empty(calendar.Days[4].Entries);
        Assert.DoesNotContain(withoutPending.Days.Single().Entries, candidate => candidate.ApplicationId == submitted.Id);
    }

    [IntegrationFact]
    public async Task The_calendar_endpoint_maps_its_errors()
    {
        var reversed = await client.GetAsync("/api/leave/calendar?from=2026-12-02&to=2026-12-01");
        var missing = await client.GetAsync("/api/leave/calendar?from=2026-12-02");
        var garbage = await client.GetAsync("/api/leave/calendar?from=yesterday&to=tomorrow");
        var tooLong = await client.GetAsync("/api/leave/calendar?from=2026-01-01&to=2027-06-01");

        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        Assert.True((await ReadAsync(reversed)).GetProperty("errors").TryGetProperty("to", out _));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [IntegrationFact]
    public async Task Every_leave_route_needs_a_signed_in_user()
    {
        using var anonymous = factory.CreateBrowserClient(out _);

        foreach (var url in new[] { "/api/leave-types", "/api/leave-policies", "/api/leave-applications/query", "/api/leave/balances?employeeId=1", "/api/leave/calendar?from=2026-12-01&to=2026-12-02" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/leave-applications/1/approve", new { rowVersion = "AAAAAAAAAAA=" })).StatusCode);
    }
}
