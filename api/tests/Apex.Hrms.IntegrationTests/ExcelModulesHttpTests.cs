using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Attendance.Records;
using Apex.Hrms.Application.Attendance.Shifts;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.IntegrationTests.Support;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The Excel modules added after employees (organization, leave, attendance, payroll) over HTTP against the test database. Each
/// import goes through the real file checks and handlers; every record and file carries the tag of the test, so the cleanup finds it.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ExcelModulesHttpTests : PayrollTestBase, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ApiFactory factory = new();
    private readonly List<HttpClient> clients = [];
    private readonly HttpClient admin;

    public ExcelModulesHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        admin = factory.CreateBrowserClient(out _);
        clients.Add(admin);
    }

    public override Task InitializeAsync() => admin.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        foreach (var client in clients)
        {
            client.Dispose();
        }

        factory.Dispose();
    }

    public void Dispose() => Cleanup();

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

        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, $"{suffix}-login"), roleName: role, employeeId: employeeId);
        var client = factory.CreateBrowserClient(out _);
        clients.Add(client);
        await client.SignInAsync(account);
        return client;
    }

    // ---- Workbooks ----

    private string FileName(string module) => $"{Tag}-{module}.xlsx";

    /// <summary>The template of the module with the rows filled in below its header (cells by header text).</summary>
    private async Task<byte[]> WorkbookAsync(string module, params Dictionary<string, string>[] rows)
    {
        var template = await admin.GetByteArrayAsync($"/api/excel/templates/{module}");
        var catalog = (await admin.GetFromJsonAsync<ExcelCatalogDto>("/api/excel/modules", Json))!;
        using var workbook = new XLWorkbook(new MemoryStream(template));
        var sheet = workbook.Worksheet(catalog.Modules.Single(m => m.Code == module).SheetName);
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

    private async Task<ImportPreviewDto> ValidateAsync(HttpClient client, string module, byte[] bytes, string strategy)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(ExcelContentTypes.Xlsx);
        content.Add(file, "file", FileName(module));
        var inspect = await client.PostAsync($"/api/excel/imports/{module}/inspect", content);
        Assert.True(inspect.StatusCode == HttpStatusCode.OK, await inspect.Content.ReadAsStringAsync());
        var inspection = (await inspect.Content.ReadFromJsonAsync<FileInspectionDto>(Json))!;
        var validate = await client.PostAsJsonAsync($"/api/excel/imports/{module}/validate", new { uploadId = inspection.UploadId, strategy });
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        return (await validate.Content.ReadFromJsonAsync<ImportPreviewDto>(Json))!;
    }

    private static async Task<ImportResultDto> ExecuteAsync(HttpClient client, ImportPreviewDto preview, string mode)
    {
        var response = await client.PostAsJsonAsync($"/api/excel/imports/{preview.BatchId}/execute", new { mode });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportResultDto>(Json))!;
    }

    private static async Task<List<string>> HeadersAsync(HttpResponseMessage response)
    {
        using var book = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var sheet = book.Worksheets.First();
        return [.. Enumerable.Range(1, sheet.LastColumnUsed()!.ColumnNumber()).Select(c => sheet.Cell(1, c).GetString())];
    }

    // ---- Catalog ----

    [IntegrationFact]
    public async Task The_catalog_lists_every_module_and_the_flags_follow_the_grants_of_the_caller()
    {
        var all = (await admin.GetFromJsonAsync<ExcelCatalogDto>("/api/excel/modules", Json))!;
        var viewer = await SignedInAsRoleAsync("viewer", null, "excel:View", "excel:Export", "leave:View", "payroll:View");
        var limited = (await viewer.GetFromJsonAsync<ExcelCatalogDto>("/api/excel/modules", Json))!;

        string[] codes = ["employees", "companies", "branches", "departments", "sections", "positions", "job-grades", "leave-applications", "leave-balances", "attendance-records", "shift-assignments", "salary-assignments"];
        Assert.Equal(codes.Order(StringComparer.Ordinal), all.Modules.Select(m => m.Code).Order(StringComparer.Ordinal));
        Assert.All(all.Modules.Where(m => m.Code is not ("leave-applications" or "leave-balances" or "attendance-records")), m => Assert.True(m.CanImport && m.CanExport, m.Code));
        Assert.All(all.Modules.Where(m => m.Code is "leave-applications" or "leave-balances" or "attendance-records"), m => Assert.False(m.CanImport, m.Code));
        Assert.True(limited.Modules.Single(m => m.Code == "leave-applications").CanExport);
        Assert.False(limited.Modules.Single(m => m.Code == "salary-assignments").CanExport);
        Assert.False(limited.Modules.Single(m => m.Code == "companies").CanExport);
        Assert.All(limited.Modules, m => Assert.False(m.CanImport, m.Code));
    }

    // ---- Organization ----

    [IntegrationFact]
    public async Task Organization_files_import_in_order_and_an_export_of_the_template_layout_imports_again_without_changes()
    {
        var company = Code("CO1");
        var companies = await ValidateAsync(
            admin,
            "companies",
            await WorkbookAsync("companies", new Dictionary<string, string>
            {
                ["Company Code"] = company, ["Company Name"] = Name("Company"), ["Legal Name"] = Name("Company Ltd"), ["Industry"] = "Trading", ["Email"] = "info@example.test",
                ["Phone"] = "+95 1 555 0100", ["City"] = "Yangon", ["Country"] = "Myanmar", ["Founded Date"] = "2020-01-01",
            }),
            "InsertOnly");
        Assert.Equal(1, (await ExecuteAsync(admin, companies, "AllOrNothing")).Inserted);

        var branch = Code("BR1");
        var department = Code("DP1");
        var grade = Code("GR1");
        await RunAsync("branches", new Dictionary<string, string>
        {
            ["Branch Code"] = branch, ["Branch Name"] = Name("Branch"), ["Company Code"] = company, ["Branch Type"] = "Branch", ["City"] = "Yangon", ["Country"] = "Myanmar", ["Opened Date"] = "2021-01-01",
        });
        await RunAsync("departments", new Dictionary<string, string>
        {
            ["Department Code"] = department, ["Department Name"] = Name("Department"), ["Company Code"] = company, ["Branch Code"] = branch,
        });
        await RunAsync("sections", new Dictionary<string, string>
        {
            ["Section Code"] = Code("SC1"), ["Section Name"] = Name("Section"), ["Department Code"] = department,
        });
        await RunAsync("job-grades", new Dictionary<string, string>
        {
            ["Grade Code"] = grade, ["Grade Name"] = Name("Grade"), ["Level"] = "2", ["Minimum Salary"] = "100", ["Maximum Salary"] = "200",
        });
        await RunAsync("positions", new Dictionary<string, string>
        {
            ["Position Code"] = Code("PS1"), ["Position Title"] = Name("Position"), ["Department Code"] = department, ["Job Grade Code"] = grade, ["Headcount"] = "2",
        });

        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Position WHERE Code = @c AND Headcount = 2", ("@c", Code("PS1"))));
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Department d JOIN org.Branch b ON b.Id = d.BranchId WHERE d.Code = @d AND b.Code = @b", ("@d", department), ("@b", branch)));

        var export = await admin.PostAsJsonAsync("/api/excel/exports/branches", new { scope = "filtered", search = Tag, layout = "template" });
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var exported = await export.Content.ReadAsByteArrayAsync();
        var again = await ValidateAsync(admin, "branches", exported, "InsertOrUpdate");
        Assert.All(again.Rows, row => Assert.Equal(ImportRowStatus.Valid, row.Status));
        Assert.All(again.Rows, row => Assert.Equal(ImportRowAction.Update, row.Action));
    }

    private async Task RunAsync(string module, Dictionary<string, string> row)
    {
        var preview = await ValidateAsync(admin, module, await WorkbookAsync(module, row), "InsertOnly");
        Assert.True(preview.Rows.All(r => r.Status == ImportRowStatus.Valid), string.Join("; ", preview.Rows.SelectMany(r => r.Issues).Select(i => $"{i.Field}: {i.Message}")));
        Assert.Equal(1, (await ExecuteAsync(admin, preview, "AllOrNothing")).Inserted);
    }

    [IntegrationFact]
    public async Task Organization_rows_with_problems_are_refused_with_the_messages_of_the_api_and_a_role_without_create_cannot_import()
    {
        var org = await SeedOrganizationAsync();
        var bad = await ValidateAsync(
            admin,
            "branches",
            await WorkbookAsync(
                "branches",
                new Dictionary<string, string> { ["Branch Code"] = Code("BR2"), ["Branch Name"] = Name("Branch 2"), ["Company Code"] = "NOPE", ["Branch Type"] = "Branch", ["City"] = "Yangon", ["Country"] = "Myanmar", ["Opened Date"] = "2021-01-01" },
                new Dictionary<string, string> { ["Branch Code"] = org.Branch.Code, ["Branch Name"] = Name("Branch 3"), ["Company Code"] = org.Company.Code, ["Branch Type"] = "Branch", ["City"] = "Yangon", ["Country"] = "Myanmar", ["Opened Date"] = "2021-01-01" }),
            "InsertOnly");

        Assert.Equal("Company \"NOPE\" does not exist.", bad.Rows[0].Issues.Single().Message);
        Assert.Equal(ImportRowStatus.Duplicate, bad.Rows[1].Status);
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM org.Branch WHERE Code = @c", ("@c", Code("BR2"))));

        var editor = await SignedInAsRoleAsync("editor", null, "excel:View", "excel:Import", "organization:View", "organization:Create");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/excel/templates/branches")).StatusCode);
    }

    // ---- Leave and attendance ----

    [IntegrationFact]
    public async Task Leave_exports_show_only_the_rows_of_the_callers_tier_and_the_modules_refuse_an_upload()
    {
        var org = await SeedOrganizationAsync();
        var mia = await SeedEmployeeAsync(org, "1", "mia");
        var noah = await SeedEmployeeAsync(org, "2", "noah");
        var type = await SeedLeaveTypeAsync();
        var monday = new DateOnly(2026, 11, 16);
        await SeedSubmittedLeaveAsync(mia.Id, type.Id, monday);
        await SeedSubmittedLeaveAsync(noah.Id, type.Id, monday.AddDays(1));

        var own = await SignedInAsRoleAsync("own", mia.Id, "excel:View", "excel:Export", "leave:View", "employees:View");

        foreach (var module in new[] { "leave-applications", "leave-balances" })
        {
            var all = await admin.PostAsJsonAsync($"/api/excel/exports/{module}", new { scope = "filtered", search = Tag });
            var mine = await own.PostAsJsonAsync($"/api/excel/exports/{module}", new { scope = "all" });
            Assert.Equal(HttpStatusCode.OK, all.StatusCode);
            Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
            using var ownBook = new XLWorkbook(new MemoryStream(await mine.Content.ReadAsByteArrayAsync()));
            var sheet = ownBook.Worksheets.First();
            var codeColumn = (await HeadersFromSheet(sheet)).IndexOf("Employee Code") + 1;
            var codes = Enumerable.Range(2, sheet.LastRowUsed()!.RowNumber() - 1).Select(r => sheet.Cell(r, codeColumn).GetString()).Distinct().ToList();
            Assert.Equal([mia.EmployeeCode], codes);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/excel/templates/leave-applications")).StatusCode);
    }

    private static Task<List<string>> HeadersFromSheet(IXLWorksheet sheet) =>
        Task.FromResult(Enumerable.Range(1, sheet.LastColumnUsed()!.ColumnNumber()).Select(c => sheet.Cell(1, c).GetString()).ToList());

    [IntegrationFact]
    public async Task Shift_assignments_are_imported_through_the_employee_rules_and_need_the_hr_tier_of_attendance()
    {
        var org = await SeedOrganizationAsync();
        var mia = await SeedEmployeeAsync(org, "1", "mia");
        var shift = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<ShiftCreateRequest, ShiftDto>>().HandleAsync(
            new ShiftCreateRequest(Code("SH1"), Name("Day"), new TimeOnly(9, 0), new TimeOnly(18, 0), 60, 15, [1, 2, 3, 4, 5], false, "#3f6fd8", "Active"), default)));

        var workbook = await WorkbookAsync(
            "shift-assignments",
            new Dictionary<string, string> { ["Employee Code"] = mia.EmployeeCode, ["Shift Code"] = shift.Code },
            new Dictionary<string, string> { ["Employee Code"] = mia.EmployeeCode.Replace("1", "9", StringComparison.Ordinal), ["Shift Code"] = shift.Code });
        var preview = await ValidateAsync(admin, "shift-assignments", workbook, "InsertOrUpdate");

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[1].Status);
        var result = await ExecuteAsync(admin, preview, "Partial");
        Assert.Equal(1, result.Inserted + result.Updated);
        Assert.Equal(shift.Id, await Sql.ScalarAsync<int>("SELECT ShiftId FROM hr.Employee WHERE Id = @id", ("@id", mia.Id)));

        var manager = await SignedInAsRoleAsync("manager", null, "excel:View", "excel:Import", "attendance:View", "attendance:Edit", "attendance:Approve");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/excel/templates/shift-assignments")).StatusCode);
    }

    [IntegrationFact]
    public async Task Attendance_records_are_exported_with_the_names_of_the_list()
    {
        var org = await SeedOrganizationAsync();
        var shift = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<ICreateHandler<ShiftCreateRequest, ShiftDto>>().HandleAsync(
            new ShiftCreateRequest(Code("SH2"), Name("Day"), new TimeOnly(0, 0), new TimeOnly(23, 59), 0, 15, [0, 1, 2, 3, 4, 5, 6], false, "#3f6fd8", "Active"), default)));
        var mia = Succeeded(await Create(Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, "1", "mia") with { ShiftId = shift.Id }));
        Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IPunchHandler>().HandleAsync(new PunchRequest(mia.Id, "CheckIn"), default)));

        var response = await admin.PostAsJsonAsync("/api/excel/exports/attendance-records", new { scope = "filtered", search = mia.EmployeeCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var headers = await HeadersAsync(response);
        Assert.Contains("Employee Code", headers);
        Assert.Contains("Check In", headers);
    }

    // ---- Salary assignments ----

    [IntegrationFact]
    public async Task Salary_assignments_apply_the_payroll_rules_hide_amounts_and_need_payroll_approve()
    {
        var world = await SeedPayrollAsync();
        var structureCode = Code("STAFF");
        var workbook = await WorkbookAsync(
            "salary-assignments",
            new Dictionary<string, string> { ["Employee Code"] = world.Employee.EmployeeCode, ["Effective From"] = "2026-01-01", ["Salary Structure Code"] = structureCode, ["Basic Salary"] = "1777" },
            new Dictionary<string, string> { ["Employee Code"] = world.Employee.EmployeeCode, ["Effective From"] = "2026-02-01", ["Salary Structure Code"] = structureCode, ["Basic Salary"] = "9876" },
            new Dictionary<string, string> { ["Employee Code"] = world.Employee.EmployeeCode, ["Effective From"] = "1990-01-01", ["Salary Structure Code"] = structureCode, ["Basic Salary"] = "1500" });

        var preview = await ValidateAsync(admin, "salary-assignments", workbook, "InsertOnly");

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal("••••", preview.Rows[0].Values["basicSalary"]);
        Assert.Contains("band", preview.Rows[1].Issues.Single().Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Effective From", preview.Rows[2].Issues.Single().Field);
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM pay.SalaryAssignment WHERE EmployeeId = @id", ("@id", world.Employee.Id)));

        var result = await ExecuteAsync(admin, preview, "Partial");

        Assert.Equal(1, result.Inserted);
        Assert.Equal(2, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM pay.SalaryAssignment WHERE EmployeeId = @id", ("@id", world.Employee.Id)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Message LIKE '%1777%' AND Message LIKE '%' + @p + '%'", ("@p", Tag)));
        Assert.Equal(0, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM app.ImportIssue i JOIN app.ImportBatch b ON b.Id = i.ImportBatchId WHERE b.FileName LIKE @p AND i.Value LIKE '%9876%'", ("@p", Tag + "%")));

        var export = await admin.PostAsJsonAsync("/api/excel/exports/salary-assignments", new { scope = "filtered", search = Tag });
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Contains("Basic Salary", await HeadersAsync(export));

        var payroll = await SignedInAsRoleAsync("payroll", null, "excel:View", "excel:Export", "excel:Import", "payroll:View", "payroll:Create", "payroll:Edit");
        Assert.Equal(HttpStatusCode.Forbidden, (await payroll.PostAsJsonAsync("/api/excel/exports/salary-assignments", new { scope = "all" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await payroll.GetAsync("/api/excel/templates/salary-assignments")).StatusCode);
    }

    [IntegrationFact]
    public async Task A_salary_row_for_an_existing_employee_and_date_updates_that_assignment()
    {
        var world = await SeedPayrollAsync();
        var workbook = await WorkbookAsync(
            "salary-assignments",
            new Dictionary<string, string> { ["Employee Code"] = world.Employee.EmployeeCode, ["Effective From"] = "1998-01-01", ["Salary Structure Code"] = Code("STAFF"), ["Basic Salary"] = "1200" });

        var preview = await ValidateAsync(admin, "salary-assignments", workbook, "InsertOrUpdate");
        await ExecuteAsync(admin, preview, "AllOrNothing");

        Assert.Equal(ImportRowAction.Update, preview.Rows[0].Action);
        Assert.Equal(1200m, await Sql.ScalarAsync<decimal>("SELECT BasicSalary FROM pay.SalaryAssignment WHERE Id = @id", ("@id", world.Assignment.Id)));
    }
}
