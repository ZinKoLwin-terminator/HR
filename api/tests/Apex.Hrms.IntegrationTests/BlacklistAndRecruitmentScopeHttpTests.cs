using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Organization.Branches;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Organization.Departments;
using Apex.Hrms.Application.Organization.JobGrades;
using Apex.Hrms.Application.Organization.Positions;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The do-not-rehire list with its recruitment check, "HR all, managers own" recruitment scoping, the employee picker and
/// "my payslips", over HTTP against the test database. Everyone is fictional; rows carry the TST tag and are removed with it
/// (hr.EmployeeBlacklist before hr.Employee). Nothing here runs unless the database is free.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class BlacklistAndRecruitmentScopeHttpTests : PayrollTestBase, IDisposable
{
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromMinutes(390);

    private static readonly string[] RecruiterGrants =
        ["dashboard:View", "recruitment:View", "recruitment:Create", "recruitment:Edit", "employees:View"];

    private static readonly string[] ManagerGrants = ["dashboard:View", "recruitment:View"];

    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];

    public BlacklistAndRecruitmentScopeHttpTests() => factory.EnsurePointsAtTestDatabase();

    protected override void Cleanup()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }

        clients.Clear();
        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private static DateOnly BusinessToday => DateOnly.FromDateTime(DateTime.UtcNow + BusinessOffset);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static string Version(JsonElement body) => body.GetProperty("rowVersion").GetString()!;

    private static int Id(JsonElement body) => body.GetProperty("id").GetInt32();

    private async Task<HttpClient> SignedInAsync(TestAccount account)
    {
        var client = factory.CreateBrowserClient(out _);
        clients.Add(client);
        await client.SignInAsync(account);
        return client;
    }

    private async Task<HttpClient> SignedInAsRoleAsync(string suffix, int? employeeId, params string[] grants)
    {
        var role = $"{Tag}-{suffix}";
        var roleId = await Sql.ScalarAsync<int>("INSERT INTO sec.Role (Name, IsSystem, Status) OUTPUT INSERTED.Id VALUES (@name, 0, 'Active');", ("@name", role));
        foreach (var grant in grants.OrderBy(grant => grant.EndsWith(":View", StringComparison.Ordinal) ? 0 : 1))
        {
            var parts = grant.Split(':');
            await Sql.ExecuteAsync(
                "INSERT INTO sec.RolePermission (RoleId, ModuleKey, Action) VALUES (@role, @module, @action);",
                ("@role", roleId),
                ("@module", parts[0]),
                ("@action", parts[1]));
        }

        return await SignedInAsync(await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, $"{suffix}-login"), roleName: role, employeeId: employeeId));
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string url, object body, HttpStatusCode expected)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(expected, response.StatusCode);
        return await ReadAsync(response);
    }

    /// <summary>A former employee (exit command over HTTP) of the first employee's organization.</summary>
    private async Task<EmployeeDto> SeedFormerEmployeeAsync(HttpClient admin, (CompanyDto Company, BranchDto Branch, DepartmentDto Department, JobGradeDto Grade, PositionDto Position) org)
    {
        var employee = await SeedEmployeeAsync(org, "9", "leaver");
        var exited = await PostAsync(
            admin,
            $"/api/employees/{employee.Id}/exit",
            new { rowVersion = Requests.Version(employee.RowVersion), exitDate = Iso(BusinessToday.AddDays(-1)), status = "Resigned" },
            HttpStatusCode.OK);
        Assert.Equal("Resigned", exited.GetProperty("employmentStatus").GetString());
        return employee;
    }

    private static async Task<JsonElement> CreateCandidateAsync(HttpClient client, string email, string? nationalId = null) =>
        await PostAsync(
            client,
            "/api/candidates",
            new
            {
                firstName = "Ada",
                lastName = "Testcandidate",
                email,
                phone = "+95 9 555 0188",
                source = "Referral",
                currentEmployer = "Sample Employer",
                experienceYears = 4.5,
                resumeName = "cv.pdf",
                nationalId,
            },
            HttpStatusCode.Created);

    private async Task<JsonElement> CreateOpenJobAsync(HttpClient client, int departmentId, int managerId, string title)
    {
        var job = await PostAsync(
            client,
            "/api/job-openings",
            new
            {
                title = $"{Tag} {title}",
                departmentId,
                hiringManagerId = managerId,
                employmentType = "Full-time",
                vacancies = 1,
                closingDate = Iso(BusinessToday.AddDays(60)),
                description = "Builds test services.",
            },
            HttpStatusCode.Created);
        return await PostAsync(client, $"/api/job-openings/{Id(job)}/open", new { rowVersion = Version(job) }, HttpStatusCode.OK);
    }

    private static async Task<JsonElement> ApplyAsync(HttpClient client, JsonElement job, JsonElement candidate) =>
        await PostAsync(client, "/api/applications", new { jobOpeningId = Id(job), candidateId = Id(candidate) }, HttpStatusCode.Created);

    private static async Task<JsonElement> ReadyToSelectAsync(HttpClient client, JsonElement job, JsonElement candidate, int interviewerId)
    {
        var application = await ApplyAsync(client, job, candidate);
        application = await PostAsync(client, $"/api/applications/{Id(application)}/stage", new { rowVersion = Version(application), stage = "Screening" }, HttpStatusCode.OK);
        var interview = await PostAsync(
            client,
            "/api/interviews",
            new
            {
                applicationId = Id(application),
                round = "Technical",
                date = Iso(BusinessToday),
                startTime = "10:00",
                durationMinutes = 60,
                mode = "Video",
                interviewerId,
                location = "Meeting link",
            },
            HttpStatusCode.Created);
        await PostAsync(client, $"/api/interviews/{Id(interview)}/outcome", new { rowVersion = Version(interview), score = 5, recommendation = "Strong hire", feedback = "Excellent." }, HttpStatusCode.OK);
        return await ReadAsync(await client.GetAsync($"/api/applications/{Id(application)}"));
    }

    // ---- The list ----

    [IntegrationFact]
    public async Task The_do_not_rehire_list_works_over_http_and_flags_a_matching_candidate_only_as_a_boolean()
    {
        var org = await SeedOrganizationAsync();
        var current = await SeedEmployeeAsync(org, "1", "current");
        var admin = await SignedInAsync(TestAdministrator.Account);
        var former = await SeedFormerEmployeeAsync(admin, org);

        var refused = await PostAsync(admin, "/api/employee-blacklist", new { employeeId = current.Id, reason = "Not allowed." }, HttpStatusCode.Conflict);
        Assert.Equal("Only a former employee can be put on the do-not-rehire list.", refused.GetProperty("detail").GetString());
        await PostAsync(admin, "/api/employee-blacklist", new { employeeId = former.Id, reason = " " }, HttpStatusCode.BadRequest);

        var created = await admin.PostAsJsonAsync("/api/employee-blacklist", new { employeeId = former.Id, reason = "Confidential reason." });
        var entry = await ReadAsync(created);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/employee-blacklist/{Id(entry)}", created.Headers.Location?.OriginalString);
        Assert.Equal(former.EmployeeCode, entry.GetProperty("employeeCode").GetString());
        Assert.Equal("Confidential reason.", entry.GetProperty("reason").GetString());
        Assert.Equal(Iso(BusinessToday), entry.GetProperty("blacklistedOn").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("liftedOn").ValueKind);
        await PostAsync(admin, "/api/employee-blacklist", new { employeeId = former.Id, reason = "Again." }, HttpStatusCode.Conflict);

        var active = await ReadAsync(await admin.GetAsync("/api/employee-blacklist/query?active=true&pageSize=50"));
        Assert.Contains(Id(entry), active.GetProperty("items").EnumerateArray().Select(Id));
        Assert.Equal(Id(entry), Id(await ReadAsync(await admin.GetAsync($"/api/employees/{former.Id}/blacklist"))));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/employees/{current.Id}/blacklist")).StatusCode);

        var candidate = await CreateCandidateAsync(admin, former.WorkEmail.ToUpperInvariant());
        var other = await CreateCandidateAsync(admin, Requests.CandidateEmail("fine"));
        var json = (await admin.GetStringAsync($"/api/candidates/{Id(candidate)}"));
        Assert.True(candidate.GetProperty("blacklistMatch").GetBoolean());
        Assert.False(other.GetProperty("blacklistMatch").GetBoolean());
        Assert.DoesNotContain("Confidential", json, StringComparison.Ordinal);
        Assert.DoesNotContain(former.EmployeeCode, json, StringComparison.Ordinal);
        Assert.DoesNotContain("nationalId", json, StringComparison.OrdinalIgnoreCase);

        var activity = await ReadAsync(await admin.GetAsync($"/api/activity/query?search={former.EmployeeCode}&pageSize=20"));
        var messages = activity.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("message").GetString()).ToList();
        Assert.Contains($"Employee {former.EmployeeCode} added to the do-not-rehire list", messages);
        Assert.DoesNotContain(messages, message => message!.Contains("Confidential", StringComparison.Ordinal));

        var deleteBlocked = await admin.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/employees/{former.Id}")
        {
            Content = JsonContent.Create(new { rowVersion = Version(await ReadAsync(await admin.GetAsync($"/api/employees/{former.Id}"))) }),
        });
        Assert.Equal(HttpStatusCode.Conflict, deleteBlocked.StatusCode);

        var lifted = await PostAsync(admin, $"/api/employee-blacklist/{Id(entry)}/lift", new { rowVersion = Version(entry), reason = "Cleared after review." }, HttpStatusCode.OK);
        Assert.Equal(Iso(BusinessToday), lifted.GetProperty("liftedOn").GetString());
        Assert.False((await ReadAsync(await admin.GetAsync($"/api/candidates/{Id(candidate)}"))).GetProperty("blacklistMatch").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/employees/{former.Id}/blacklist")).StatusCode);
        await PostAsync(admin, $"/api/employee-blacklist/{Id(entry)}/lift", new { rowVersion = Version(lifted), reason = "Twice." }, HttpStatusCode.Conflict);
    }

    [IntegrationFact]
    public async Task The_list_needs_its_own_permissions_which_a_recruiter_does_not_hold()
    {
        var org = await SeedOrganizationAsync();
        var admin = await SignedInAsync(TestAdministrator.Account);
        var former = await SeedFormerEmployeeAsync(admin, org);
        var recruiter = await SignedInAsRoleAsync("Recruiter", null, RecruiterGrants);

        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/api/employee-blacklist/query")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync($"/api/employees/{former.Id}/blacklist")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.PostAsJsonAsync("/api/employee-blacklist", new { employeeId = former.Id, reason = "No." })).StatusCode);
    }

    // ---- The hire gate ----

    [IntegrationFact]
    public async Task Selecting_a_matching_candidate_needs_an_administrator_override_which_is_stored()
    {
        var org = await SeedOrganizationAsync();
        var manager = await SeedEmployeeAsync(org, "1", "manager");
        var interviewer = await SeedEmployeeAsync(org, "2", "interviewer");
        var admin = await SignedInAsync(TestAdministrator.Account);
        var former = await SeedFormerEmployeeAsync(admin, org);
        await PostAsync(admin, "/api/employee-blacklist", new { employeeId = former.Id, reason = "Confidential reason." }, HttpStatusCode.Created);
        var recruiter = await SignedInAsRoleAsync("Recruiter", null, RecruiterGrants);
        var job = await CreateOpenJobAsync(admin, org.Department.Id, manager.Id, "Backend");
        var candidate = await CreateCandidateAsync(recruiter, former.WorkEmail);
        var application = await ReadyToSelectAsync(recruiter, job, candidate, interviewer.Id);
        Assert.True(application.GetProperty("blacklistMatch").GetBoolean());
        var url = $"/api/applications/{Id(application)}/stage";

        var refused = await PostAsync(recruiter, url, new { rowVersion = Version(application), stage = "Selected" }, HttpStatusCode.Conflict);
        Assert.Equal("This candidate matches the do-not-rehire list. An HR Administrator must approve the hire with a reason.", refused.GetProperty("detail").GetString());
        await PostAsync(recruiter, url, new { rowVersion = Version(application), stage = "Selected", blacklistOverrideReason = "I say so." }, HttpStatusCode.Forbidden);
        await PostAsync(admin, url, new { rowVersion = Version(application), stage = "Selected", blacklistOverrideReason = new string('x', 301) }, HttpStatusCode.BadRequest);

        var selected = await PostAsync(admin, url, new { rowVersion = Version(application), stage = "Selected", blacklistOverrideReason = "Approved by the director." }, HttpStatusCode.OK);

        Assert.Equal("Selected", selected.GetProperty("stage").GetString());
        Assert.Equal("Approved by the director.", await Sql.ScalarAsync<string>("SELECT BlacklistOverrideReason FROM rec.Application WHERE Id = @id", ("@id", Id(application))));
        Assert.Equal(TestAdministrator.AccountId, await Sql.ScalarAsync<int>("SELECT BlacklistOverrideByUserId FROM rec.Application WHERE Id = @id", ("@id", Id(application))));
        var activity = await ReadAsync(await admin.GetAsync($"/api/activity/query?search=approved despite&pageSize=20"));
        var messages = activity.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("message").GetString()).ToList();
        Assert.Contains($"Hire of application #{Id(application)} approved despite a do-not-rehire match", messages);
    }

    // ---- Recruitment scoping and the picker ----

    [IntegrationFact]
    public async Task A_hiring_manager_sees_only_their_openings_while_a_recruiter_sees_all_and_the_picker_shows_no_personal_data()
    {
        var org = await SeedOrganizationAsync();
        var maya = await SeedEmployeeAsync(org, "1", "maya");
        var other = await SeedEmployeeAsync(org, "2", "other");
        var admin = await SignedInAsync(TestAdministrator.Account);
        var mayasJob = await CreateOpenJobAsync(admin, org.Department.Id, maya.Id, "Mayas");
        var otherJob = await CreateOpenJobAsync(admin, org.Department.Id, other.Id, "Others");
        var mayasCandidate = await CreateCandidateAsync(admin, Requests.CandidateEmail("one"));
        var otherCandidate = await CreateCandidateAsync(admin, Requests.CandidateEmail("two"));
        var mayasApplication = await ApplyAsync(admin, mayasJob, mayasCandidate);
        var otherApplication = await ApplyAsync(admin, otherJob, otherCandidate);
        var manager = await SignedInAsRoleAsync("Manager", maya.Id, ManagerGrants);
        var recruiter = await SignedInAsRoleAsync("Recruiter", null, RecruiterGrants);

        async Task<List<int>> IdsAsync(HttpClient client, string url) =>
            [.. (await ReadAsync(await client.GetAsync(url))).GetProperty("items").EnumerateArray().Select(Id)];

        var managerJobs = await IdsAsync(manager, $"/api/job-openings/query?search={Tag}&pageSize=50");
        var managerCandidates = await IdsAsync(manager, $"/api/candidates/query?search={Tag.ToLowerInvariant()}&pageSize=50");
        var managerApplications = await IdsAsync(manager, "/api/applications/query?pageSize=200");
        var recruiterJobs = await IdsAsync(recruiter, $"/api/job-openings/query?search={Tag}&pageSize=50");

        Assert.Equal([Id(mayasJob)], managerJobs);
        Assert.Equal([Id(mayasCandidate)], managerCandidates);
        Assert.Contains(Id(mayasApplication), managerApplications);
        Assert.DoesNotContain(Id(otherApplication), managerApplications);
        Assert.Equal(2, recruiterJobs.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/job-openings/{Id(otherJob)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/candidates/{Id(otherCandidate)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/applications/{Id(otherApplication)}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/job-openings/{Id(mayasJob)}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync($"/api/job-openings/{Id(otherJob)}")).StatusCode);
        var counts = await ReadAsync(await manager.GetAsync("/api/recruitment/counts"));
        Assert.Equal([Id(mayasJob)], counts.GetProperty("jobOpenings").EnumerateArray().Select(item => item.GetProperty("jobOpeningId").GetInt32()));

        var lookup = await ReadAsync(await recruiter.GetAsync($"/api/employees/lookup?search={maya.EmployeeCode}&pageSize=50"));
        var first = lookup.GetProperty("items").EnumerateArray().First();
        Assert.Equal(
            ["departmentName", "displayName", "employeeCode", "id", "jobTitle"],
            first.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/employees/lookup")).StatusCode);
    }

    // ---- My payslips ----

    [IntegrationFact]
    public async Task My_payslips_show_only_my_own_of_an_approved_run_and_need_no_payroll_permission()
    {
        var world = await SeedPayrollAsync("ada", "1");
        var ben = await SeedSecondEmployeeAsync(world);
        var admin = await SignedInAsync(TestAdministrator.Account);
        var run = (await (await admin.PostAsJsonAsync("/api/payroll-runs", new { year = Year, month = 3 })).Content.ReadFromJsonAsync<PayrollRunDto>())!;
        var processed = (await (await admin.PostAsJsonAsync($"/api/payroll-runs/{run.Id}/process", new { rowVersion = Requests.Version(run.RowVersion) })).Content.ReadFromJsonAsync<PayrollRunDto>())!;
        var ada = await SignedInAsRoleAsync("Ada", world.Employee.Id, "dashboard:View");
        var noEmployee = await SignedInAsRoleAsync("NoEmployee", null, "dashboard:View");

        var beforeApproval = await ReadAsync(await ada.GetAsync("/api/me/payslips"));
        Assert.Equal(0, beforeApproval.GetProperty("total").GetInt32());

        var approver = await SignedInAsRoleAsync("Approver", null, "dashboard:View", "payroll:View", "payroll:Create", "payroll:Edit", "payroll:Approve");
        Assert.Equal(HttpStatusCode.OK, (await approver.PostAsJsonAsync($"/api/payroll-runs/{processed.Id}/approve", new { rowVersion = Requests.Version(processed.RowVersion) })).StatusCode);

        var mine = await ReadAsync(await ada.GetAsync("/api/me/payslips"));
        var items = mine.GetProperty("items").EnumerateArray().ToList();
        var own = Assert.Single(items);
        Assert.Equal(world.Employee.Id, own.GetProperty("employeeId").GetInt32());
        Assert.EndsWith("7890", own.GetProperty("accountMasked").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(AccountNumber, own.ToString(), StringComparison.Ordinal);
        var slips = (await admin.GetFromJsonAsync<PagedResult<PayslipDto>>($"/api/payslips/query?runId={run.Id}&pageSize=50"))!;
        var bensSlip = slips.Items.Single(item => item.EmployeeId == ben.Id);

        Assert.Equal(HttpStatusCode.OK, (await ada.GetAsync($"/api/me/payslips/{Id(own)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ada.GetAsync($"/api/me/payslips/{bensSlip.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.GetAsync("/api/payslips/query")).StatusCode);
        Assert.Equal(0, (await ReadAsync(await noEmployee.GetAsync("/api/me/payslips"))).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await noEmployee.GetAsync($"/api/me/payslips/{Id(own)}")).StatusCode);
    }
}
