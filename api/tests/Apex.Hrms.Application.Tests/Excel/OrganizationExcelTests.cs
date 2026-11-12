using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Excel;
using static Apex.Hrms.Application.Tests.Excel.ExcelModuleHarness;

namespace Apex.Hrms.Application.Tests.Excel;

/// <summary>
/// The organization modules (companies, branches, departments, sections, positions, job grades): every row goes through the
/// create or update handler of the API, the business code is the match key, and references are codes.
/// </summary>
public sealed class OrganizationExcelTests : IDisposable
{
    private readonly ExcelModuleHarness excel = new();

    public void Dispose() => excel.Dispose();

    private static IReadOnlyDictionary<string, string?> CompanyRow(string code, params (string Key, string? Value)[] changes) => With(
        Cells(
            ("code", code), ("name", "Company " + code), ("legalName", code + " Ltd"), ("industry", "Trading"), ("email", "info@" + code.ToLowerInvariant() + ".example.test"),
            ("phone", "+95 1 555 0100"), ("city", "Yangon"), ("country", "Myanmar"), ("foundedDate", "2020-01-01")),
        changes);

    [Fact]
    public void The_catalog_lists_the_six_organization_modules_with_code_keys_and_import_columns()
    {
        string[] codes = ["companies", "branches", "departments", "sections", "positions", "job-grades"];
        foreach (var code in codes)
        {
            var info = excel.Info(code);
            Assert.True(info.CanImport && info.CanExport);
            Assert.Equal(["code"], info.MatchKey);
            Assert.Equal("code", info.Columns[0].Key);
            Assert.True(info.Columns[0].Required);
            Assert.Contains(info.Columns, c => c.Key == "status" && c.Allowed.SequenceEqual(["Active", "Inactive"]));
        }

        Assert.Equal("Company Code", excel.Info("branches").Columns.Single(c => c.Key == "companyCode").Header);
        Assert.Equal("Position Title", excel.Info("positions").Columns.Single(c => c.Key == "title").Header);
        Assert.Equal("Level", excel.Info("job-grades").Columns.Single(c => c.Key == "level").Header);
    }

