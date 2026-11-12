using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;
using static Apex.Hrms.Application.Tests.Excel.ExcelModuleHarness;

namespace Apex.Hrms.Application.Tests.Excel;

/// <summary>
/// The Leave, Attendance and Payroll modules: exports show only the rows the caller's tier allows (own record, own team, all),
/// leave and attendance records cannot be imported, shift assignments and salary assignments can, through the API's handlers.
/// </summary>
public sealed class PerEmployeeExcelTests : IDisposable
{
    private readonly ExcelModuleHarness excel = new();

    public void Dispose() => excel.Dispose();

    private TestHost Host => excel.Host;

    private static DateOnly D(int month, int day) => new(2026, month, day);

    private sealed record Cast(EmployeeDto Maya, EmployeeDto Ria, EmployeeDto Nora, OrgSeed Org);

    /// <summary>Maya manages Ria; Nora is a colleague who reports to nobody here.</summary>
    private async Task<Cast> SeedCastAsync(int? shiftId = null)
    {
        var org = await Host.SeedOrgAsync();
        return new Cast(
            await Host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: "maya@example.test") with { ShiftId = shiftId }),
            await Host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: "ria@example.test") with { ShiftId = shiftId }),
            await Host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: "nora@example.test") with { ShiftId = shiftId }),
            org);
    }

    private void SignInAs(EmployeeDto caller, EmployeeDto[] reports, params string[] grants)
    {
        Host.User.EmployeeId = caller.Id;
        Host.Team.Set([.. reports.Select(report => report.Id)]);
        Host.Permissions.Only(grants);
    }

    private static string[] Codes(IEnumerable<IReadOnlyDictionary<string, object?>> rows) =>
        [.. rows.Select(r => (string)r["employeeCode"]!).Distinct().Order(StringComparer.Ordinal)];

    // ---- Catalog ----

    [Fact]
    public void Leave_and_attendance_records_are_export_only_and_the_others_have_the_expected_keys()
    {
        foreach (var code in new[] { "leave-applications", "leave-balances", "attendance-records" })
        {
            var info = excel.Info(code);
            Assert.True(info.CanExport);
            Assert.False(info.CanImport);
            Assert.Empty(info.Columns);
            Assert.NotEmpty(info.ExportColumns!);
        }

        Assert.Equal(["employeeCode"], excel.Info("shift-assignments").MatchKey);
        Assert.Equal(["employeeCode", "effectiveFrom"], excel.Info("salary-assignments").MatchKey);
        Assert.True(excel.Info("salary-assignments").Columns.Single(c => c.Key == "basicSalary").Sensitive);
        Assert.False(excel.Info("salary-assignments").ExportColumns!.Single(c => c.Key == "basicSalary").Sensitive);
    }

    [Fact]
    public async Task An_export_only_module_has_no_template_and_refuses_an_upload()
    {
        var template = await excel.Imports.GetTemplateAsync("leave-applications", default);
        var inspect = await excel.Imports.InspectAsync("attendance-records", "a.xlsx", ExcelTestFiles.Package(), default);

        Assert.Equal(ErrorType.NotFound, template.Error.Type);
        Assert.Equal(ErrorType.NotFound, inspect.Error.Type);
    }

    [Fact]
    public void Policies_follow_the_brief_for_each_module()
    {
        var modules = Host.Get<IEnumerable<IExcelModule>>().ToDictionary(m => m.Info.Code);

        Assert.Equal(["leave:View"], modules["leave-applications"].ExportPolicies);
        Assert.Equal(["leave:View"], modules["leave-balances"].ExportPolicies);
        Assert.Equal(["attendance:View"], modules["attendance-records"].ExportPolicies);
        Assert.Equal(["attendance:View"], modules["shift-assignments"].ExportPolicies);
        Assert.Equal(["attendance:Edit", "attendance:Approve", "employees:Edit"], modules["shift-assignments"].ImportPolicies);
        Assert.Equal(["payroll:View", "payroll:Approve"], modules["salary-assignments"].ExportPolicies);
        Assert.Equal(["payroll:View", "payroll:Create", "payroll:Edit", "payroll:Approve"], modules["salary-assignments"].ImportPolicies);
        Assert.Equal(["payroll:Approve"], modules["salary-assignments"].SensitiveExportPolicies);
    }

    [Fact]
    public async Task The_catalog_flags_follow_the_grants_of_the_caller()
    {
        await Task.CompletedTask;
        Host.Permissions.Only("excel:View", "leave:View", "payroll:View", "payroll:Approve");

        var modules = excel.Catalog.GetModules().Value.Modules.ToDictionary(m => m.Code);

        Assert.True(modules["leave-applications"].CanExport);
        Assert.True(modules["salary-assignments"].CanExport);
        Assert.False(modules["salary-assignments"].CanImport);
        Assert.False(modules["companies"].CanExport);
        Assert.False(modules["attendance-records"].CanExport);
        Assert.False(modules["employees"].CanImport);

        Host.Permissions.Only("payroll:View", "payroll:Create", "payroll:Edit", "payroll:Approve");
        Assert.True(excel.Catalog.GetModules().Value.Modules.Single(m => m.Code == "salary-assignments").CanImport);
    }

    // ---- Leave exports ----

    private async Task SeedLeaveAsync(Cast cast)
    {
        var type = await Host.CreateTypeAsync(LeaveFixtures.NewType(days: 15m));
        var day = 16;
        foreach (var employee in new[] { cast.Maya, cast.Ria, cast.Nora })
        {
            await Host.SubmittedAsync(LeaveFixtures.NewApplication(employee.Id, type.Id, D(11, day++)));
        }
    }

    [Fact]
    public async Task Leave_applications_export_the_columns_of_the_list_and_only_the_rows_of_the_callers_tier()
    {
        var cast = await SeedCastAsync();
        await SeedLeaveAsync(cast);

        SignInAs(cast.Ria, [], "leave:View");
        var own = await excel.ExportAsync("leave-applications");
        SignInAs(cast.Maya, [cast.Ria], "leave:View", "leave:Approve");
        var team = await excel.ExportAsync("leave-applications");
        SignInAs(cast.Maya, [cast.Ria], "leave:View", "leave:Approve", "employees:Edit");
        var all = await excel.ExportAsync("leave-applications");

        Assert.Equal([cast.Ria.EmployeeCode], Codes(own));
        Assert.Equal(new[] { cast.Maya.EmployeeCode, cast.Ria.EmployeeCode }.Order(StringComparer.Ordinal), Codes(team));
        Assert.Equal(3, Codes(all).Length);
        Assert.Equal("Submitted", all[0]["status"]);
        Assert.Equal(1m, all[0]["days"]);
        Assert.StartsWith("LV-", (string)all[0]["applicationNo"]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_leave_export_honours_the_date_range_and_a_caller_without_a_linked_employee_sees_nothing()
    {
        var cast = await SeedCastAsync();
        await SeedLeaveAsync(cast);

        var narrowed = await excel.ExportAsync("leave-applications", new ExportRequest(ExportScope.All, DateFrom: D(11, 17), DateTo: D(11, 17)));
        Host.User.EmployeeId = null;
        Host.Permissions.Only("leave:View");
        var nobody = await excel.ExportAsync("leave-applications");

        Assert.Single(narrowed);
        Assert.Empty(nobody);
    }

    [Fact]
    public async Task Leave_balances_are_worked_out_per_employee_and_type_and_scoped_like_the_leave_screen()
    {
        var cast = await SeedCastAsync();
        await SeedLeaveAsync(cast);

        SignInAs(cast.Ria, [], "leave:View");
        var own = await excel.ExportAsync("leave-balances");
        SignInAs(cast.Maya, [cast.Ria], "leave:View", "leave:Approve", "employees:Edit");
        var all = await excel.ExportAsync("leave-balances");

        Assert.Equal([cast.Ria.EmployeeCode], Codes(own));
        Assert.Equal(3, Codes(all).Length);
        var line = all.First(r => (string)r["employeeCode"]! == cast.Nora.EmployeeCode);
        Assert.Equal(2026, line["year"]);
        Assert.Equal(15m, line["entitled"]);
        Assert.Equal(1m, line["pending"]);
        Assert.Equal(14m, line["available"]);
    }

    [Fact]
    public async Task A_balance_export_refuses_a_year_outside_the_supported_range()
    {
        await SeedCastAsync();

        var result = await excel.Exports.ExportAsync(
            "leave-balances",
            new ExportRequest(ExportScope.Filtered, Filters: new Dictionary<string, System.Text.Json.JsonElement> { ["year"] = System.Text.Json.JsonSerializer.SerializeToElement(1900) }),
            default);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("year", result.Error.FieldErrors!.Keys);
    }

    // ---- Attendance ----

    [Fact]
    public async Task Attendance_records_export_only_the_rows_of_the_callers_tier()
    {
        var org = await Host.SeedOrgAsync();
        var shift = await Host.CreateShiftAsync();
        var maya = await Host.CreateWorkerAsync(org, "maya@example.test", shift.Id);
        var ria = await Host.CreateWorkerAsync(org, "ria@example.test", shift.Id);
        var nora = await Host.CreateWorkerAsync(org, "nora@example.test", shift.Id);
        foreach (var worker in new[] { maya, ria, nora })
        {
            await Host.PunchAsync(worker.Id, "CheckIn", 9, 0);
        }

        SignInAs(ria, [], "attendance:View");
        var own = await excel.ExportAsync("attendance-records");
        SignInAs(maya, [ria], "attendance:View", "attendance:Approve");
        var team = await excel.ExportAsync("attendance-records");
        SignInAs(maya, [ria], "attendance:View", "attendance:Approve", "employees:Edit");
        var all = await excel.ExportAsync("attendance-records");

        Assert.Equal([ria.EmployeeCode], Codes(own));
        Assert.Equal(2, Codes(team).Length);
        Assert.Equal(3, all.Count);
        Assert.Equal("09:00", all[0]["checkIn"]);
        Assert.Equal(AttendanceFixtures.Monday, all[0]["date"]);
        Assert.Equal(shift.Name, all[0]["shift"]);
    }

    [Fact]
    public async Task Shift_assignments_export_the_employees_of_the_callers_attendance_tier_with_their_shift()
    {
        var shift = await Host.CreateShiftAsync();
        var cast = await SeedCastAsync(shift.Id);

        SignInAs(cast.Ria, [], "attendance:View");
        var own = await excel.ExportAsync("shift-assignments");
        SignInAs(cast.Maya, [cast.Ria], "attendance:View", "attendance:Approve", "employees:Edit");
        var all = await excel.ExportAsync("shift-assignments");

        Assert.Equal([cast.Ria.EmployeeCode], Codes(own));
        Assert.Equal(3, all.Count);
        Assert.All(all, r => Assert.Equal(shift.Code, r["shiftCode"]));
    }

    private static IReadOnlyDictionary<string, string?> ShiftRow(EmployeeDto employee, string shiftCode) =>
        Cells(("employeeCode", employee.EmployeeCode), ("shiftCode", shiftCode));

    [Fact]
    public async Task A_shift_row_sets_the_shift_through_the_employee_update_handler()
    {
        var general = await Host.CreateShiftAsync();
        var early = await Host.CreateShiftAsync(AttendanceFixtures.NewShift("EARLY", "Early Shift", AttendanceFixtures.At(7, 0), AttendanceFixtures.At(16, 0)));
        var cast = await SeedCastAsync(general.Id);
        var noShift = await Host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(cast.Org, email: "ivy@example.test"));

        var preview = await excel.ValidateAsync(
            "shift-assignments",
            ImportStrategy.InsertOrUpdate,
            ShiftRow(cast.Ria, early.Code),
            ShiftRow(noShift, early.Code),
            ShiftRow(cast.Nora, "NOPE"),
            ShiftRow(cast.Maya, general.Code),
            Cells(("employeeCode", "EMP999"), ("shiftCode", early.Code)));

        Assert.Equal([ImportRowAction.Update, ImportRowAction.Insert], preview.Rows.Take(2).Select(r => r.Action));
        Assert.Equal(early.Id, ResultAssert.Succeeded(await Host.GetEmployee(cast.Ria.Id)).ShiftId);
        Assert.Equal(early.Id, ResultAssert.Succeeded(await Host.GetEmployee(noShift.Id)).ShiftId);
        Assert.Equal("Shift \"NOPE\" does not exist.", preview.Rows[2].Issues.Single().Message);
        Assert.Equal(ImportRowStatus.Valid, preview.Rows[3].Status);
        Assert.Equal("Employee \"EMP999\" does not exist.", preview.Rows[4].Issues.Single().Message);
    }

    [Fact]
    public async Task Shift_strategies_treat_an_employee_with_a_shift_as_an_existing_assignment()
    {
        var general = await Host.CreateShiftAsync();
        var cast = await SeedCastAsync(general.Id);

        var insertOnly = await excel.ValidateAsync("shift-assignments", ImportStrategy.InsertOnly, ShiftRow(cast.Ria, general.Code));

        Assert.Equal(ImportRowStatus.Duplicate, insertOnly.Rows[0].Status);
    }

    // ---- Salary assignments ----

    private static IReadOnlyDictionary<string, string?> SalaryRow(EmployeeDto employee, string from, string basic, string structure = "STAFF") =>
        Cells(("employeeCode", employee.EmployeeCode), ("effectiveFrom", from), ("structureCode", structure), ("basicSalary", basic));

    [Fact]
    public async Task Salary_rows_reuse_the_band_joining_date_and_one_per_date_rules_of_the_payroll_handlers()
    {
        var seed = await Host.SeedPayrollAsync();

        var preview = await excel.ValidateAsync(
            "salary-assignments",
            ImportStrategy.InsertOnly,
            SalaryRow(seed.Employee, "2026-01-01", "1500"),
            SalaryRow(seed.Employee, "2026-02-01", "9000"),
            SalaryRow(seed.Employee, "2019-01-01", "1500"),
            SalaryRow(seed.Employee, "2020-01-01", "1500"),
            SalaryRow(seed.Employee, "2026-03-01", "1500", "NOPE"),
            Cells(("employeeCode", "EMP999"), ("effectiveFrom", "2026-04-01"), ("structureCode", "STAFF"), ("basicSalary", "1500")));

        Assert.Equal(ImportRowStatus.Valid, preview.Rows[0].Status);
        Assert.Equal("Outside the PG1 band (500 – 5000).", preview.Rows[1].Issues.Single().Message);
        Assert.Equal("Basic Salary", preview.Rows[1].Issues.Single().Field);
        Assert.Equal("Must be on or after 2020-01-01.", preview.Rows[2].Issues.Single().Message);
        Assert.Equal("Effective From", preview.Rows[2].Issues.Single().Field);
        Assert.Equal(ImportRowStatus.Duplicate, preview.Rows[3].Status);
        Assert.Equal("Salary structure \"NOPE\" does not exist.", preview.Rows[4].Issues.Single().Message);
        Assert.Equal("Employee \"EMP999\" does not exist.", preview.Rows[5].Issues.Single().Message);
    }

    [Fact]
    public async Task A_salary_row_with_an_existing_employee_and_date_updates_that_assignment()
    {
        var seed = await Host.SeedPayrollAsync();

        var preview = await excel.ValidateAsync("salary-assignments", ImportStrategy.InsertOrUpdate, SalaryRow(seed.Employee, "2020-01-01", "1200"));

        Assert.Equal(ImportRowAction.Update, preview.Rows[0].Action);
        Assert.Equal(1200m, Host.Repository<Domain.Payroll.SalaryAssignment>().Items.Single().BasicSalary);
    }

    [Fact]
    public async Task Salary_amounts_are_masked_in_the_preview_and_never_reach_the_activity_feed()
    {
        var seed = await Host.SeedPayrollAsync();
        var preview = await excel.ValidateAsync(
            "salary-assignments",
            ImportStrategy.InsertOrUpdate,
            SalaryRow(seed.Employee, "2026-01-01", "1777"),
            SalaryRow(seed.Employee, "2026-02-01", "9876"));

        var result = await excel.ExecuteAsync(preview, ImportMode.Partial);

        Assert.Equal("••••", preview.Rows[0].Values["basicSalary"]);
        Assert.Equal("••••", preview.Rows[1].Values["basicSalary"]);
        Assert.Equal(1, result.Inserted + result.Updated);
        Assert.Equal(1, result.Skipped);
        Assert.DoesNotContain(Host.ActivityLog.Messages, m => m.Contains("1777", StringComparison.Ordinal) || m.Contains("9876", StringComparison.Ordinal));
        Assert.Contains(Host.ActivityLog.Messages, m => m.StartsWith("Excel import of Salary assignments", StringComparison.Ordinal));
        var stored = (await excel.Imports.GetBatchAsync(preview.BatchId, default)).Value;
        Assert.DoesNotContain(stored.Issues, i => i.Value.Contains("9876", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_salary_export_writes_the_assignments_and_only_the_callers_own_without_payroll_approve()
    {
        var seed = await Host.SeedPayrollAsync();
        var other = await Host.CreatePayrollEmployeeAsync(seed.Org, "bo.test@example.test");
        await Host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(other.Id, seed.Structure.Id, 2000m));

        var all = await excel.ExportAsync("salary-assignments");
        Host.User.EmployeeId = seed.Employee.Id;
        Host.Permissions.Only("payroll:View");
        var own = await excel.ExportAsync("salary-assignments");

        Assert.Equal(2, all.Count);
        Assert.Equal(1000m, all.First(r => (string)r["employeeCode"]! == seed.Employee.EmployeeCode)["basicSalary"]);
        Assert.Equal("STAFF", all[0]["structureCode"]);
        Assert.Equal([seed.Employee.EmployeeCode], Codes(own));
        Assert.DoesNotContain(excel.Writer.ExportedMain!.Columns, c => c.Key == "bankAccountNumber");
    }

    [Fact]
    public async Task A_salary_export_honours_the_effective_date_range()
    {
        var seed = await Host.SeedPayrollAsync();
        await Host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 1100m, new DateOnly(2026, 6, 1)));

        var from2026 = await excel.ExportAsync("salary-assignments", new ExportRequest(ExportScope.All, DateFrom: new DateOnly(2026, 1, 1)));

        Assert.Equal(new DateOnly(2026, 6, 1), Assert.Single(from2026)["effectiveFrom"]);
    }

    [Fact]
    public async Task The_salary_template_lists_the_active_structure_codes()
    {
        await Host.SeedPayrollAsync();
        var module = Host.Get<IEnumerable<IExcelModule>>().Single(m => m.Info.Code == "salary-assignments");

        var sheets = await module.ReferenceSheetsAsync(default);

        Assert.Equal("STAFF", Assert.Single(Assert.Single(sheets).Rows)["code"]);
    }
}
