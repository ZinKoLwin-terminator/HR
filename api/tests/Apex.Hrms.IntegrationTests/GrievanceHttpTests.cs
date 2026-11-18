using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.IntegrationTests.Support;
using Apex.Hrms.TestSupport;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The grievance module over HTTP against the test database and a temporary grievance folder (removed with the host): the whole
/// lifecycle, the status codes per route group (401, 403, 404, 409, 400), the anonymous redaction end to end (API answers and stored
/// rows), files, the read receipt, the append-only grants and the two grv.Case triggers. Fictional data only; every row carries the tag.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class GrievanceHttpTests : DatabaseTest, IDisposable
{
    private static readonly string[] SelfGrants = ["grievance:View", "grievance:Create"];
    private static readonly string[] ManagementGrants = ["grievance:View", "grievance:Create", "grievance-management:View", "grievance-management:Create", "grievance-management:Edit", "grievance-management:Delete"];
    private static readonly string[] InvestigatorGrants = ["grievance:View", "grievance:Create", "grievance-investigation:View", "grievance-investigation:Edit"];
    private static readonly string[] ReviewerGrants = ["grievance:View", "grievance:Create", "grievance-review:View", "grievance-review:Approve"];

    private static readonly string[] SubmitterActors = ["You", "HR team"];

    private readonly ApiFactory factory = new();
    private readonly HttpClient admin;
    private readonly List<HttpClient> others = [];

    public GrievanceHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        admin = factory.CreateBrowserClient(out _);
    }

    public override Task InitializeAsync() => admin.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        admin.Dispose();
        foreach (var other in others)
        {
            other.Dispose();
        }

        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private sealed record People(
        EmployeeDto Sam,
        EmployeeDto Hana,
        EmployeeDto Ivan,
        EmployeeDto Rita,
        int CategoryId,
        HttpClient SamClient,
        HttpClient HanaClient,
        HttpClient IvanClient,
        HttpClient RitaClient);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = response.Content.Headers.ContentLength == 0 ? default : await ReadAsync(response);
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {body}");
        return body;
    }

    private async Task<HttpClient> SignedInAsync(string suffix, int? employeeId, params string[] grants)
    {
        var role = $"{Tag}-{suffix}";
        var roleId = await Sql.ScalarAsync<int>("INSERT INTO sec.Role (Name, IsSystem, Status) OUTPUT INSERTED.Id VALUES (@name, 0, 'Active');", ("@name", role));
        foreach (var grant in grants)
        {
            var parts = grant.Split(':');
            await Sql.ExecuteAsync(
                "INSERT INTO sec.RolePermission (RoleId, ModuleKey, Action) VALUES (@role, @module, @action);",
                ("@role", roleId),
                ("@module", parts[0]),
                ("@action", parts[1]));
        }

        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, $"{suffix}-login"), roleName: role, employeeId: employeeId);
        var signedIn = factory.CreateBrowserClient(out _);
        others.Add(signedIn);
        await signedIn.SignInAsync(account);
        return signedIn;
    }

    private async Task<People> SetupAsync(bool hanaAlsoReviews = false)
    {
        var org = await SeedOrganizationAsync();
        async Task<EmployeeDto> Employee(string suffix, string local, string first) =>
            Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, suffix, local) with { FirstName = first }));

        var sam = await Employee("1", "sam", "Samwise");
        var hana = await Employee("2", "hana", "Hanako");
        var ivan = await Employee("3", "ivan", "Ivanka");
        var rita = await Employee("4", "rita", "Ritanna");
        var category = await OkAsync(
            await admin.PostAsJsonAsync("/api/grievances/categories/", new
            {
                code = Code("CAT"),
                name = Name("Harassment"),
                description = (string?)null,
                allowsAnonymous = true,
                defaultTargetDays = 14,
                sortOrder = 1,
                status = "Active",
            }),
            HttpStatusCode.Created);
        return new People(
            sam,
            hana,
            ivan,
            rita,
            category.GetProperty("id").GetInt32(),
            await SignedInAsync("Sam", sam.Id, SelfGrants),
            await SignedInAsync("Hana", hana.Id, hanaAlsoReviews ? [.. ManagementGrants, "grievance-review:View", "grievance-review:Approve"] : ManagementGrants),
            await SignedInAsync("Ivan", ivan.Id, InvestigatorGrants),
            await SignedInAsync("Rita", rita.Id, ReviewerGrants));
    }

    private async Task<JsonElement> SubmittedAsync(People people, bool anonymous = false)
    {
        var draft = await OkAsync(
            await people.SamClient.PostAsJsonAsync("/api/grievances/my", new
            {
                categoryId = people.CategoryId,
                subject = Name("Unfair shift changes"),
                description = "My shifts are changed without notice every single week.",
                incidentDate = (string?)null,
                location = (string?)null,
                involvedPersonText = (string?)null,
                involvedDepartmentId = (int?)null,
                isAnonymous = anonymous,
            }),
            HttpStatusCode.Created);
        return await OkAsync(await people.SamClient.PostAsJsonAsync(
            $"/api/grievances/my/{draft.GetProperty("id").GetInt32()}/submit",
            new { rowVersion = draft.GetProperty("rowVersion").GetString(), declarationAccepted = true }));
    }

    private static Task<HttpResponseMessage> Step(HttpClient client, int id, string action, object body) =>
        client.PostAsJsonAsync($"/api/grievances/cases/{id}/{action}", body);

    private static string Version(JsonElement detail) => detail.GetProperty("rowVersion").GetString()!;

    // ---- The whole lifecycle ----

    [IntegrationFact]
    public async Task A_case_goes_from_submission_to_closure_and_is_reopened_only_with_review_approval()
    {
        var people = await SetupAsync();
        var submitted = await SubmittedAsync(people);
        var id = submitted.GetProperty("id").GetInt32();
        Assert.Matches("^GRV-[0-9]{4}-[0-9]{5,}$", submitted.GetProperty("caseNo").GetString());

        var acknowledged = await OkAsync(await Step(people.HanaClient, id, "acknowledge", new { rowVersion = Version(await OkAsync(await people.HanaClient.GetAsync($"/api/grievances/cases/{id}"))), priority = "High" }));
        var assigned = await OkAsync(await Step(people.HanaClient, id, "assign", new { rowVersion = Version(acknowledged), investigatorEmployeeId = people.Ivan.Id, role = "Lead" }));
        Assert.Equal("Assigned", assigned.GetProperty("status").GetString());
        var investigating = await OkAsync(await Step(people.IvanClient, id, "start-investigation", new { rowVersion = Version(assigned) }));
        var awaiting = await OkAsync(await Step(people.IvanClient, id, "request-info", new { rowVersion = Version(investigating), message = "Which dates were affected?" }));

        var mine = await OkAsync(await people.SamClient.GetAsync($"/api/grievances/my/{id}"));
        Assert.True(mine.GetProperty("hasUnreadUpdate").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await people.SamClient.PostAsync($"/api/grievances/my/{id}/seen", null)).StatusCode);
        var answered = await OkAsync(await people.SamClient.PostAsJsonAsync($"/api/grievances/my/{id}/messages", new { rowVersion = Version(mine), body = "The 3rd and the 10th." }));
        Assert.Equal("UnderInvestigation", answered.GetProperty("status").GetString());

        var current = await OkAsync(await people.IvanClient.GetAsync($"/api/grievances/cases/{id}"));
        var proposed = await OkAsync(await Step(people.IvanClient, id, "propose-resolution", new { rowVersion = Version(current), summary = "Mediation and a fixed rota." }));
        var resolved = await OkAsync(await Step(people.RitaClient, id, "approve-resolution", new { rowVersion = Version(proposed), visibleToSubmitter = true }));
        var closed = await OkAsync(await Step(people.HanaClient, id, "close", new { rowVersion = Version(resolved) }));
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, closed.GetProperty("retentionUntil").ValueKind);

        Assert.Equal(HttpStatusCode.Forbidden, (await Step(people.HanaClient, id, "reopen", new { rowVersion = Version(closed), reason = "New facts." })).StatusCode);
        // One account per employee: the person with both grants is a second HR user without an employee (like the built-in administrator).
        var both = await SignedInAsync("Reopener", null, [.. ManagementGrants, "grievance-review:View", "grievance-review:Approve"]);
        var reopened = await OkAsync(await Step(both, id, "reopen", new { rowVersion = Version(closed), reason = "New facts." }));
        Assert.Equal("Reopened", reopened.GetProperty("status").GetString());

        // Reopening ended Ivan's assignment: HR assigns him again as Lead and he continues the investigation.
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.Assignment WHERE CaseId = @id AND UnassignedAtUtc IS NULL", ("@id", id)));
        Assert.Equal(HttpStatusCode.NotFound, (await people.IvanClient.GetAsync($"/api/grievances/cases/{id}")).StatusCode);
        var reassigned = await OkAsync(await Step(people.HanaClient, id, "assign", new { rowVersion = Version(reopened), investigatorEmployeeId = people.Ivan.Id, role = "Lead" }));
        Assert.Equal("Assigned", reassigned.GetProperty("status").GetString());
        var continued = await OkAsync(await Step(people.IvanClient, id, "start-investigation", new { rowVersion = Version(reassigned) }));
        Assert.Equal("UnderInvestigation", continued.GetProperty("status").GetString());

        var history = await OkAsync(await people.HanaClient.GetAsync($"/api/grievances/cases/{id}/history"));
        Assert.Equal(
            ["Submit", "Acknowledge", "Assign", "StartInvestigation", "RequestInfo", "Respond", "ProposeResolution", "ApproveResolution", "Close", "Reopen", "Unassign", "Assign", "StartInvestigation"],
            history.EnumerateArray().Select(entry => entry.GetProperty("action").GetString()));
        var unassign = history.EnumerateArray().Single(entry => entry.GetProperty("action").GetString() == "Unassign");
        Assert.Equal(("Reopened", "Reopened", false, "Case reopened"), (unassign.GetProperty("fromStatus").GetString(), unassign.GetProperty("toStatus").GetString(), unassign.GetProperty("visibleToSubmitter").GetBoolean(), unassign.GetProperty("reason").GetString()));
        var timeline = (await OkAsync(await people.SamClient.GetAsync($"/api/grievances/my/{id}"))).GetProperty("timeline");
        Assert.All(timeline.EnumerateArray(), entry => Assert.Contains(entry.GetProperty("actor").GetString(), SubmitterActors));
        Assert.DoesNotContain(timeline.EnumerateArray(), entry => entry.GetProperty("action").GetString() == "Unassign");
    }

    [IntegrationFact]
    public async Task A_case_escalated_before_acknowledgement_is_not_escalated_again_by_a_critical_acknowledgement()
    {
        var people = await SetupAsync();
        var submitted = await SubmittedAsync(people);
        var id = submitted.GetProperty("id").GetInt32();
        var current = await OkAsync(await people.HanaClient.GetAsync($"/api/grievances/cases/{id}"));

        var escalated = await OkAsync(await Step(people.HanaClient, id, "escalate", new { rowVersion = Version(current), reason = "Urgent.", isImmediate = false }));
        var acknowledged = await OkAsync(await Step(people.HanaClient, id, "acknowledge", new { rowVersion = Version(escalated), priority = "Critical" }));

        Assert.Equal("Acknowledged", acknowledged.GetProperty("status").GetString());
        Assert.True(acknowledged.GetProperty("isEscalated").GetBoolean());
        Assert.Equal(1, acknowledged.GetProperty("escalations").GetArrayLength());
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.Escalation WHERE CaseId = @id", ("@id", id)));
    }

    [IntegrationFact]
    public async Task A_closed_case_is_archived_over_the_api_once()
    {
        var people = await SetupAsync();
        var closed = await ClosedAsync(people);
        var id = closed.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await Step(people.IvanClient, id, "archive", new { rowVersion = Version(closed) })).StatusCode);

        var archived = await OkAsync(await Step(people.HanaClient, id, "archive", new { rowVersion = Version(closed) }));

        Assert.Equal("Closed", archived.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, archived.GetProperty("archivedAtUtc").ValueKind);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.[Case] WHERE Id = @id AND ArchivedAtUtc IS NOT NULL", ("@id", id)));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.StatusHistory WHERE CaseId = @id AND Action = 'Archive' AND VisibleToSubmitter = 0", ("@id", id)));
        Assert.Equal(HttpStatusCode.Conflict, (await Step(people.HanaClient, id, "archive", new { rowVersion = Version(archived) })).StatusCode);
    }

    [IntegrationFact]
    public async Task A_stale_row_version_on_the_case_team_path_is_a_409_except_for_a_file_the_submitter_does_not_see()
    {
        var people = await SetupAsync();
        var investigating = await UnderInvestigationAsync(people);
        var id = investigating.GetProperty("id").GetInt32();
        var old = Version(investigating);
        await OkAsync(await people.IvanClient.PostAsJsonAsync($"/api/grievances/cases/{id}/messages", new { rowVersion = old, body = "We will call you." }));

        Assert.Equal(HttpStatusCode.Conflict, (await Step(people.IvanClient, id, "request-info", new { rowVersion = old, message = "Which dates?" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await people.IvanClient.PostAsJsonAsync($"/api/grievances/cases/{id}/messages", new { rowVersion = old, body = "Again." })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await people.IvanClient.PostAsync($"/api/grievances/cases/{id}/attachments", Upload(old, "letter.pdf", "Shared"))).StatusCode);
        await OkAsync(await people.IvanClient.PostAsync($"/api/grievances/cases/{id}/attachments", Upload(old, "internal.pdf", "CaseTeam")), HttpStatusCode.Created);

        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.Attachment WHERE CaseId = @id", ("@id", id)));
        Assert.Single(Directory.GetFiles(factory.GrievanceFolder));
    }

    [IntegrationFact]
    public async Task A_draft_with_a_file_is_deleted_with_its_rows_and_its_stored_file()
    {
        var people = await SetupAsync();
        var draft = await DraftAsync(people);
        var id = draft.GetProperty("id").GetInt32();
        await OkAsync(await people.SamClient.PostAsync($"/api/grievances/my/{id}/attachments", Upload(Version(draft), "rota.pdf")), HttpStatusCode.Created);
        Assert.Single(Directory.GetFiles(factory.GrievanceFolder));

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/grievances/my/{id}") { Content = JsonContent.Create(new { rowVersion = Version(draft) }) };
        Assert.Equal(HttpStatusCode.NoContent, (await people.SamClient.SendAsync(delete)).StatusCode);

        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.[Case] WHERE Id = @id", ("@id", id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.Attachment WHERE CaseId = @id", ("@id", id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.CaseSubmitter WHERE CaseId = @id", ("@id", id)));
        Assert.Empty(Directory.GetFiles(factory.GrievanceFolder));

        // The database rules behind it: the cascade of a draft passes grv.TR_Attachment_Fixed (the case row is already gone), a
        // file of a submitted case is never deleted (51102).
        var other = await DraftAsync(people);
        var otherId = other.GetProperty("id").GetInt32();
        await OkAsync(await people.SamClient.PostAsync($"/api/grievances/my/{otherId}/attachments", Upload(Version(other), "rota.pdf")), HttpStatusCode.Created);
        Assert.Equal(0, await Sql.ErrorNumberOfAsync("DELETE FROM grv.[Case] WHERE Id = @id AND Status = 'Draft'", ("@id", otherId)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.Attachment WHERE CaseId = @id", ("@id", otherId)));

        var submitted = await SubmittedAsync(people);
        var submittedId = submitted.GetProperty("id").GetInt32();
        await OkAsync(await people.SamClient.PostAsync($"/api/grievances/my/{submittedId}/attachments", Upload(Version(submitted), "rota.pdf")), HttpStatusCode.Created);
        Assert.Equal(51102, await Sql.ErrorNumberOfAsync("DELETE FROM grv.Attachment WHERE CaseId = @id", ("@id", submittedId)));
    }

    private async Task<JsonElement> DraftAsync(People people) =>
        await OkAsync(
            await people.SamClient.PostAsJsonAsync("/api/grievances/my", new { categoryId = people.CategoryId, subject = Name("Draft with a file"), isAnonymous = false }),
            HttpStatusCode.Created);

    private async Task<JsonElement> UnderInvestigationAsync(People people)
    {
        var submitted = await SubmittedAsync(people);
        var id = submitted.GetProperty("id").GetInt32();
        var current = await OkAsync(await people.HanaClient.GetAsync($"/api/grievances/cases/{id}"));
        var acknowledged = await OkAsync(await Step(people.HanaClient, id, "acknowledge", new { rowVersion = Version(current), priority = "High" }));
        var assigned = await OkAsync(await Step(people.HanaClient, id, "assign", new { rowVersion = Version(acknowledged), investigatorEmployeeId = people.Ivan.Id, role = "Lead" }));
        return await OkAsync(await Step(people.IvanClient, id, "start-investigation", new { rowVersion = Version(assigned) }));
    }

    private async Task<JsonElement> ClosedAsync(People people)
    {
        var investigating = await UnderInvestigationAsync(people);
        var id = investigating.GetProperty("id").GetInt32();
        var proposed = await OkAsync(await Step(people.IvanClient, id, "propose-resolution", new { rowVersion = Version(investigating), summary = "Mediation and a fixed rota." }));
        var resolved = await OkAsync(await Step(people.RitaClient, id, "approve-resolution", new { rowVersion = Version(proposed), visibleToSubmitter = true }));
        return await OkAsync(await Step(people.HanaClient, id, "close", new { rowVersion = Version(resolved) }));
    }

    private static MultipartFormDataContent Upload(string rowVersion, string fileName, string? visibility = null)
    {
        var form = new MultipartFormDataContent { { new StringContent(rowVersion), "rowVersion" } };
        if (visibility is not null)
        {
            form.Add(new StringContent(visibility), "visibility");
        }

        var file = new ByteArrayContent(EvidenceSamples.Pdf(2048));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return form;
    }

    // ---- Status codes per route group ----

    [IntegrationFact]
    public async Task The_routes_answer_401_403_404_409_and_400_as_documented()
    {
        var people = await SetupAsync();
        var submitted = await SubmittedAsync(people);
        var id = submitted.GetProperty("id").GetInt32();
        var draft = await OkAsync(
            await people.SamClient.PostAsJsonAsync("/api/grievances/my", new { categoryId = people.CategoryId, subject = Name("Draft"), isAnonymous = false }),
            HttpStatusCode.Created);

        using var anonymousClient = factory.CreateBrowserClient(out _);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync("/api/grievances/my")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await people.SamClient.GetAsync("/api/grievances/cases/query")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await people.SamClient.GetAsync($"/api/grievances/cases/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await people.HanaClient.GetAsync($"/api/grievances/cases/{draft.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await people.IvanClient.GetAsync($"/api/grievances/cases/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await people.HanaClient.GetAsync($"/api/grievances/my/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Step(people.HanaClient, id, "acknowledge", new { rowVersion = "nope", priority = "High" })).StatusCode);

        var current = Version(await OkAsync(await people.HanaClient.GetAsync($"/api/grievances/cases/{id}")));
        await OkAsync(await Step(people.HanaClient, id, "acknowledge", new { rowVersion = current, priority = "High" }));
        Assert.Equal(HttpStatusCode.Conflict, (await Step(people.HanaClient, id, "start-review", new { rowVersion = current })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Step(people.RitaClient, id, "reject", new { rowVersion = current, reason = "No." })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await people.RitaClient.GetAsync("/api/grievances/dashboard/hr")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await people.RitaClient.GetAsync("/api/grievances/reports/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await people.HanaClient.GetAsync("/api/grievances/dashboard/hr")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await people.HanaClient.GetAsync("/api/grievances/employees?search=Sam")).StatusCode);
    }

    // ---- Anonymous redaction ----

    [IntegrationFact]
    public async Task An_anonymous_submitter_is_on_no_answer_and_in_no_stored_row()
    {
        var people = await SetupAsync();
        var submitted = await SubmittedAsync(people, anonymous: true);
        var id = submitted.GetProperty("id").GetInt32();

        var detail = await OkAsync(await people.HanaClient.GetAsync($"/api/grievances/cases/{id}"));
        var history = await OkAsync(await people.HanaClient.GetAsync($"/api/grievances/cases/{id}/history"));
        var list = await OkAsync(await people.HanaClient.GetAsync("/api/grievances/cases/query?pageSize=200"));
        var report = await OkAsync(await people.HanaClient.GetAsync("/api/grievances/reports/cases?pageSize=200"));

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("submitter").ValueKind);
        Assert.Equal("Anonymous", detail.GetProperty("submitterLabel").GetString());
        foreach (var json in new[] { detail.ToString(), history.ToString(), list.ToString(), report.ToString() })
        {
            Assert.DoesNotContain("Samwise", json, StringComparison.Ordinal);
            Assert.DoesNotContain(people.Sam.EmployeeCode, json, StringComparison.Ordinal);
        }

        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.CaseSubmitter WHERE CaseId = @id AND EmployeeId IS NOT NULL", ("@id", id)));
        Assert.Equal(32, await Sql.ScalarAsync<int>("SELECT DATALENGTH(SubmitterPseudonym) FROM grv.CaseSubmitter WHERE CaseId = @id", ("@id", id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.StatusHistory WHERE CaseId = @id AND ActorRole = 'Submitter' AND (ActorUserId IS NOT NULL OR ActorEmployeeId IS NOT NULL)", ("@id", id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.[Case] WHERE Id = @id AND (CompanyId IS NOT NULL OR DepartmentId IS NOT NULL)", ("@id", id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.AuditLog WHERE CaseId = @id AND ActorRole = 'Submitter' AND ActorUserId IS NOT NULL", ("@id", id)));

        var mine = await OkAsync(await people.SamClient.GetAsync("/api/grievances/my?pageSize=50"));
        Assert.Contains(mine.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);
    }

    // ---- Files ----

    [IntegrationFact]
    public async Task A_submitters_file_is_shared_downloadable_and_leaves_the_case_row_version_alone()
    {
        var people = await SetupAsync();
        var submitted = await SubmittedAsync(people);
        var id = submitted.GetProperty("id").GetInt32();
        var pdf = EvidenceSamples.Pdf(2048);
        using var form = new MultipartFormDataContent { { new StringContent(Version(submitted)), "rowVersion" } };
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "rota.pdf");

        var uploaded = await OkAsync(await people.SamClient.PostAsync($"/api/grievances/my/{id}/attachments", form), HttpStatusCode.Created);

        Assert.Equal(Version(submitted), uploaded.GetProperty("caseRowVersion").GetString());
        var attachmentId = uploaded.GetProperty("attachment").GetProperty("id").GetInt32();
        Assert.Equal(pdf, await (await people.SamClient.GetAsync($"/api/grievances/my/attachments/{attachmentId}")).Content.ReadAsByteArrayAsync());
        Assert.Equal(pdf, await (await people.HanaClient.GetAsync($"/api/grievances/attachments/{attachmentId}")).Content.ReadAsByteArrayAsync());
        Assert.Equal("Shared", await Sql.ScalarAsync<string>("SELECT Visibility FROM grv.Attachment WHERE Id = @id", ("@id", attachmentId)));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM grv.Attachment WHERE Id = @id AND UploadedByUserId IS NULL", ("@id", attachmentId)));
        Assert.Single(Directory.GetFiles(factory.GrievanceFolder));
    }

    // ---- The database rules ----

    [IntegrationFact]
    public async Task The_append_only_tables_deny_update_and_delete_to_the_api_login()
    {
        foreach (var table in new[] { "grv.StatusHistory", "grv.InvestigationNote", "grv.Communication", "grv.AuditLog" })
        {
            var denied = await Sql.ScalarAsync<int>(
                """
                SELECT COUNT(*) FROM sys.database_permissions p
                JOIN sys.database_principals g ON g.principal_id = p.grantee_principal_id
                WHERE g.name = N'hrms_api' AND p.state_desc = 'DENY' AND p.permission_name IN (N'UPDATE', N'DELETE')
                  AND p.class_desc = 'OBJECT_OR_COLUMN' AND p.major_id = OBJECT_ID(@table);
                """,
                ("@table", table));
            Assert.True(denied == 2, table);
        }
    }

    [IntegrationFact]
    public async Task A_submitted_case_keeps_what_submission_fixed_and_a_closed_case_is_final()
    {
        var people = await SetupAsync();
        var submitted = await SubmittedAsync(people);
        var id = submitted.GetProperty("id").GetInt32();

        Assert.Equal(51101, await Sql.ErrorNumberOfAsync("UPDATE grv.[Case] SET IsAnonymous = 1, CompanyId = NULL, BranchId = NULL, DepartmentId = NULL, PositionId = NULL WHERE Id = @id", ("@id", id)));
        Assert.Equal(51101, await Sql.ErrorNumberOfAsync("BEGIN TRANSACTION; DELETE FROM grv.StatusHistory WHERE CaseId = @id; DELETE FROM grv.[Case] WHERE Id = @id; COMMIT TRANSACTION;", ("@id", id)));

        await Sql.ExecuteAsync(
            "UPDATE grv.[Case] SET Status = 'Resolved', ResolvedAtUtc = SYSUTCDATETIME(), ResolutionSummary = N'Fixed.', AcknowledgedAtUtc = SYSUTCDATETIME(), Priority = 'Low', TargetResolutionDate = '2026-12-31' WHERE Id = @id",
            ("@id", id));
        await Sql.ExecuteAsync("UPDATE grv.[Case] SET Status = 'Closed', ClosedAtUtc = SYSUTCDATETIME() WHERE Id = @id", ("@id", id));
        Assert.Equal(51101, await Sql.ErrorNumberOfAsync("UPDATE grv.[Case] SET Subject = N'Changed subject' WHERE Id = @id", ("@id", id)));
        Assert.Equal(0, await Sql.ErrorNumberOfAsync("UPDATE grv.[Case] SET LegalHold = 1 WHERE Id = @id", ("@id", id)));
        Assert.Equal(51101, await Sql.ErrorNumberOfAsync("UPDATE grv.[Case] SET ArchivedAtUtc = SYSUTCDATETIME() WHERE Id = @id", ("@id", id)));
    }
}