    [Fact]
    public async Task A_valid_company_is_inserted_and_a_bad_shape_is_reported_on_its_column()
    {
        var preview = await excel.ValidateAsync(
            "companies",
            ImportStrategy.InsertOnly,
            CompanyRow("NEWCO"),
            CompanyRow("BADCO", ("status", "Dormant")),
            CompanyRow("noco", ("email", "not-an-email")));

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal(ImportRowAction.Insert, preview.Rows[0].Action);
        Assert.Contains("\"Dormant\" is not an allowed value", preview.Rows[1].Issues.Single().Message);
        Assert.Equal("Status", preview.Rows[1].Issues.Single().Field);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[2].Status);
        Assert.Equal("Email", preview.Rows[2].Issues.Single().Field);
    }

    [Fact]
    public async Task The_match_key_is_the_business_code_and_the_three_strategies_follow_it()
    {
        await excel.Host.CreateCompanyAsync("AHT", "Apex Holdings");

        var insertOnly = await excel.ValidateAsync("companies", ImportStrategy.InsertOnly, CompanyRow("AHT"), CompanyRow("NEW1"));
        var updateOnly = await excel.ValidateAsync("companies", ImportStrategy.UpdateOnly, CompanyRow("AHT", ("name", "Apex Group")), CompanyRow("NEW2"));
        var either = await excel.ValidateAsync("companies", ImportStrategy.InsertOrUpdate, CompanyRow("AHT", ("name", "Apex Group Two")), CompanyRow("NEW3"));

        Assert.Equal(ImportRowStatus.Duplicate, insertOnly.Rows[0].Status);
        Assert.Contains("already exists", insertOnly.Rows[0].Issues.Single().Message, StringComparison.Ordinal);
        Assert.Equal(ImportRowStatus.Valid, insertOnly.Rows[1].Status);
        Assert.Equal(ImportRowAction.Update, updateOnly.Rows[0].Action);
        Assert.Equal(ImportRowStatus.Invalid, updateOnly.Rows[1].Status);
        Assert.Contains("cannot be updated", updateOnly.Rows[1].Issues.Single().Message, StringComparison.Ordinal);
        Assert.Equal([ImportRowAction.Update, ImportRowAction.Insert], either.Rows.Select(r => r.Action));
    }

    [Fact]
    public async Task A_duplicate_code_inside_the_file_is_flagged_and_codes_are_case_insensitive()
    {
        var preview = await excel.ValidateAsync("companies", ImportStrategy.InsertOnly, CompanyRow("DUP1"), CompanyRow("dup1"));

        Assert.Equal(ImportRowStatus.Duplicate, preview.Rows[1].Status);
        Assert.Contains("Duplicate of row 2", preview.Rows[1].Issues.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_update_changes_the_record_through_the_update_handler_and_can_change_its_status()
    {
        var company = await excel.Host.CreateCompanyAsync("AHT", "Apex Holdings");
        var preview = await excel.ValidateAsync("companies", ImportStrategy.UpdateOnly, CompanyRow("AHT", ("name", "Apex Group"), ("status", "Inactive")));

        var stored = excel.Host.Repository<Apex.Hrms.Domain.Organization.Company>().Items.Single(c => c.Id == company.Id);
        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal("Apex Group", stored.Name);
        Assert.Equal(Apex.Hrms.Domain.Common.RecordStatus.Inactive, stored.Status);
    }

    [Fact]
    public async Task An_update_with_a_blank_optional_cell_keeps_the_current_value()
    {
        var created = await excel.Host.CreateCompanyAsync("AHT", "Apex Holdings");
        var update = Requests.CompanyUpdate(created.RowVersion) with { Website = "www.apex.example.test", RowVersion = Requests.Version(created.RowVersion) };
        Apex.Hrms.Application.Tests.Fakes.ResultAssert.Succeeded(
            await excel.Host.Get<IUpdateHandler<CompanyUpdateRequest, CompanyDto>>().HandleAsync(created.Id, update, default));

        await excel.ValidateAsync("companies", ImportStrategy.UpdateOnly, CompanyRow("AHT"));

        Assert.Equal("www.apex.example.test", excel.Host.Repository<Apex.Hrms.Domain.Organization.Company>().Items.Single().Website);
    }

    [Fact]
    public async Task A_company_name_that_exists_is_refused_with_the_message_of_the_api_on_the_name_column()
    {
        await excel.Host.CreateCompanyAsync("AHT", "Shared Name");

        var preview = await excel.ValidateAsync("companies", ImportStrategy.InsertOnly, CompanyRow("OTHER", ("name", "Shared Name")));

        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[0].Status);
        Assert.Contains("name", preview.Rows[0].Issues.Single().Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Branches_resolve_the_company_by_code_and_name_a_missing_one_like_the_employee_import()
    {
        var company = await excel.Host.CreateCompanyAsync();
        var row = Cells(("code", "NBR"), ("name", "New Branch"), ("companyCode", company.Code), ("type", "Branch"), ("city", "Mandalay"), ("country", "Myanmar"), ("openedDate", "2021-02-01"));

        var preview = await excel.ValidateAsync(
            "branches",
            ImportStrategy.InsertOnly,
            row,
            With(row, ("code", "NBR2"), ("name", "Other"), ("companyCode", "NOPE")),
            With(row, ("code", "NBR3"), ("name", "Third"), ("managerCode", "EMP999")),
            With(row, ("code", "NBR4"), ("name", "Fourth"), ("type", "Kiosk")));

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal("Company \"NOPE\" does not exist.", preview.Rows[1].Issues.Single().Message);
        Assert.Equal("Company Code", preview.Rows[1].Issues.Single().Field);
        Assert.Equal("Manager \"EMP999\" does not exist.", preview.Rows[2].Issues.Single().Message);
        Assert.Contains("is not an allowed value", preview.Rows[3].Issues.Single().Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_branch_can_move_to_another_company_only_when_the_api_allows_it_and_the_reason_is_shown()
    {
        var org = await excel.Host.SeedOrgAsync();
        var other = await excel.Host.CreateCompanyAsync("OTH", "Other Co");
        var row = Cells(("code", org.Branch.Code), ("name", org.Branch.Name), ("companyCode", other.Code), ("type", "Branch"), ("city", "Yangon"), ("country", "Myanmar"), ("openedDate", "2020-01-01"));

        var preview = await excel.ValidateAsync("branches", ImportStrategy.UpdateOnly, row);

        // The branch has a department, so the reference protection of the API refuses the move.
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[0].Status);
        Assert.NotEmpty(preview.Rows[0].Issues.Single().Message);
    }

    [Fact]
    public async Task Departments_need_a_branch_that_belongs_to_the_company_and_sections_need_a_department()
    {
        var org = await excel.Host.SeedOrgAsync();
        var other = await excel.Host.CreateCompanyAsync("OTH", "Other Co");
        var department = Cells(("code", "NDP"), ("name", "New Department"), ("companyCode", org.Company.Code), ("branchCode", org.Branch.Code));
        var section = Cells(("code", "NSC"), ("name", "New Section"), ("departmentCode", org.Department.Code));

        var departments = await excel.ValidateAsync(
            "departments",
            ImportStrategy.InsertOnly,
            department,
            With(department, ("code", "NDP2"), ("name", "Second"), ("companyCode", other.Code)));
        var sections = await excel.ValidateAsync(
            "sections",
            ImportStrategy.InsertOnly,
            section,
            With(section, ("code", "NSC2"), ("name", "Second"), ("departmentCode", "NOPE")));

        Assert.Equal(ImportRowStatus.Valid, departments.Rows[0].Status);
        Assert.Equal($"Branch \"{org.Branch.Code}\" does not exist in company {other.Name}.", departments.Rows[1].Issues.Single().Message);
        Assert.Equal(ImportRowStatus.Valid, sections.Rows[0].Status);
        Assert.Equal("Department \"NOPE\" does not exist.", sections.Rows[1].Issues.Single().Message);
    }

    [Fact]
    public async Task Positions_resolve_department_section_grade_and_a_reporting_line_created_earlier_in_the_same_file()
    {
        var org = await excel.Host.SeedOrgAsync();
        var head = Cells(("code", "HEAD"), ("title", "Head"), ("departmentCode", org.Department.Code), ("jobGradeCode", org.Grade.Code), ("headcount", "1"));

        var preview = await excel.ValidateAsync(
            "positions",
            ImportStrategy.InsertOnly,
            head,
            With(head, ("code", "STAFF"), ("title", "Staff"), ("reportsToCode", "HEAD")),
            With(head, ("code", "BAD1"), ("title", "Bad"), ("jobGradeCode", "NOPE")),
            With(head, ("code", "BAD2"), ("title", "Bad two"), ("sectionCode", "NOSEC")),
            With(head, ("code", "BAD3"), ("title", "Bad three"), ("reportsToCode", "NOPE")));

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Valid, preview.Rows[1].Status);
        Assert.Equal("Job grade \"NOPE\" does not exist.", preview.Rows[2].Issues.Single().Message);
        Assert.Equal($"Section \"NOSEC\" does not exist in department {org.Department.Name}.", preview.Rows[3].Issues.Single().Message);
        Assert.Equal("Position \"NOPE\" does not exist.", preview.Rows[4].Issues.Single().Message);
    }

    [Fact]
    public async Task Job_grades_are_checked_by_the_api_for_the_salary_band()
    {
        var row = Cells(("code", "NG1"), ("name", "New grade"), ("level", "2"), ("minSalary", "100"), ("maxSalary", "200"));

        var preview = await excel.ValidateAsync(
            "job-grades",
            ImportStrategy.InsertOnly,
            row,
            With(row, ("code", "NG2"), ("name", "Inverted"), ("minSalary", "300")),
            With(row, ("code", "NG3"), ("name", "Zero"), ("level", "0")));

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[1].Status);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[2].Status);
    }

    [Fact]
    public async Task Partial_execute_keeps_the_good_rows_and_all_or_nothing_reports_a_rollback()
    {
        var first = await excel.ValidateAsync("companies", ImportStrategy.InsertOrUpdate, CompanyRow("GOOD1"), CompanyRow("BAD1", ("email", "nope")));
        var partial = await excel.ExecuteAsync(first, ImportMode.Partial);
        var second = await excel.ValidateAsync("companies", ImportStrategy.InsertOnly, CompanyRow("GOOD2"), CompanyRow("BAD2", ("email", "nope")));
        var refused = await excel.Imports.ExecuteAsync(second.BatchId, ImportMode.AllOrNothing, default);

        Assert.Equal(1, partial.Inserted + partial.Updated);
        Assert.Equal(1, partial.Skipped);
        Assert.True(refused.IsFailure);
        Assert.Contains("all-or-nothing", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains(excel.Host.ActivityLog.Messages, m => m.StartsWith("Excel import of Companies", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_export_writes_the_records_with_codes_and_names_and_the_template_layout_can_be_imported_again()
    {
        var org = await excel.Host.SeedOrgAsync();

        var branches = await excel.ExportAsync("branches");
        var positions = await excel.ExportAsync("positions", new ExportRequest(ExportScope.All, Layout: "template"));

        var branch = Assert.Single(branches);
        Assert.Equal(org.Company.Code, branch["companyCode"]);
        Assert.Equal(org.Company.Name, branch["company"]);
        Assert.Equal(org.Branch.Code, branch["code"]);
        Assert.Contains("code", excel.Writer.ExportedMain!.Columns.Select(c => c.Key));
        var position = Assert.Single(positions);
        Assert.Equal(org.Department.Code, position["departmentCode"]);
        Assert.Equal(org.Grade.Code, position["jobGradeCode"]);
        Assert.DoesNotContain(excel.Writer.ExportedMain!.Columns, c => c.Key == "department");
    }

    [Fact]
    public async Task An_export_applies_the_filters_and_the_search_of_the_list_and_a_selection_of_ids()
    {
        var org = await excel.Host.SeedOrgAsync();
        await excel.Host.CreateBranchAsync(org.Company.Id, "SEC", "Inactive");

        var active = await excel.ExportAsync(
            "branches",
            new ExportRequest(ExportScope.Filtered, Filters: new Dictionary<string, System.Text.Json.JsonElement> { ["status"] = System.Text.Json.JsonSerializer.SerializeToElement("Active") }));
        var searched = await excel.ExportAsync("branches", new ExportRequest(ExportScope.Filtered, Search: "SEC"));
        var selected = await excel.ExportAsync("branches", new ExportRequest(ExportScope.Selected, SelectedIds: [org.Branch.Id]));

        Assert.Equal(org.Branch.Code, Assert.Single(active)["code"]);
        Assert.Equal("SEC", Assert.Single(searched)["code"]);
        Assert.Equal(org.Branch.Code, Assert.Single(selected)["code"]);
    }

    [Fact]
    public async Task The_organization_modules_need_organization_create_and_edit_to_import_and_view_to_export()
    {
        await Task.CompletedTask;
        foreach (var code in new[] { "companies", "branches", "departments", "sections", "positions", "job-grades" })
        {
            var module = excel.Host.Get<IEnumerable<IExcelModule>>().Single(m => m.Info.Code == code);
            Assert.Equal(["organization:Create", "organization:Edit"], module.ImportPolicies);
            Assert.Equal(["organization:View"], module.ExportPolicies);
            Assert.Empty(module.SensitiveExportPolicies);
        }
    }
}
