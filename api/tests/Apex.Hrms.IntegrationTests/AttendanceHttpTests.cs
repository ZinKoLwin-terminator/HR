using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The attendance routes over HTTP: routing, the "HH:mm" time format, the shapes of the views, the command bodies, the
/// error mapping and the server-owned time of a punch. The host uses the real clock and the default business time zone
/// (Asia/Yangon, UTC+6:30, no daylight saving).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class AttendanceHttpTests : DatabaseTest, IDisposable
{
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromMinutes(390);

    private static readonly string[] CellKinds = ["Shift", "Off", "Holiday", "Leave"];

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public AttendanceHttpTests()
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

    private static DateTime BusinessNow => DateTime.UtcNow + BusinessOffset;

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private object ShiftBody(string suffix = "S1", int[]? workDays = null, string status = "Active") => new
    {
        code = Code(suffix),
        name = Name("General"),
        startTime = "09:00",
        endTime = "18:00",
        breakMinutes = 60,
        graceMinutes = 15,
        workDays = workDays ?? [1, 2, 3, 4, 5],
        isNightShift = false,
        color = "#3f6fd8",
        status,
    };

    private async Task<JsonElement> CreateShiftAsync(string suffix = "S1")
    {
        var response = await client.PostAsJsonAsync("/api/shifts", ShiftBody(suffix));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private async Task<(int EmployeeId, int BranchId, int DepartmentId)> SeedWorkerAsync(int shiftId, string codeSuffix = "1", string local = "mia")
    {
        var org = await SeedOrganizationAsync();
        var request = Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, codeSuffix, local) with { ShiftId = shiftId };
        var employee = Succeeded(await Create(request));
        return (employee.Id, org.Branch.Id, org.Department.Id);
    }

    [IntegrationFact]
    public async Task Shifts_use_the_resource_routes_and_hour_minute_times()
    {
        var created = await client.PostAsJsonAsync("/api/shifts", ShiftBody(workDays: [5, 1, 3]));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var shift = await ReadAsync(created);
        var id = shift.GetProperty("id").GetInt32();
        Assert.Equal($"/api/shifts/{id}", created.Headers.Location?.OriginalString);
        Assert.Equal("09:00", shift.GetProperty("startTime").GetString());
        Assert.Equal("18:00", shift.GetProperty("endTime").GetString());
        Assert.Equal([1, 3, 5], shift.GetProperty("workDays").EnumerateArray().Select(day => day.GetInt32()));
        Assert.Equal("Active", shift.GetProperty("status").GetString());

        var query = await ReadAsync(await client.GetAsync($"/api/shifts/query?search={Tag}&status=Active"));
        Assert.Equal(id, Assert.Single(query.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
        var deactivated = await client.PostAsJsonAsync($"/api/shifts/{id}/deactivate", new { rowVersion = shift.GetProperty("rowVersion").GetString() });
        Assert.Equal("Inactive", (await ReadAsync(deactivated)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task A_shift_without_a_working_day_is_a_400_with_a_field_error_and_a_used_shift_cannot_be_deleted()
    {
        var bad = await client.PostAsJsonAsync("/api/shifts", ShiftBody("BAD", workDays: []));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var problem = await ReadAsync(bad);
        Assert.Equal("Validation", problem.GetProperty("code").GetString());
        Assert.Equal("Choose at least one day.", problem.GetProperty("errors").GetProperty("workDays")[0].GetString());

        var shift = await CreateShiftAsync();
        await SeedWorkerAsync(shift.GetProperty("id").GetInt32());
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/shifts/{shift.GetProperty("id").GetInt32()}")
        {
            Content = JsonContent.Create(new { rowVersion = shift.GetProperty("rowVersion").GetString() }),
        };
        var refused = await client.SendAsync(delete);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("assigned employee(s)", (await ReadAsync(refused)).GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task A_punch_takes_no_date_or_time_from_the_client_and_is_stamped_with_server_time()
    {
        var shift = await CreateShiftAsync();
        var (employeeId, _, _) = await SeedWorkerAsync(shift.GetProperty("id").GetInt32());

        var before = BusinessNow;
        var response = await client.PostAsJsonAsync(
            "/api/attendance/punches",
            new { employeeId, kind = "CheckIn", date = "2001-01-01", time = "03:00" });
        var after = BusinessNow;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await ReadAsync(response);
        var date = DateOnly.ParseExact(record.GetProperty("date").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var time = TimeOnly.ParseExact(record.GetProperty("checkIn").GetString()!, "HH:mm", CultureInfo.InvariantCulture);
        var stamped = date.ToDateTime(time);
        Assert.InRange(stamped, before.AddMinutes(-2), after.AddMinutes(1));
        Assert.Equal("Web", record.GetProperty("source").GetString());
        Assert.Equal(8, Convert.FromBase64String(record.GetProperty("rowVersion").GetString()!).Length);
    }

    [IntegrationFact]
    public async Task A_punch_with_an_unknown_kind_or_a_second_check_in_is_refused_with_the_error_shape()
    {
        var shift = await CreateShiftAsync();
        var (employeeId, _, _) = await SeedWorkerAsync(shift.GetProperty("id").GetInt32());

        var badKind = await client.PostAsJsonAsync("/api/attendance/punches", new { employeeId, kind = "in" });
        Assert.Equal(HttpStatusCode.BadRequest, badKind.StatusCode);
        Assert.True((await ReadAsync(badKind)).GetProperty("errors").TryGetProperty("kind", out _));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/attendance/punches", new { employeeId, kind = "CheckIn" })).StatusCode);
        var again = await client.PostAsJsonAsync("/api/attendance/punches", new { employeeId, kind = "CheckIn" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("Conflict", (await ReadAsync(again)).GetProperty("code").GetString());

        var noBody = await client.PostAsJsonAsync("/api/attendance/punches", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);
    }

    [IntegrationFact]
    public async Task The_adjustment_flow_over_http_applies_the_approval_to_the_record()
    {
        var shift = await CreateShiftAsync();
        var (employeeId, _, _) = await SeedWorkerAsync(shift.GetProperty("id").GetInt32());
        var punch = await ReadAsync(await client.PostAsJsonAsync("/api/attendance/punches", new { employeeId, kind = "CheckIn" }));
        var date = punch.GetProperty("date").GetString();

        var created = await client.PostAsJsonAsync("/api/attendance-adjustments", new { employeeId, date, requestedStatus = "Half Day", reason = "Left early" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var adjustment = await ReadAsync(created);
        Assert.Equal("Pending", adjustment.GetProperty("status").GetString());
        var id = adjustment.GetProperty("id").GetInt32();

        var duplicate = await client.PostAsJsonAsync("/api/attendance-adjustments", new { employeeId, date, requestedStatus = "Present", reason = "Again" });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.True((await ReadAsync(duplicate)).GetProperty("errors").TryGetProperty("date", out _));

        var noRemarks = await client.PostAsJsonAsync($"/api/attendance-adjustments/{id}/reject", new { rowVersion = adjustment.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.BadRequest, noRemarks.StatusCode);

        var approved = await client.PostAsJsonAsync(
            $"/api/attendance-adjustments/{id}/approve",
            new { rowVersion = adjustment.GetProperty("rowVersion").GetString(), remarks = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var decided = await ReadAsync(approved);
        Assert.Equal("Approved", decided.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(decided.GetProperty("decidedBy").GetString()));

        var record = await ReadAsync(await client.GetAsync($"/api/attendance-records/{punch.GetProperty("id").GetInt32()}"));
        Assert.Equal("Half Day", record.GetProperty("status").GetString());
        Assert.Equal("Manual", record.GetProperty("source").GetString());
        Assert.Equal(Name("General"), record.GetProperty("shiftName").GetString());

        // There is no unpaged list of adjustments (they carry reasons): only the paged query.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/api/attendance-adjustments/")).StatusCode);
    }

    [IntegrationFact]
    public async Task Overtime_over_http_derives_minutes_and_is_approved_with_a_command()
    {
        var shift = await CreateShiftAsync();
        var (employeeId, _, _) = await SeedWorkerAsync(shift.GetProperty("id").GetInt32());
        var today = DateOnly.FromDateTime(BusinessNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var created = await client.PostAsJsonAsync("/api/overtime-records", new { employeeId, date = today, startTime = "18:00", endTime = "20:00", reason = "Close" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var overtime = await ReadAsync(created);
        Assert.Equal(120, overtime.GetProperty("minutes").GetInt32());
        Assert.Equal("18:00", overtime.GetProperty("startTime").GetString());

        var tooShort = await client.PostAsJsonAsync("/api/overtime-records", new { employeeId, date = today, startTime = "18:00", endTime = "18:10", reason = "Close" });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        var approved = await client.PostAsJsonAsync(
            $"/api/overtime-records/{overtime.GetProperty("id").GetInt32()}/approve",
            new { rowVersion = overtime.GetProperty("rowVersion").GetString() });
        Assert.Equal("Approved", (await ReadAsync(approved)).GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task The_overview_and_schedule_have_the_shapes_of_the_frontend_models()
    {
        var shift = await CreateShiftAsync();
        var (employeeId, branchId, departmentId) = await SeedWorkerAsync(shift.GetProperty("id").GetInt32());
        await client.PostAsJsonAsync("/api/attendance/punches", new { employeeId, kind = "CheckIn" });
        var today = DateOnly.FromDateTime(BusinessNow);

        var overview = await ReadAsync(await client.GetAsync($"/api/attendance/overview?date={today:yyyy-MM-dd}&branchId={branchId}"));

        var summary = overview.GetProperty("summary");
        foreach (var field in new[] { "date", "scheduled", "present", "late", "halfDay", "absent", "onLeave", "attendanceRate" })
        {
            Assert.True(summary.TryGetProperty(field, out _), field);
        }

        foreach (var list in new[] { "byDepartment", "late", "absent", "trend" })
        {
            Assert.Equal(JsonValueKind.Array, overview.GetProperty(list).ValueKind);
        }

        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var schedule = await ReadAsync(await client.GetAsync($"/api/attendance/schedule?weekStart={weekStart:yyyy-MM-dd}&departmentId={departmentId}&branchId={branchId}"));

        Assert.Equal(weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), schedule.GetProperty("weekStart").GetString());
        Assert.Equal(7, schedule.GetProperty("days").GetArrayLength());
        var row = Assert.Single(schedule.GetProperty("rows").EnumerateArray());
        Assert.Equal(employeeId, row.GetProperty("employeeId").GetInt32());
        foreach (var field in new[] { "employeeCode", "employeeName", "department", "shiftName", "shiftColor" })
        {
            Assert.True(row.TryGetProperty(field, out _), field);
        }

        var cells = row.GetProperty("cells").EnumerateArray().ToList();
        Assert.Equal(7, cells.Count);
        Assert.All(cells, cell => Assert.Contains(cell.GetProperty("kind").GetString(), CellKinds));
        Assert.Equal("Shift", cells[0].GetProperty("kind").GetString());
        Assert.Equal("09:00–18:00", cells[0].GetProperty("label").GetString());
    }

    [IntegrationFact]
    public async Task A_missing_date_for_the_overview_is_a_400_and_an_unknown_id_is_a_404()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/attendance/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/attendance-records/2000000000")).StatusCode);
    }

    [IntegrationFact]
    public async Task Holidays_are_created_listed_by_date_range_and_unique_by_date()
    {
        var holiday = new { date = "2041-05-06", name = Code("HOL"), type = "Public" };

        var created = await client.PostAsJsonAsync("/api/holidays", holiday);
        var duplicate = await client.PostAsJsonAsync("/api/holidays", holiday);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.True((await ReadAsync(duplicate)).GetProperty("errors").TryGetProperty("date", out _));
        var query = await ReadAsync(await client.GetAsync($"/api/holidays/query?dateFrom=2041-05-01&dateTo=2041-05-31&search={Tag}"));
        Assert.Equal("2041-05-06", Assert.Single(query.GetProperty("items").EnumerateArray()).GetProperty("date").GetString());
    }
}
