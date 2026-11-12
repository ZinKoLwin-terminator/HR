using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.IntegrationTests.Support;
using ClosedXML.Excel;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// Excel import and export over HTTP against the test database: the upload goes through the real file checks and the real
/// handlers, inside one transaction. Every row carries the tag of the test (work email, employee code, organization codes,
/// file name), so the cleanup finds it.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ExcelHttpTests : DatabaseTest, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public ExcelHttpTests()
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

    private string FileName => $"{Tag}-employees.xlsx";

    private async Task<Dictionary<string, string>> RowAsync(
        (string Company, string Branch, string Department, string Position) codes,
        string suffix,
        params (string Header, string Value)[] changes)
    {
        await Task.CompletedTask;
        var row = new Dictionary<string, string>
        {
            ["Employee Code"] = Requests.EmployeeCode(suffix), ["First Name"] = "Mia", ["Last Name"] = "Importwell", ["Gender"] = "Female",
            ["Date of Birth"] = "1994-05-17", ["Marital Status"] = "Single", ["Nationality"] = "Myanmar", ["National ID"] = "NRC-TEST-000002",
            ["Work Email"] = Requests.WorkEmail("imp" + suffix), ["Mobile Phone"] = "+95 9 555 0101", ["Address Line 1"] = "12 Test Street",
            ["City"] = "Yangon", ["Country"] = "Myanmar", ["Emergency Contact Name"] = "Test Contact", ["Emergency Contact Relationship"] = "Sibling",
            ["Emergency Contact Phone"] = "+95 9 555 0102", ["Employment Type"] = "Full-time", ["Employment Status"] = "Active",
            ["Joining Date"] = "2024-03-01", ["Company Code"] = codes.Company, ["Branch Code"] = codes.Branch, ["Department Code"] = codes.Department,
            ["Position Code"] = codes.Position, ["Bank Name"] = "Sample Bank", ["Bank Account Holder"] = "Mia Importwell", ["Bank Account Number"] = "1234567890",
        };
        foreach (var (header, value) in changes)
        {
            row[header] = value;
        }

        return row;
    }

    private async Task<(string Company, string Branch, string Department, string Position)> SeedCodesAsync()
    {
        var org = await SeedOrganizationAsync();
        return (org.Company.Code, org.Branch.Code, org.Department.Code, org.Position.Code);
    }

    /// <summary>The downloaded template with the rows filled in below its header, as a user would do.</summary>
    private async Task<byte[]> WorkbookAsync(params Dictionary<string, string>[] rows)
    {
        var template = await client.GetByteArrayAsync("/api/excel/templates/employees");
        using var workbook = new XLWorkbook(new MemoryStream(template));
        var sheet = workbook.Worksheet("Employees");
        var headers = Enumerable.Range(1, sheet.LastColumnUsed()!.ColumnNumber()).Select(column => sheet.Cell(1, column).GetString()).ToList();
        for (var r = 0; r < rows.Length; r++)
        {
            foreach (var (header, value) in rows[r])
            {
                sheet.Cell(r + 2, headers.IndexOf(header) + 1).Value = value;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private Task<HttpResponseMessage> UploadAsync(byte[] bytes, string? fileName = null)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(ExcelContentTypes.Xlsx);
        content.Add(file, "file", fileName ?? FileName);
        return client.PostAsync("/api/excel/imports/employees/inspect", content);
    }

    private async Task<ImportPreviewDto> ValidateAsync(byte[] workbook, string strategy)
    {
        var inspect = await UploadAsync(workbook);
        Assert.True(inspect.StatusCode == HttpStatusCode.OK, await inspect.Content.ReadAsStringAsync());
        var inspection = (await inspect.Content.ReadFromJsonAsync<FileInspectionDto>(Json))!;
        var validate = await client.PostAsJsonAsync("/api/excel/imports/employees/validate", new { uploadId = inspection.UploadId, strategy });
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        return (await validate.Content.ReadFromJsonAsync<ImportPreviewDto>(Json))!;
    }

    private Task<HttpResponseMessage> ExecuteAsync(string batchId, string mode) =>
        client.PostAsJsonAsync($"/api/excel/imports/{batchId}/execute", new { mode });

    private async Task<int> EmployeesAsync() =>
        await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@p)", ("@p", Tag + "-%"));

    private async Task<int> BankAccountsAsync() =>
        await Sql.ScalarAsync<int>(
            "SELECT COUNT(*) FROM hr.EmployeeBankAccount WHERE EmployeeId IN (SELECT Id FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@p))",
            ("@p", Tag + "-%"));

    [IntegrationFact]
    public async Task The_catalog_and_the_template_describe_the_employee_module()
    {
        var catalog = (await client.GetFromJsonAsync<ExcelCatalogDto>("/api/excel/modules", Json))!;
        var template = await client.GetAsync("/api/excel/templates/employees");

        var employees = Assert.Single(catalog.Modules);
        Assert.Equal("employees", employees.Code);
        Assert.Equal("Employee Code", employees.Columns[0].Header);
        Assert.True(employees.Columns[0].Required);
        Assert.Contains(catalog.Unavailable, m => m.Code == "shifts");
        Assert.Equal(ExcelContentTypes.Xlsx, template.Content.Headers.ContentType!.MediaType);
        Assert.Equal("employees-import-template.xlsx", template.Content.Headers.ContentDisposition!.FileNameStar ?? template.Content.Headers.ContentDisposition.FileName);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/excel/templates/nothing")).StatusCode);
    }

    [IntegrationFact]
    public async Task Excel_routes_need_a_signed_in_user()
    {
        using var anonymous = factory.CreateBrowserClient(out _);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/excel/modules")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/excel/imports/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/excel/exports/employees", new { })).StatusCode);
    }

    [IntegrationFact]
    public async Task Valid_organization_rows_are_imported_in_one_transaction_and_recorded()
    {
        var codes = await SeedCodesAsync();
        var workbook = await WorkbookAsync(await RowAsync(codes, "1"), await RowAsync(codes, "2"));

        var preview = await ValidateAsync(workbook, "InsertOnly");

        Assert.All(preview.Rows, row => Assert.Equal(ImportRowStatus.Valid, row.Status));
        Assert.Equal([2, 3], preview.Rows.Select(r => r.RowNumber));
        Assert.Equal(0, await EmployeesAsync());

        var executed = await ExecuteAsync(preview.BatchId, "AllOrNothing");

        Assert.Equal(HttpStatusCode.OK, executed.StatusCode);
        var result = (await executed.Content.ReadFromJsonAsync<ImportResultDto>(Json))!;
        Assert.Equal(2, result.Inserted);
        Assert.False(result.RolledBack);
        Assert.Equal(2, await EmployeesAsync());
        Assert.Equal(2, await BankAccountsAsync());
        Assert.Equal(2, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.EmploymentHistory WHERE EmployeeId IN (SELECT Id FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@p))", ("@p", Tag + "-%")));

        // One activity entry for the batch, not one per row.
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Message LIKE @p", ("@p", "Excel import of Employees (" + Tag + "%")));

        var again = await ExecuteAsync(preview.BatchId, "AllOrNothing");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [IntegrationFact]
    public async Task A_file_with_row_errors_saves_nothing_and_the_error_report_lists_each_row()
    {
        var codes = await SeedCodesAsync();
        var workbook = await WorkbookAsync(
            await RowAsync(codes, "1"),
            await RowAsync(codes, "2", ("Gender", "Unknown")),
            await RowAsync(codes, "3", ("Department Code", "NOPE")),
            await RowAsync(codes, "4", ("Date of Birth", "2015-01-01")));

        var preview = await ValidateAsync(workbook, "InsertOnly");

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Contains("is not an allowed value", preview.Rows[1].Issues.Single().Message, StringComparison.Ordinal);
        Assert.Equal("Department Code", preview.Rows[2].Issues.Single().Field);
        Assert.Equal("Date of Birth", preview.Rows[3].Issues.Single().Field);
        Assert.Equal(0, await EmployeesAsync());

        var allOrNothing = await ExecuteAsync(preview.BatchId, "AllOrNothing");
        Assert.Equal(HttpStatusCode.Conflict, allOrNothing.StatusCode);
        Assert.Equal(0, await EmployeesAsync());

        var report = await client.GetAsync($"/api/excel/imports/{preview.BatchId}/error-report");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        using var book = new XLWorkbook(new MemoryStream(await report.Content.ReadAsByteArrayAsync()));
        var sheet = book.Worksheet("Validation report");
        Assert.Equal([3, 4, 5], Enumerable.Range(2, 3).Select(row => sheet.Cell(row, 1).GetValue<int>()));
        Assert.Equal("Department Code", sheet.Cell(3, 2).GetString());

        var partial = await ExecuteAsync(preview.BatchId, "Partial");
        var result = (await partial.Content.ReadFromJsonAsync<ImportResultDto>(Json))!;
        Assert.Equal(1, result.Inserted);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(1, await EmployeesAsync());
    }

    [IntegrationFact]
    public async Task A_row_that_fails_during_the_import_rolls_the_whole_batch_back()
    {
        var org = await SeedOrganizationAsync();
        var codes = (org.Company.Code, org.Branch.Code, org.Department.Code, org.Position.Code);
        var workbook = await WorkbookAsync(await RowAsync(codes, "1"), await RowAsync(codes, "2"));
        var preview = await ValidateAsync(workbook, "InsertOnly");
        Assert.All(preview.Rows, row => Assert.Equal(ImportRowStatus.Valid, row.Status));

        // Another session takes the work email of the second row after the preview: row 1 saves, row 2 is refused.
        Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "9", "imp2")));

        var executed = await ExecuteAsync(preview.BatchId, "AllOrNothing");

        var result = (await executed.Content.ReadFromJsonAsync<ImportResultDto>(Json))!;
        Assert.True(result.RolledBack);
        Assert.Equal(Apex.Hrms.Domain.Excel.ImportBatchStatus.Failed, result.Status);
        Assert.Equal(1, await EmployeesAsync());
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.Employee WHERE EmployeeCode = @c", ("@c", Requests.EmployeeCode("1"))));
        var detail = (await client.GetFromJsonAsync<ImportBatchDetailDto>($"/api/excel/imports/{preview.BatchId}", Json))!;
        Assert.Equal(Apex.Hrms.Domain.Excel.ImportBatchStatus.Failed, detail.Status);
        Assert.Contains(detail.Issues, issue => issue.RowNumber == 3);
    }

    [IntegrationFact]
    public async Task Update_only_skips_unknown_codes_and_keeps_blank_optional_cells()
    {
        var org = await SeedOrganizationAsync();
        var codes = (org.Company.Code, org.Branch.Code, org.Department.Code, org.Position.Code);
        var existing = await SeedEmployeeAsync(org, "1", "imp1");
        var workbook = await WorkbookAsync(
            await RowAsync(codes, "1", ("First Name", "Maya")),
            await RowAsync(codes, "7"));

        var preview = await ValidateAsync(workbook, "UpdateOnly");

        Assert.Equal(ImportRowAction.Update, preview.Rows[0].Action);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[1].Status);
        Assert.Contains("cannot be updated", preview.Rows[1].Issues.Single().Message, StringComparison.Ordinal);

        var result = (await (await ExecuteAsync(preview.BatchId, "Partial")).Content.ReadFromJsonAsync<ImportResultDto>(Json))!;

        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, await EmployeesAsync());
        var loaded = Succeeded(await Get<Apex.Hrms.Application.Employees.EmployeeDto>(existing.Id));
        Assert.Equal("Maya", loaded.FirstName);
    }

    [IntegrationFact]
    public async Task Exporting_and_importing_the_same_rows_again_changes_nothing()
    {
        var codes = await SeedCodesAsync();
        var first = await ValidateAsync(await WorkbookAsync(await RowAsync(codes, "1"), await RowAsync(codes, "2")), "InsertOnly");
        Assert.Equal(HttpStatusCode.OK, (await ExecuteAsync(first.BatchId, "AllOrNothing")).StatusCode);
        var before = await Sql.ScalarAsync<string>(
            "SELECT STRING_AGG(CONVERT(varchar(32), e.RowVersion, 1) + ISNULL(CONVERT(varchar(32), b.RowVersion, 1), ''), ',') WITHIN GROUP (ORDER BY e.Id) FROM hr.Employee e LEFT JOIN hr.EmployeeBankAccount b ON b.EmployeeId = e.Id WHERE LOWER(e.WorkEmail) LIKE LOWER(@p)",
            ("@p", Tag + "-%"));

        var export = await client.PostAsJsonAsync(
            "/api/excel/exports/employees",
            new { scope = "filtered", search = Tag, layout = "template", includeSensitive = true });

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var exported = await export.Content.ReadAsByteArrayAsync();
        using (var book = new XLWorkbook(new MemoryStream(exported)))
        {
            Assert.Equal(3, book.Worksheet("Employees").LastRowUsed()!.RowNumber());
        }

        var again = await ValidateAsync(exported, "InsertOrUpdate");
        Assert.All(again.Rows, row =>
        {
            Assert.Equal(ImportRowStatus.Valid, row.Status);
            Assert.Equal(ImportRowAction.Update, row.Action);
        });
        Assert.Equal(HttpStatusCode.OK, (await ExecuteAsync(again.BatchId, "AllOrNothing")).StatusCode);

        var after = await Sql.ScalarAsync<string>(
            "SELECT STRING_AGG(CONVERT(varchar(32), e.RowVersion, 1) + ISNULL(CONVERT(varchar(32), b.RowVersion, 1), ''), ',') WITHIN GROUP (ORDER BY e.Id) FROM hr.Employee e LEFT JOIN hr.EmployeeBankAccount b ON b.EmployeeId = e.Id WHERE LOWER(e.WorkEmail) LIKE LOWER(@p)",
            ("@p", Tag + "-%"));
        Assert.Equal(before, after);
        Assert.Equal(2, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM hr.EmploymentHistory WHERE EmployeeId IN (SELECT Id FROM hr.Employee WHERE LOWER(WorkEmail) LIKE LOWER(@p))", ("@p", Tag + "-%")));
    }

    [IntegrationFact]
    public async Task The_report_export_leaves_out_sensitive_columns_unless_asked_and_filters_like_the_list()
    {
        var codes = await SeedCodesAsync();
        var preview = await ValidateAsync(await WorkbookAsync(await RowAsync(codes, "1"), await RowAsync(codes, "2", ("First Name", "Zed"))), "InsertOnly");
        await ExecuteAsync(preview.BatchId, "AllOrNothing");

        var plain = await client.PostAsJsonAsync("/api/excel/exports/employees", new { scope = "filtered", search = Tag });
        var sensitive = await client.PostAsJsonAsync("/api/excel/exports/employees", new { scope = "filtered", search = Tag, includeSensitive = true });
        var one = await client.PostAsJsonAsync("/api/excel/exports/employees", new { scope = "filtered", search = "Zed Importwell" });

        using var plainBook = new XLWorkbook(new MemoryStream(await plain.Content.ReadAsByteArrayAsync()));
        var headers = Enumerable.Range(1, plainBook.Worksheet("Employees").LastColumnUsed()!.ColumnNumber()).Select(c => plainBook.Worksheet("Employees").Cell(1, c).GetString()).ToList();
        Assert.Contains("Employee Name", headers);
        Assert.DoesNotContain("National ID", headers);
        Assert.DoesNotContain("Bank Account Number", headers);
        Assert.Contains("Department Summary", plainBook.Worksheets.Select(s => s.Name));
        using var sensitiveBook = new XLWorkbook(new MemoryStream(await sensitive.Content.ReadAsByteArrayAsync()));
        Assert.Contains(Enumerable.Range(1, sensitiveBook.Worksheet("Employees").LastColumnUsed()!.ColumnNumber()), c => sensitiveBook.Worksheet("Employees").Cell(1, c).GetString() == "National ID");
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
        using var oneBook = new XLWorkbook(new MemoryStream(await one.Content.ReadAsByteArrayAsync()));
        Assert.Equal(2, oneBook.Worksheet("Employees").LastRowUsed()!.RowNumber());

        var badRange = await client.PostAsJsonAsync("/api/excel/exports/employees", new { dateFrom = "2026-02-01", dateTo = "2026-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
    }

    [IntegrationFact]
    public async Task Uploads_that_are_not_excel_workbooks_are_refused_with_the_message_of_the_mock()
    {
        var csv = await UploadAsync([.. "a,b"u8], $"{Tag}-employees.csv");
        var fake = await UploadAsync([.. "this is not a zip file"u8]);
        var empty = await UploadAsync([]);

        Assert.Equal(HttpStatusCode.BadRequest, csv.StatusCode);
        Assert.Contains("Only Excel workbooks (.xlsx)", await csv.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("not a valid Excel workbook", await fake.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var missingHeader = await UploadAsync(await WorkbookWithoutLastNameAsync());
        var inspection = (await missingHeader.Content.ReadFromJsonAsync<FileInspectionDto>(Json))!;
        Assert.True(inspection.NeedsMapping);
        Assert.Contains("lastName", inspection.MissingRequired);
    }

    private async Task<byte[]> WorkbookWithoutLastNameAsync()
    {
        var codes = await SeedCodesAsync();
        var bytes = await WorkbookAsync(await RowAsync(codes, "1"));
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheet("Employees");
        sheet.Cell(1, 3).Value = "Surname";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    [IntegrationFact]
    public async Task Another_users_batch_is_not_visible_in_history_detail_or_error_report_but_the_administrator_sees_it()
    {
        var codes = await SeedCodesAsync();
        var preview = await ValidateAsync(await WorkbookAsync(await RowAsync(codes, "1", ("Gender", "Unknown"))), "InsertOnly");
        var name = $"{Tag}-Viewer";
        await Sql.ExecuteAsync("INSERT INTO sec.Role (Name, IsSystem, Status) VALUES (@name, 0, 'Active');", ("@name", name));
        var roleId = await Sql.ScalarAsync<int>("SELECT Id FROM sec.Role WHERE Name = @name", ("@name", name));
        foreach (var grant in new[] { ("employees", "View"), ("excel", "View") })
        {
            await Sql.ExecuteAsync(
                "INSERT INTO sec.RolePermission (RoleId, ModuleKey, Action) VALUES (@role, @module, @action);",
                ("@role", roleId),
                ("@module", grant.Item1),
                ("@action", grant.Item2));
        }

        var other = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "viewer"), roleName: name);
        using var viewer = factory.CreateBrowserClient(out _);
        await viewer.SignInAsync(other);

        var history = (await viewer.GetFromJsonAsync<PagedResult<ImportBatchSummaryDto>>("/api/excel/imports/history?pageIndex=0&pageSize=50", Json))!;
        Assert.DoesNotContain(history.Items, item => item.BatchId == preview.BatchId);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/excel/imports/{preview.BatchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/excel/imports/{preview.BatchId}/error-report")).StatusCode);

        var adminHistory = (await client.GetFromJsonAsync<PagedResult<ImportBatchSummaryDto>>("/api/excel/imports/history?pageIndex=0&pageSize=50", Json))!;
        Assert.Contains(adminHistory.Items, item => item.BatchId == preview.BatchId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/excel/imports/{preview.BatchId}/error-report")).StatusCode);
    }

    [IntegrationFact]
    public async Task History_lists_the_batches_newest_first_with_the_uploader()
    {
        var codes = await SeedCodesAsync();
        var preview = await ValidateAsync(await WorkbookAsync(await RowAsync(codes, "1", ("Gender", "Unknown"))), "InsertOnly");

        var page = (await client.GetFromJsonAsync<PagedResult<ImportBatchSummaryDto>>("/api/excel/imports/history?pageIndex=0&pageSize=5", Json))!;
        var detail = (await client.GetFromJsonAsync<ImportBatchDetailDto>($"/api/excel/imports/{preview.BatchId}", Json))!;

        Assert.Equal(preview.BatchId, page.Items[0].BatchId);
        Assert.Equal(FileName, page.Items[0].FileName);
        Assert.Equal("TST- Test Person", page.Items[0].UploadedBy);
        Assert.Equal(Apex.Hrms.Domain.Excel.ImportBatchStatus.Validated, page.Items[0].Status);
        Assert.Equal(1, detail.InvalidRows);
        Assert.Single(detail.Issues);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/excel/imports/{Guid.NewGuid()}")).StatusCode);
    }
}
