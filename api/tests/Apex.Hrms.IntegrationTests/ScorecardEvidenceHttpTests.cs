using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.IntegrationTests.Support;
using Apex.Hrms.TestSupport;
using Org = (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position);

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// KPI evidence files over HTTP against the test database and a temporary evidence folder (removed with the host): a real multipart
/// upload, the download byte for byte with its headers, the removal of the row and the file, who may do what (the owner uploads and
/// removes; the owner, the reviewer and HR download; an unrelated employee gets 404), a wrong signature that stores nothing, the limit
/// per KPI, the antiforgery header and the CHECK constraints of <c>kpi.Attachment</c>.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ScorecardEvidenceHttpTests : DatabaseTest, IDisposable
{
    private static readonly string[] EmployeeGrants = ["kpi:View", "kpi:Create"];
    private static readonly string[] ManagerGrants = ["kpi:View", "kpi:Create", "kpi:Approve"];
    private static readonly string[] HrGrants = ["kpi:View", "kpi:Create", "kpi:Approve", "employees:View", "employees:Edit"];

    private readonly ApiFactory factory = new();
    private readonly HttpClient admin;
    private readonly List<HttpClient> others = [];

    public ScorecardEvidenceHttpTests()
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

    private sealed record Setup(EmployeeDto Anna, EmployeeDto Ben, EmployeeDto Zed, ScorecardDetailDto Card, int FirstItem, int SecondItem);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private async Task<HttpClient> SignedInAsync(string suffix, int? employeeId, params string[] grants)
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

        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, $"{suffix}-login"), roleName: role, employeeId: employeeId);
        var signedIn = factory.CreateBrowserClient(out _);
        others.Add(signedIn);
        await signedIn.SignInAsync(account);
        return signedIn;
    }

    private async Task<EmployeeDto> EmployeeAsync(Org org, string suffix, string local, int? managerId = null) =>
        Succeeded(await Create(
            Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, suffix, local) with { ManagerId = managerId }));

    /// <summary>Anna manages Ben; Zed manages nobody and is unrelated to Ben. Ben's scorecard (two KPIs) is in Draft in an Active period.</summary>
    private async Task<Setup> SetupAsync()
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var first = await SeedKpiTemplateAsync(category.Id, "E-01", "Monthly sales");
        var second = await SeedKpiTemplateAsync(category.Id, "E-02", "Quality score");
        var draft = Succeeded(await Create(Requests.Period(org.Company.Id)));
        var period = Succeeded(await RunCommand<ActivatePeriodCommand, EvaluationPeriodDto>(new ActivatePeriodCommand(draft.Id, Requests.Version(draft.RowVersion))));
        var anna = await EmployeeAsync(org, "1", "anna");
        var ben = await EmployeeAsync(org, "2", "ben", anna.Id);
        var zed = await EmployeeAsync(org, "3", "zed");
        var card = Succeeded(await FinalizePlan(await SeedScorecardAsync(ben.Id, period.Id, ItemOf(first.Id, weight: 60m), ItemOf(second.Id, weight: 40m))));
        return new Setup(anna, ben, zed, card, card.Items[0].Id, card.Items[1].Id);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient http, int itemId, byte[] content, string fileName, string? rowVersion)
    {
        var form = new MultipartFormDataContent();
        if (rowVersion is not null)
        {
            form.Add(new StringContent(rowVersion), "rowVersion");
        }

        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return http.PostAsync($"/api/kpi/scorecard-items/{itemId}/attachments", form);
    }

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient http, int attachmentId, string? rowVersion) =>
        http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/kpi/attachments/{attachmentId}") { Content = JsonContent.Create(new { rowVersion }) });

    private static async Task<string> RowVersionOfAsync(HttpClient http, int scorecardId) =>
        (await ReadAsync(await http.GetAsync($"/api/kpi/scorecards/{scorecardId}"))).GetProperty("rowVersion").GetString()!;

    private string[] StoredFiles() => Directory.Exists(factory.EvidenceFolder) ? Directory.GetFiles(factory.EvidenceFolder) : [];

    private static async Task<JsonElement> UploadedAsync(HttpResponseMessage response)
    {
        var json = await ReadAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {json}");
        return json;
    }

    // ---- The whole path ----

    [IntegrationFact]
    public async Task The_owner_uploads_downloads_and_removes_a_file_and_the_row_the_file_and_the_row_version_follow()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var pdf = EvidenceSamples.Pdf(4096);
        var before = Requests.Version(setup.Card.RowVersion);

        var response = await UploadAsync(ben, setup.FirstItem, pdf, "Q4 résumé.pdf", before);
        var created = await UploadedAsync(response);

        var attachment = created.GetProperty("attachment");
        var id = attachment.GetProperty("id").GetInt32();
        Assert.Equal($"/api/kpi/attachments/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(["contentType", "fileName", "id", "sizeBytes", "uploadedOn"], attachment.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("Q4 résumé.pdf", "application/pdf", 4096), (attachment.GetProperty("fileName").GetString(), attachment.GetProperty("contentType").GetString(), attachment.GetProperty("sizeBytes").GetInt32()));
        var afterUpload = created.GetProperty("scorecardRowVersion").GetString()!;
        Assert.NotEqual(before, afterUpload);
        Assert.Equal(afterUpload, await RowVersionOfAsync(ben, setup.Card.Id));

        var key = (await Sql.ScalarAsync<string>("SELECT StorageKey FROM kpi.Attachment WHERE Id = @id", ("@id", id))).Trim();
        Assert.Matches("^[0-9a-f]{32}$", key);
        Assert.Equal(Path.Combine(factory.EvidenceFolder, key + ".bin"), Assert.Single(StoredFiles()));
        Assert.Equal(pdf, await File.ReadAllBytesAsync(Path.Combine(factory.EvidenceFolder, key + ".bin")));

        var detail = await ReadAsync(await ben.GetAsync($"/api/kpi/scorecards/{setup.Card.Id}"));
        var items = detail.GetProperty("items").EnumerateArray().ToList();
        var listed = Assert.Single(items.Single(item => item.GetProperty("id").GetInt32() == setup.FirstItem).GetProperty("attachments").EnumerateArray());
        Assert.Equal(id, listed.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Array, items.Single(item => item.GetProperty("id").GetInt32() == setup.SecondItem).GetProperty("attachments").ValueKind);
        Assert.DoesNotContain(key, detail.ToString(), StringComparison.Ordinal);

        var download = await ben.GetAsync($"/api/kpi/attachments/{id}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Q4 résumé.pdf", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Contains("nosniff", download.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.False(download.Content.Headers.ContentDisposition?.DispositionType.Equals("inline", StringComparison.OrdinalIgnoreCase));

        var removed = await RemoveAsync(ben, id, afterUpload);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var afterRemove = (await ReadAsync(removed)).GetProperty("scorecardRowVersion").GetString()!;
        Assert.NotEqual(afterUpload, afterRemove);
        Assert.Equal(afterRemove, await RowVersionOfAsync(ben, setup.Card.Id));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Attachment WHERE Id = @id", ("@id", id)));
        Assert.Empty(StoredFiles());
        Assert.Equal(HttpStatusCode.NotFound, (await ben.GetAsync($"/api/kpi/attachments/{id}")).StatusCode);
    }

    [IntegrationFact]
    public async Task The_reviewer_and_hr_download_it_but_cannot_add_or_remove_and_an_unrelated_employee_gets_404()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var zed = await SignedInAsync("Zed", setup.Zed.Id, EmployeeGrants);
        var hr = await SignedInAsync("Hr", null, HrGrants);
        var docx = EvidenceSamples.Docx();
        var created = await UploadedAsync(await UploadAsync(ben, setup.FirstItem, docx, "plan.docx", Requests.Version(setup.Card.RowVersion)));
        var id = created.GetProperty("attachment").GetProperty("id").GetInt32();
        var version = created.GetProperty("scorecardRowVersion").GetString()!;

        foreach (var reader in new[] { anna, hr })
        {
            var download = await reader.GetAsync($"/api/kpi/attachments/{id}");
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(docx, await download.Content.ReadAsByteArrayAsync());
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", download.Content.Headers.ContentType?.MediaType);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await zed.GetAsync($"/api/kpi/attachments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(zed, setup.FirstItem, EvidenceSamples.Pdf(), "x.pdf", version)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RemoveAsync(zed, id, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(anna, setup.FirstItem, EvidenceSamples.Pdf(), "x.pdf", version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RemoveAsync(anna, id, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(hr, setup.FirstItem, EvidenceSamples.Pdf(), "x.pdf", version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RemoveAsync(hr, id, version)).StatusCode);
        Assert.Single(StoredFiles());
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Attachment WHERE Id = @id", ("@id", id)));
    }

    [IntegrationFact]
    public async Task A_file_that_is_not_what_its_name_says_is_a_400_on_file_and_nothing_is_stored()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var version = Requests.Version(setup.Card.RowVersion);

        foreach (var (content, name) in new (byte[], string)[]
        {
            (EvidenceSamples.Executable(), "report.pdf"),
            (EvidenceSamples.Html(), "page.pdf"),
            (EvidenceSamples.Svg(), "logo.png"),
            (EvidenceSamples.Zip("a.txt"), "notes.docx"),
            (EvidenceSamples.Pdf(), "report.png"),
            ([], "empty.pdf"),
        })
        {
            var response = await UploadAsync(ben, setup.FirstItem, content, name, version);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await ReadAsync(response)).GetProperty("errors").TryGetProperty("file", out _), name);
        }

        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Attachment WHERE ScorecardItemId = @id", ("@id", setup.FirstItem)));
        Assert.Empty(StoredFiles());
        Assert.Equal(version, await RowVersionOfAsync(ben, setup.Card.Id));
    }

    [IntegrationFact]
    public async Task A_client_file_name_with_a_path_is_cleaned_and_a_missing_or_stale_row_version_stores_nothing()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var version = Requests.Version(setup.Card.RowVersion);

        var missing = await UploadAsync(ben, setup.FirstItem, EvidenceSamples.Pdf(), "a.pdf", rowVersion: null);
        var garbage = await UploadAsync(ben, setup.FirstItem, EvidenceSamples.Pdf(), "a.pdf", "not-a-version");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.True((await ReadAsync(missing)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);

        var created = await UploadedAsync(await UploadAsync(ben, setup.FirstItem, EvidenceSamples.Pdf(), @"..\..\secret\x.pdf", version));
        Assert.Equal("x.pdf", created.GetProperty("attachment").GetProperty("fileName").GetString());

        var stale = await UploadAsync(ben, setup.FirstItem, EvidenceSamples.Pdf(), "again.pdf", version);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Single(StoredFiles());
    }

    [IntegrationFact]
    public async Task A_KPI_takes_five_files_and_the_sixth_is_a_409_and_a_scorecard_after_submission_refuses_changes()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var version = Requests.Version(setup.Card.RowVersion);
        int lastId = 0;
        for (var count = 1; count <= 5; count++)
        {
            var created = await UploadedAsync(await UploadAsync(ben, setup.FirstItem, EvidenceSamples.Png(), $"f{count}.png", version));
            version = created.GetProperty("scorecardRowVersion").GetString()!;
            lastId = created.GetProperty("attachment").GetProperty("id").GetInt32();
        }

        var sixth = await UploadAsync(ben, setup.FirstItem, EvidenceSamples.Png(), "f6.png", version);
        var other = await UploadAsync(ben, setup.SecondItem, EvidenceSamples.Png(), "g1.png", version);

        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        Assert.Equal(6, StoredFiles().Length);

        var filled = await ReadAsync(await ben.PutAsJsonAsync($"/api/kpi/scorecards/{setup.Card.Id}/results", new
        {
            rowVersion = await RowVersionOfAsync(ben, setup.Card.Id),
            items = new[] { new { id = setup.FirstItem, actualValue = (decimal?)100m }, new { id = setup.SecondItem, actualValue = (decimal?)50m } },
        }));
        var submitted = await ReadAsync(await ben.PostAsJsonAsync($"/api/kpi/scorecards/{setup.Card.Id}/submit", new { rowVersion = filled.GetProperty("rowVersion").GetString() }));
        var current = submitted.GetProperty("rowVersion").GetString()!;

        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(ben, setup.SecondItem, EvidenceSamples.Png(), "late.png", current)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await RemoveAsync(ben, lastId, current)).StatusCode);
        Assert.Equal(6, StoredFiles().Length);
    }

    [IntegrationFact]
    public async Task An_upload_without_the_antiforgery_header_is_refused_and_stores_nothing()
    {
        var setup = await SetupAsync();
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "plain-login"), roleName: await RoleWithAsync("Plain", EmployeeGrants), employeeId: setup.Ben.Id);
        using var browser = factory.CreateBrowserClient(out var signedIn);
        await browser.SignInAsync(account);
        using var noHeader = factory.CreateBrowserClient(out var plain, sendXsrfHeader: false);
        foreach (System.Net.Cookie cookie in signedIn.Cookies.GetAllCookies())
        {
            plain.Cookies.Add(cookie);
        }

        var response = await UploadAsync(noHeader, setup.FirstItem, EvidenceSamples.Pdf(), "a.pdf", Requests.Version(setup.Card.RowVersion));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest, $"{(int)response.StatusCode}");
        Assert.Empty(StoredFiles());
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Attachment WHERE ScorecardItemId = @id", ("@id", setup.FirstItem)));
    }

    private async Task<string> RoleWithAsync(string suffix, string[] grants)
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

        return role;
    }

    // ---- The CHECK constraints of kpi.Attachment ----

    private const string InsertSql =
        "INSERT INTO kpi.Attachment (ScorecardItemId, FileName, ContentType, SizeBytes, StorageKey, UploadedOn) VALUES (@item, N'evidence.pdf', 'application/pdf', @size, @key, '2026-11-09');";

    [IntegrationFact]
    public async Task The_size_and_storage_key_checks_of_the_attachment_table_hold()
    {
        var setup = await SetupAsync();
        var item = ("@item", (object?)setup.FirstItem);
        var key = Guid.NewGuid().ToString("N");

        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 0L), ("@key", key)));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", -1L), ("@key", key)));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 10_485_761L), ("@key", key)));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 1L), ("@key", "ABCDEF0123456789ABCDEF0123456789")));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 1L), ("@key", new string('g', 32))));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 1L), ("@key", "..\\..\\" + key[..25])));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 1L), ("@key", key[..31] + "-")));
        Assert.Equal(0, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 1L), ("@key", key)));
        Assert.Equal(0, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 10_485_760L), ("@key", Guid.NewGuid().ToString("N"))));
        Assert.Equal(2627, await Sql.ErrorNumberOfAsync(InsertSql, item, ("@size", 5L), ("@key", key)));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(InsertSql, ("@item", 2_000_000_000), ("@size", 5L), ("@key", Guid.NewGuid().ToString("N"))));
    }

    [IntegrationFact]
    public async Task Deleting_an_item_with_files_removes_their_rows_by_cascade()
    {
        var setup = await SetupAsync();
        Assert.Equal(0, await Sql.ErrorNumberOfAsync(InsertSql, ("@item", setup.FirstItem), ("@size", 5L), ("@key", Guid.NewGuid().ToString("N"))));

        await Sql.ExecuteAsync("DELETE FROM kpi.ScorecardItem WHERE Id = @item;", ("@item", setup.FirstItem));

        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.Attachment WHERE ScorecardItemId = @item", ("@item", setup.FirstItem)));
    }
}
