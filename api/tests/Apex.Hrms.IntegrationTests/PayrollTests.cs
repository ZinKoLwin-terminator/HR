using System.Reflection;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Payroll.Assignments;
using Apex.Hrms.Application.Payroll.Components;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.Application.Payroll.Structures;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The payroll module against the real SQL Server schema, including its triggers and check constraints. Every run lives in the reserved
/// test year 1999 (see <see cref="TestRows"/>), the employees are tagged, and the figures are fictional DEMONSTRATION figures: a basic
/// of 1000 with Housing 10% of basic, Transport fixed 50, Tax 5% of gross and Social 2% of gross.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class PayrollTests : PayrollTestBase
{
    private const string Masked = "•••• 7890";

    // ---- Processing ----

    [IntegrationFact]
    public async Task Processing_a_run_stores_the_payslip_its_lines_the_run_totals_and_only_the_masked_account()
    {
        var world = await SeedPayrollAsync();
        var run = await CreateRunAsync();

        var processed = await ProcessedAsync(run);

        Assert.Equal("Pending Approval", processed.Status);
        Assert.Equal("January 1999", processed.PeriodName);
        Assert.Equal(1, processed.EmployeeCount);
        Assert.Equal(1000m, processed.TotalBasic);
        Assert.Equal(150m, processed.TotalAllowances);
        Assert.Equal(1150m, processed.TotalGross);
        Assert.Equal(80.5m, processed.TotalDeductions);
        Assert.Equal(1069.5m, processed.TotalNet);
        Assert.Equal(new DateOnly(2026, 11, 9), processed.ProcessedOn);

        var payslip = Assert.Single(Succeeded(await PayslipsOfAsync(run.Id)).Items);
        Assert.Equal(world.Employee.Id, payslip.EmployeeId);
        Assert.Equal(Masked, payslip.AccountMasked);
        Assert.Equal([(Code("HOUS"), 100m), (Code("TRAN"), 50m)], payslip.Earnings.Select(line => (line.Code, line.Amount)));
        Assert.Equal([57.5m, 23m], payslip.Deductions.Select(line => line.Amount));
        Assert.Equal(1069.5m, payslip.NetSalary);
        Assert.Equal(31, payslip.PeriodDays);
        Assert.Equal(31, payslip.DaysEmployed);
        Assert.Equal(21, payslip.WorkingDays); // January 1999: 21 weekdays

        // What the table holds: the masked account only, never the number; and every check constraint accepted the figures.
        Assert.Equal(Masked, await ScalarAsync<string>("SELECT AccountMasked FROM pay.Payslip WHERE Id = @id", ("@id", payslip.Id)));
        Assert.Equal(0, await ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pay.Payslip p
            WHERE p.Id = @id AND (p.AccountMasked + p.BankName + p.EmployeeName + p.EmployeeCode + p.PositionTitle + p.DepartmentName + p.CompanyName + p.PeriodName) LIKE '%' + @account + '%'
            """,
            ("@id", payslip.Id),
            ("@account", AccountNumber)));
        Assert.Equal(4, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.PayslipLine WHERE PayslipId = @id", ("@id", payslip.Id)));
        Assert.Equal(0, await ScalarAsync<int>(
            "SELECT COUNT(*) FROM pay.PayslipLine WHERE PayslipId = @id AND (Name + Basis + Code) LIKE '%' + @account + '%'",
            ("@id", payslip.Id),
            ("@account", AccountNumber)));
        Assert.Equal(TestAdministrator.AccountId, await ScalarAsync<int>("SELECT ProcessedByUserId FROM pay.PayrollRun WHERE Id = @id", ("@id", run.Id)));
        Assert.Equal($"{Tag} Test User", await ScalarAsync<string>("SELECT ProcessedByName FROM pay.PayrollRun WHERE Id = @id", ("@id", run.Id)));
    }

    [IntegrationFact]
    public async Task Overtime_and_unpaid_leave_in_the_database_change_the_payslip()
    {
        var world = await SeedPayrollAsync();
        await Sql.ExecuteAsync(
            """
            INSERT INTO att.OvertimeRecord (EmployeeId, [Date], StartTime, EndTime, Minutes, Reason, Status, RequestedOn, DecidedOn, DecidedByName)
            VALUES (@employee, '1999-01-12', '18:00', '20:00', 120, N'Month end close', 'Approved', '1999-01-12', '1999-01-13', N'Test Approver');
            """,
            ("@employee", world.Employee.Id));
        var run = await CreateRunAsync();

        var processed = await ProcessedAsync(run);

        var payslip = Assert.Single(Succeeded(await PayslipsOfAsync(run.Id)).Items);
        // 21 working days: hourly = 1000 / (21 x 8); 2 h x hourly x 1.5 = 17.857... = 17.86
        Assert.Equal(120, payslip.OvertimeMinutes);
        Assert.Equal(17.86m, payslip.Earnings.Single(line => line.Code == "OT").Amount);
        Assert.Equal(1167.86m, payslip.GrossSalary);
        Assert.Equal(1167.86m, processed.TotalGross);
    }

    [IntegrationFact]
    public async Task Employees_without_a_salary_assignment_are_skipped_with_a_warning_that_is_stored_and_returned()
    {
        var world = await SeedPayrollAsync();
        var skipped = await SeedSecondEmployeeAsync(world, withSalary: false);
        var run = await CreateRunAsync();

        var processed = await ProcessedAsync(run);

        Assert.Equal(1, processed.EmployeeCount);
        var warning = Assert.Single(processed.Warnings);
        Assert.Equal($"{skipped.EmployeeCode} has no salary assignment and was skipped.", warning);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.PayrollRunWarning WHERE PayrollRunId = @id", ("@id", run.Id)));
        Assert.Equal(processed.Warnings, Succeeded(await Get<PayrollRunDto>(run.Id)).Warnings);
    }

    [IntegrationFact]
    public async Task One_run_per_month_and_earlier_months_must_be_finished_first()
    {
        await SeedPayrollAsync();
        var january = await CreateRunAsync(1);
        var february = await CreateRunAsync(2);

        FailedOnField(await CreateAsync<PayrollRunCreateRequest, PayrollRunDto>(new PayrollRunCreateRequest(Year, 1, null)), "month");
        var blocked = Failed(await ProcessAsync(february), ErrorType.Conflict);
        Assert.Equal("Finish January 1999 (Draft) before processing February 1999.", blocked.Message);

        var processedJanuary = await ProcessedAsync(january);
        await ActAsSecondUserAsync();
        Succeeded(await ApproveAsync(processedJanuary));
        Succeeded(await ProcessAsync(Succeeded(await Get<PayrollRunDto>(february.Id))));
    }

    // ---- Separation of duties ----

    [IntegrationFact]
    public async Task Nobody_approves_what_they_processed_but_a_different_user_may()
    {
        await SeedPayrollAsync();
        var processed = await ProcessedAsync(await CreateRunAsync());

        var refused = Failed(await ApproveAsync(processed), ErrorType.Conflict);
        Assert.Equal("You processed this payroll period, so you cannot approve it. Another approver must do it.", refused.Message);
        Assert.Equal("Pending Approval", await ScalarAsync<string>("SELECT Status FROM pay.PayrollRun WHERE Id = @id", ("@id", processed.Id)));

        await ActAsSecondUserAsync();
        var approved = Succeeded(await ApproveAsync(processed));

        Assert.Equal("Approved", approved.Status);
        Assert.Equal($"{Tag} Approver", approved.ApprovedBy);
        Assert.Equal($"{Tag} Test User", approved.ProcessedBy);
        Assert.NotEqual(
            await ScalarAsync<int>("SELECT ProcessedByUserId FROM pay.PayrollRun WHERE Id = @id", ("@id", processed.Id)),
            await ScalarAsync<int>("SELECT ApprovedByUserId FROM pay.PayrollRun WHERE Id = @id", ("@id", processed.Id)));
    }

    [IntegrationFact]
    public async Task The_database_also_refuses_an_approver_who_is_the_processor()
    {
        await SeedPayrollAsync();
        var processed = await ProcessedAsync(await CreateRunAsync());

        var error = await Sql.ErrorNumberOfAsync(
            "UPDATE pay.PayrollRun SET Status = 'Approved', ApprovedByUserId = ProcessedByUserId, ApprovedByName = N'Same', ApprovedOn = '2026-11-09' WHERE Id = @id",
            ("@id", processed.Id));

        Assert.Equal(547, error); // CK_PayrollRun_ApproverNotProcessor
        Assert.Equal("Pending Approval", await ScalarAsync<string>("SELECT Status FROM pay.PayrollRun WHERE Id = @id", ("@id", processed.Id)));
    }

    // ---- Paying ----

    [IntegrationFact]
    public async Task Marking_paid_changes_only_the_status_the_paid_date_and_the_modified_columns_and_paid_is_final()
    {
        await SeedPayrollAsync();
        var processed = await ProcessedAsync(await CreateRunAsync());
        await ActAsSecondUserAsync();
        var approved = Succeeded(await ApproveAsync(processed));
        const string Columns = "PeriodName, [Year], [Month], StartDate, EndDate, PayDate, EmployeeCount, TotalBasic, TotalAllowances, TotalGross, TotalDeductions, TotalNet, ProcessedOn, ProcessedByUserId, ProcessedByName, ApprovedByUserId, ApprovedByName, ApprovedOn";
        var before = await ScalarAsync<string>($"SELECT CONCAT_WS('|', {Columns}) FROM pay.PayrollRun WHERE Id = @id", ("@id", approved.Id));

        var paid = Succeeded(await MarkPaidAsync(approved));

        Assert.Equal("Paid", paid.Status);
        Assert.Equal(new DateOnly(2026, 11, 9), paid.PaidOn);
        Assert.Equal(before, await ScalarAsync<string>($"SELECT CONCAT_WS('|', {Columns}) FROM pay.PayrollRun WHERE Id = @id", ("@id", approved.Id)));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.Payslip WHERE PayrollRunId = @id", ("@id", approved.Id)));

        // Paid is final: nothing returns it to draft, and the table refuses any change.
        Failed(await ReturnToDraftAsync(paid), ErrorType.Conflict);
        Failed(await MarkPaidAsync(paid), ErrorType.Conflict);
        Assert.Equal(51001, await Sql.ErrorNumberOfAsync("UPDATE pay.PayrollRun SET PayDate = PayDate WHERE Id = @id", ("@id", approved.Id)));
        Assert.Equal(51002, await Sql.ErrorNumberOfAsync("DELETE FROM pay.Payslip WHERE PayrollRunId = @id", ("@id", approved.Id)));
    }

    // ---- The lock ----

    [IntegrationFact]
    public async Task An_approved_run_and_its_payslips_lines_and_warnings_are_locked_and_a_change_is_a_conflict()
    {
        var world = await SeedPayrollAsync();
        await SeedSecondEmployeeAsync(world, withSalary: false); // a warning to lock
        var processed = await ProcessedAsync(await CreateRunAsync());
        await ActAsSecondUserAsync();
        Succeeded(await ApproveAsync(processed));
        var payslip = Assert.Single(Succeeded(await PayslipsOfAsync(processed.Id)).Items);

        // The API has no way to edit any of these, so the change is made on the tracked entities, as a coding mistake would.
        var onPayslip = await ChangeAndSaveAsync<Payslip>(payslip.Id, entity => Set(entity, nameof(Payslip.BankName), "Other Bank"));
        var onLine = await ChangeAndSaveAsync<Payslip>(payslip.Id, entity => Set(entity.Lines.First(), nameof(PayslipLine.Basis), "changed"));
        var onWarning = await ChangeAndSaveAsync<PayrollRun>(processed.Id, entity => Set(entity.Warnings.First(), nameof(PayrollRunWarning.Message), "changed"));
        var onRun = await ChangeAndSaveAsync<PayrollRun>(processed.Id, entity => Set(entity, nameof(PayrollRun.PayDate), new DateOnly(1999, 1, 20)));

        Assert.Equal("Payslips of an approved or paid payroll run are locked.", Failed(onPayslip, ErrorType.Conflict).Message);
        Assert.Equal("Payslip lines of an approved or paid payroll run are locked.", Failed(onLine, ErrorType.Conflict).Message);
        Assert.Equal("Warnings of an approved or paid payroll run are locked.", Failed(onWarning, ErrorType.Conflict).Message);
        Assert.Equal(
            "A paid payroll run is final, and an approved one can only be marked as paid or returned to draft.",
            Failed(onRun, ErrorType.Conflict).Message);
        Assert.Equal("Sample Bank", await ScalarAsync<string>("SELECT BankName FROM pay.Payslip WHERE Id = @id", ("@id", payslip.Id)));

        // The API refuses the same through the handlers, before the database is asked.
        var current = Succeeded(await Get<PayrollRunDto>(processed.Id));
        var update = await InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<PayrollRunUpdateRequest, PayrollRunDto>>()
            .HandleAsync(processed.Id, new PayrollRunUpdateRequest(Year, 1, null, Requests.Version(current.RowVersion)), default));
        Failed(update, ErrorType.Conflict);
        Failed(await Delete<PayrollRunDto>(processed.Id, Requests.Version(current.RowVersion)), ErrorType.Conflict);
    }

    private static void Set(object entity, string property, object value) =>
        entity.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, value);

    private Task<Result> ChangeAndSaveAsync<TEntity>(int id, Action<TEntity> change)
        where TEntity : Entity =>
        InScopeAsync(async sp =>
        {
            var entity = await sp.GetRequiredService<IRepository<TEntity>>().FindAsync(id, Predicates.Always<TEntity>(), default);
            change(entity!);
            return await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        });

    // ---- Returning to draft ----

    [IntegrationFact]
    public async Task An_approved_run_returns_to_draft_and_its_payslips_lines_and_warnings_are_deleted_after_the_run_is_saved()
    {
        var world = await SeedPayrollAsync();
        await SeedSecondEmployeeAsync(world, withSalary: false);
        var processed = await ProcessedAsync(await CreateRunAsync());
        await ActAsSecondUserAsync();
        var approved = Succeeded(await ApproveAsync(processed));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.PayrollRunWarning WHERE PayrollRunId = @id", ("@id", approved.Id)));

        var draft = Succeeded(await ReturnToDraftAsync(approved, "  Wrong overtime  "));

        Assert.Equal("Draft", draft.Status);
        Assert.Equal(0, draft.EmployeeCount);
        Assert.Equal(0m, draft.TotalNet);
        Assert.Empty(draft.Warnings);
        Assert.Null(draft.ApprovedBy);
        Assert.Null(draft.ProcessedBy);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.Payslip WHERE PayrollRunId = @id", ("@id", approved.Id)));
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.PayslipLine l JOIN pay.Payslip p ON p.Id = l.PayslipId WHERE p.PayrollRunId = @id", ("@id", approved.Id)));
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.PayrollRunWarning WHERE PayrollRunId = @id", ("@id", approved.Id)));
        Assert.Equal(
            "Draft",
            await ScalarAsync<string>("SELECT Status FROM pay.PayrollRun WHERE Id = @id AND ProcessedOn IS NULL AND ApprovedOn IS NULL AND PaidOn IS NULL AND TotalGross = 0", ("@id", approved.Id)));
        Assert.Equal(
            1,
            await ScalarAsync<int>(
                "SELECT COUNT(*) FROM app.ActivityLog WHERE Category = 'payroll' AND Message = @message AND ActorName LIKE @actor",
                ("@message", "Payroll for January 1999 returned to draft: Wrong overtime"),
                ("@actor", Tag + "%")));

        // It can be processed again.
        Assert.Equal("Pending Approval", Succeeded(await ProcessAsync(draft)).Status);
    }

    [IntegrationFact]
    public async Task A_pending_run_returns_to_draft_too()
    {
        await SeedPayrollAsync();
        var processed = await ProcessedAsync(await CreateRunAsync());

        var draft = Succeeded(await ReturnToDraftAsync(processed));

        Assert.Equal("Draft", draft.Status);
        Assert.Empty(Succeeded(await PayslipsOfAsync(processed.Id)).Items);
    }

    [IntegrationFact]
    public async Task A_failed_return_to_draft_changes_nothing()
    {
        await SeedPayrollAsync();
        var processed = await ProcessedAsync(await CreateRunAsync());
        BeforeNextSave = () => Sql.ExecuteAsync("UPDATE pay.PayrollRun SET PayDate = PayDate WHERE Id = @id", ("@id", processed.Id));

        // Another session saved the run between the check and the save: the row version no longer matches.
        var result = await ReturnToDraftAsync(processed);

        Failed(result, ErrorType.Conflict);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.Payslip WHERE PayrollRunId = @id", ("@id", processed.Id)));
        Assert.Equal("Pending Approval", await ScalarAsync<string>("SELECT Status FROM pay.PayrollRun WHERE Id = @id", ("@id", processed.Id)));
    }

    // ---- Setup data ----

    [IntegrationFact]
    public async Task A_structure_changes_its_components_in_the_real_unique_sort_order_and_a_used_component_cannot_be_deleted()
    {
        var world = await SeedPayrollAsync();
        var housing = world.Structure.ComponentIds[0];
        var tax = world.Structure.ComponentIds[2];
        var social = world.Structure.ComponentIds[3];
        var extra = await CreateComponentAsync("EXTRA", "Extra", "Earning", "Fixed", 5m);

        var reordered = await UpdateStructureAsync(world.Structure, [social, extra.Id, housing]);
        var again = await UpdateStructureAsync(reordered, [housing, social]);
        var back = await UpdateStructureAsync(again, [social, tax, housing, extra.Id]);

        Assert.Equal([social, extra.Id, housing], reordered.ComponentIds);
        Assert.Equal([housing, social], again.ComponentIds);
        Assert.Equal([social, tax, housing, extra.Id], back.ComponentIds);
        Assert.Equal([social, tax, housing, extra.Id], Succeeded(await Get<SalaryStructureDto>(world.Structure.Id)).ComponentIds);
        Assert.Equal(4, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.SalaryStructureComponent WHERE SalaryStructureId = @id", ("@id", world.Structure.Id)));

        var blocked = await Delete<SalaryComponentDto>(extra.Id, Requests.Version(extra.RowVersion));
        Assert.Contains("used by 1 salary structure(s)", Failed(blocked, ErrorType.Conflict).Message, StringComparison.Ordinal);
        FailedOnField(
            await UpdateStructureResultAsync(back, [tax, social]),
            "componentIds");
    }

    private async Task<SalaryStructureDto> UpdateStructureAsync(SalaryStructureDto current, int[] ids) =>
        Succeeded(await UpdateStructureResultAsync(current, ids));

    private Task<Result<SalaryStructureDto>> UpdateStructureResultAsync(SalaryStructureDto current, int[] ids) =>
        InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<SalaryStructureUpdateRequest, SalaryStructureDto>>().HandleAsync(
            current.Id,
            new SalaryStructureUpdateRequest(current.Code, current.Name, null, ids, Requests.Version(current.RowVersion)),
            default));

    [IntegrationFact]
    public async Task Salary_assignments_use_the_row_version_the_grade_band_and_keep_at_least_one_per_employee()
    {
        var world = await SeedPayrollAsync();
        var update = (Func<SalaryAssignmentDto, decimal, Task<Result<SalaryAssignmentDto>>>)((current, basic) =>
            InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<SalaryAssignmentUpdateRequest, SalaryAssignmentDto>>().HandleAsync(
                current.Id,
                new SalaryAssignmentUpdateRequest(current.EmployeeId, current.StructureId, basic, current.EffectiveFrom, current.Remarks, Requests.Version(current.RowVersion)),
                default)));

        var revised = Succeeded(await update(world.Assignment, 1500m));
        Failed(await update(world.Assignment, 1600m), ErrorType.Conflict); // the stale row version
        var outside = await update(revised, 5000.01m);
        Assert.Equal("Outside the " + Code("PG") + " band (500 – 5000).", FailedOnField(outside, "basicSalary"));

        var last = await Delete<SalaryAssignmentDto>(revised.Id, Requests.Version(revised.RowVersion));
        Assert.Equal("An employee needs at least one salary assignment. Add a new one instead.", Failed(last, ErrorType.Conflict).Message);

        var later = Succeeded(await CreateAsync<SalaryAssignmentCreateRequest, SalaryAssignmentDto>(
            new SalaryAssignmentCreateRequest(world.Employee.Id, world.Structure.Id, 1800m, new DateOnly(2001, 1, 1), "Raise")));
        FailedOnField(
            await CreateAsync<SalaryAssignmentCreateRequest, SalaryAssignmentDto>(
                new SalaryAssignmentCreateRequest(world.Employee.Id, world.Structure.Id, 1900m, new DateOnly(2001, 1, 1), null)),
            "effectiveFrom");
        FailedOnField(
            await CreateAsync<SalaryAssignmentCreateRequest, SalaryAssignmentDto>(
                new SalaryAssignmentCreateRequest(world.Employee.Id, world.Structure.Id, 1900m, new DateOnly(1997, 12, 31), null)),
            "effectiveFrom");
        Assert.True((await Delete<SalaryAssignmentDto>(revised.Id, Requests.Version(Succeeded(await Get<SalaryAssignmentDto>(revised.Id)).RowVersion))).IsSuccess);
        Assert.Equal(later.Id, Assert.Single(Succeeded(await Query<SalaryAssignmentDto, SalaryAssignmentFilter>(
            new PageRequest<SalaryAssignmentFilter>(0, 10, new SortState("effectiveFrom", "desc"), null, new SalaryAssignmentFilter(EmployeeId: world.Employee.Id)))).Items).Id);

        // The activity entries name the employee by code and never carry an amount.
        var messages = await ScalarAsync<int>(
            "SELECT COUNT(*) FROM app.ActivityLog WHERE Category = 'payroll' AND ActorName LIKE @actor AND (Message LIKE '%1500.%' OR Message LIKE '%1800.%' OR Message LIKE '%Mia%')",
            ("@actor", Tag + "%"));
        Assert.Equal(0, messages);
        Assert.Equal(
            1,
            await ScalarAsync<int>(
                "SELECT COUNT(*) FROM app.ActivityLog WHERE Message = @message AND ActorName LIKE @actor",
                ("@message", $"Salary assignment \"{world.Employee.EmployeeCode} from 2001-01-01\" created"),
                ("@actor", Tag + "%")));
    }

    [IntegrationFact]
    public async Task Employees_with_a_salary_or_a_payslip_cannot_be_deleted()
    {
        var world = await SeedPayrollAsync();

        var current = Succeeded(await Get<EmployeeDto>(world.Employee.Id));
        var withSalary = await Delete<EmployeeDto>(current.Id, Requests.Version(current.RowVersion));
        Assert.Contains("salary assignment(s)", Failed(withSalary, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    // ---- Reads ----

    [IntegrationFact]
    public async Task Every_sort_key_and_filter_of_the_payroll_lists_translates_to_sql()
    {
        var world = await SeedPayrollAsync();
        var run = await ProcessedAsync(await CreateRunAsync());

        foreach (var key in new[] { "id", "code", "name", "type", "calculation", "value", "status" })
        {
            Succeeded(await Query<SalaryComponentDto, SalaryComponentFilter>(
                new PageRequest<SalaryComponentFilter>(0, 5, new SortState(key, "desc"), Tag, new SalaryComponentFilter("Active", "Earning", "Fixed"))));
        }

        foreach (var key in new[] { "id", "code", "name", "status" })
        {
            Succeeded(await Query<SalaryStructureDto, SalaryStructureFilter>(
                new PageRequest<SalaryStructureFilter>(0, 5, new SortState(key, "asc"), Tag, new SalaryStructureFilter("Active"))));
        }

        foreach (var key in new[] { "id", "employeeId", "structureId", "basicSalary", "effectiveFrom" })
        {
            Succeeded(await Query<SalaryAssignmentDto, SalaryAssignmentFilter>(
                new PageRequest<SalaryAssignmentFilter>(0, 5, new SortState(key, "desc"), "x", new SalaryAssignmentFilter(world.Employee.Id, world.Structure.Id))));
        }

        foreach (var key in new[] { "id", "periodName", "year", "month", "startDate", "payDate", "status", "employeeCount", "totalGross", "totalNet" })
        {
            var page = Succeeded(await Query<PayrollRunDto, PayrollRunFilter>(
                new PageRequest<PayrollRunFilter>(0, 5, new SortState(key, "desc"), "January", new PayrollRunFilter("Pending Approval", Year))));
            Assert.Equal(run.Id, Assert.Single(page.Items).Id);
        }

        foreach (var key in new[] { "id", "runId", "employeeCode", "employeeName", "departmentName", "grossSalary", "netSalary", "generatedOn" })
        {
            var page = Succeeded(await Query<PayslipDto, PayslipFilter>(
                new PageRequest<PayslipFilter>(0, 1, new SortState(key, "desc"), "Mia", new PayslipFilter(run.Id, world.Employee.Id, world.DepartmentId))));
            Assert.Equal(1, page.Total);
            Assert.Equal(2, Assert.Single(page.Items).Earnings.Count);
        }

        var detail = Succeeded(await Get<PayslipDto>(Assert.Single(Succeeded(await PayslipsOfAsync(run.Id)).Items).Id));
        Assert.Equal(Masked, detail.AccountMasked);
        Assert.Equal(2, detail.Deductions.Count);
    }

    [IntegrationFact]
    public async Task A_run_in_draft_can_be_changed_and_deleted_and_the_pay_date_is_kept()
    {
        await SeedPayrollAsync();
        var run = await CreateRunAsync(3);

        var changed = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<PayrollRunUpdateRequest, PayrollRunDto>>()
            .HandleAsync(run.Id, new PayrollRunUpdateRequest(Year, 4, new DateOnly(1999, 4, 28), Requests.Version(run.RowVersion)), default)));

        Assert.Equal("April 1999", changed.PeriodName);
        Assert.Equal(new DateOnly(1999, 4, 28), changed.PayDate);
        Assert.Equal("1999-04-01", await ScalarAsync<string>("SELECT CONVERT(varchar(10), StartDate, 23) FROM pay.PayrollRun WHERE Id = @id", ("@id", run.Id)));
        Assert.True((await Delete<PayrollRunDto>(run.Id, Requests.Version(changed.RowVersion))).IsSuccess);
        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*) FROM pay.PayrollRun WHERE Id = @id", ("@id", run.Id)));
    }
}
