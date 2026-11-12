using System.Text.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Leave.LeaveTypes;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Application.Tests.Payroll;

/// <summary>
/// The payroll workflow through the real handlers: creating a period, processing it into payslips with the DEMONSTRATION
/// calculation, approving it by someone else, marking it paid and returning it to draft. Today is Monday 2026-11-09 in the fake
/// clock, so November 2026 is the current month. The figures are fictional.
/// </summary>
public sealed class PayrollRunHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private async Task<(PayrollSeed Seed, PayrollRunDto Run)> SeedWithRunAsync()
    {
        var seed = await host.SeedPayrollAsync();
        return (seed, await host.CreateRunAsync());
    }

    private async Task<PayslipDto> OnlyPayslipAsync(int runId)
    {
        var page = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: runId)));
        return Assert.Single(page.Items);
    }

    // ---- Creating, changing and deleting a period ----

    [Fact]
    public async Task A_period_starts_in_draft_with_its_dates_and_name_and_is_logged()
    {
        var run = await host.CreateRunAsync();

        Assert.Equal("Draft", run.Status);
        Assert.Equal("November 2026", run.PeriodName);
        Assert.Equal(new DateOnly(2026, 11, 1), run.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 30), run.EndDate);
        Assert.Equal(new DateOnly(2026, 11, 30), run.PayDate);
        Assert.Equal(0, run.EmployeeCount);
        Assert.Empty(run.Warnings);
        Assert.Equal("Payroll period \"November 2026\" created", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task There_is_one_period_per_month()
    {
        await host.CreateRunAsync();

        var error = ResultAssert.FailedOnField(await host.CreateRun(), "month", "A payroll period for November 2026 already exists.");

        Assert.Equal("This period already exists.", error.FieldErrors!["month"][0]);
    }

    [Fact]
    public async Task A_period_in_the_future_is_refused()
    {
        ResultAssert.FailedOnField(await host.CreateRun(2026, 12), "month", "A payroll period cannot be in the future.");
    }

    [Fact]
    public async Task A_draft_can_be_changed_and_deleted_and_a_processed_period_cannot()
    {
        var (_, run) = await SeedWithRunAsync();
        var update = host.Get<IUpdateHandler<PayrollRunUpdateRequest, PayrollRunDto>>();
        var delete = host.Get<IDeleteHandler<PayrollRunDto>>();

        var changed = ResultAssert.Succeeded(await update.HandleAsync(
            run.Id, new PayrollRunUpdateRequest(2026, 11, new DateOnly(2026, 11, 28), Requests.Version(run.RowVersion)), default));
        var processed = await host.ProcessedAsync(changed);
        var refusedChange = await update.HandleAsync(
            run.Id, new PayrollRunUpdateRequest(2026, 10, null, Requests.Version(processed.RowVersion)), default);
        var refusedDelete = await delete.HandleAsync(run.Id, Requests.Version(processed.RowVersion), default);

        Assert.Equal(new DateOnly(2026, 11, 28), changed.PayDate);
        Assert.Equal(
            "Only a draft period can be changed. Return it to draft first.",
            ResultAssert.Failed(refusedChange, ErrorType.Conflict).Message);
        Assert.Equal("Only a draft period can be deleted.", ResultAssert.Failed(refusedDelete, ErrorType.Conflict).Message);

        var returned = ResultAssert.Succeeded(await host.ReturnToDraft(processed));
        Assert.True((await delete.HandleAsync(run.Id, Requests.Version(returned.RowVersion), default)).IsSuccess);
        Assert.Equal("Payroll period \"November 2026\" deleted", host.ActivityLog.LastMessage);
    }

    // ---- Processing ----

    [Fact]
    public async Task Processing_makes_the_payslip_of_a_known_employee_with_the_demonstration_figures()
    {
        var (seed, run) = await SeedWithRunAsync();

        var processed = await host.ProcessedAsync(run);

        Assert.Equal("Pending Approval", processed.Status);
        Assert.Equal(1, processed.EmployeeCount);
        Assert.Equal(1000m, processed.TotalBasic);
        Assert.Equal(150m, processed.TotalAllowances);
        Assert.Equal(1150m, processed.TotalGross);
        Assert.Equal(80.5m, processed.TotalDeductions);
        Assert.Equal(1069.5m, processed.TotalNet);
        Assert.Equal(new DateOnly(2026, 11, 9), processed.ProcessedOn);
        Assert.Equal("Test User", processed.ProcessedBy);
        Assert.Null(processed.ApprovedBy);

        var payslip = await OnlyPayslipAsync(run.Id);
        Assert.Equal(seed.Employee.Id, payslip.EmployeeId);
        Assert.Equal(seed.Employee.EmployeeCode, payslip.EmployeeCode);
        Assert.Equal("Mia Hartley", payslip.EmployeeName);
        Assert.Equal("November 2026", payslip.PeriodName);
        Assert.Equal(seed.Org.Department.Name, payslip.DepartmentName);
        Assert.Equal(seed.Org.Position.Title, payslip.PositionTitle);
        Assert.Equal(seed.Org.Company.Name, payslip.CompanyName);
        Assert.Equal("Sample Bank", payslip.BankName);
        Assert.Equal(1000m, payslip.BasicSalary);
        Assert.Equal([("HOUSING", 100m, "10% of basic"), ("TRANSPORT", 50m, "Fixed 50")], payslip.Earnings.Select(line => (line.Code, line.Amount, line.Basis)));
        Assert.Equal([("TAX", 57.5m), ("SOCIAL", 23m)], payslip.Deductions.Select(line => (line.Code, line.Amount)));
        Assert.Equal(1150m, payslip.GrossSalary);
        Assert.Equal(80.5m, payslip.TotalDeductions);
        Assert.Equal(1069.5m, payslip.NetSalary);
        Assert.Equal(30, payslip.PeriodDays);
        Assert.Equal(30, payslip.DaysEmployed);
        Assert.Equal(21, payslip.WorkingDays);
        Assert.Equal(new DateOnly(2026, 11, 9), payslip.GeneratedOn);
        Assert.Equal("Payroll for November 2026 processed (1 payslips)", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task The_payslip_shows_only_the_masked_account_number()
    {
        var (_, run) = await SeedWithRunAsync();
        await host.ProcessedAsync(run);

        var payslip = await OnlyPayslipAsync(run.Id);
        var json = JsonSerializer.Serialize(payslip);

        Assert.Equal("•••• 7890", payslip.AccountMasked);
        Assert.DoesNotContain("1234567890", json, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", json, StringComparison.Ordinal);
        Assert.Equal(payslip.AccountMasked, ResultAssert.Succeeded(await host.Get<IGetByIdHandler<PayslipDto>>().HandleAsync(payslip.Id, default)).AccountMasked);
    }

    [Fact]
    public async Task Overtime_and_unpaid_leave_of_the_period_change_the_payslip_as_the_demonstration_rules_say()
    {
        var (seed, run) = await SeedWithRunAsync();
        host.AddHoliday(new DateOnly(2026, 11, 25), "Test holiday"); // 21 working days become 20
        host.AddApprovedOvertime(seed.Employee.Id, new DateOnly(2026, 11, 5), new TimeOnly(18, 0), new TimeOnly(20, 0));
        var unpaid = ResultAssert.Succeeded(await host.Get<ICreateHandler<LeaveTypeCreateRequest, LeaveTypeDto>>()
            .HandleAsync(LeaveFixtures.NewType("UNPAID", "Unpaid leave") with { IsPaid = false }, default));
        host.AddLeave(seed.Employee.Id, unpaid.Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3));
        host.AddLeave(seed.Employee.Id, unpaid.Id, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 10), approved: false); // not approved

        await host.ProcessedAsync(run);

        var payslip = await OnlyPayslipAsync(run.Id);
        Assert.Equal(20, payslip.WorkingDays);
        Assert.Equal(120, payslip.OvertimeMinutes);
        Assert.Equal(2m, payslip.UnpaidLeaveDays);
        Assert.Equal(18.75m, payslip.Earnings.Single(line => line.Code == "OT").Amount);
        Assert.Equal(100m, payslip.Deductions.Single(line => line.Code == "UL").Amount);
        Assert.Equal(1168.75m, payslip.GrossSalary);
        Assert.Equal(58.44m, payslip.Deductions.Single(line => line.Code == "TAX").Amount);
        Assert.Equal(payslip.GrossSalary - payslip.TotalDeductions, payslip.NetSalary);
    }

    [Fact]
    public async Task A_joiner_in_the_period_is_prorated_by_calendar_days()
    {
        var (seed, run) = await SeedWithRunAsync();
        var joiner = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test", joined: new DateOnly(2026, 11, 16));
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(joiner.Id, seed.Structure.Id, 1000m, new DateOnly(2026, 11, 16)));

        var processed = await host.ProcessedAsync(run);

        var payslips = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: run.Id, EmployeeId: joiner.Id)));
        var payslip = Assert.Single(payslips.Items);
        Assert.Equal(15, payslip.DaysEmployed);
        Assert.Equal(500m, payslip.BasicSalary);
        Assert.Equal([50m, 25m], payslip.Earnings.Select(line => line.Amount));
        Assert.Equal(575m, payslip.GrossSalary);
        Assert.Equal(2, processed.EmployeeCount);
        Assert.Equal(1500m, processed.TotalBasic);
    }

    [Fact]
    public async Task An_employee_who_left_before_the_period_or_joins_after_it_gets_no_payslip()
    {
        var (seed, run) = await SeedWithRunAsync();
        var later = await host.CreatePayrollEmployeeAsync(seed.Org, "later.test@example.test", joined: new DateOnly(2026, 12, 1));
        var left = await host.CreatePayrollEmployeeAsync(seed.Org, "left.test@example.test");
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(left.Id, seed.Structure.Id));
        ResultAssert.Succeeded(await host.Command<Apex.Hrms.Application.Employees.ExitEmployeeCommand, Apex.Hrms.Application.Employees.EmployeeDto>(
            new Apex.Hrms.Application.Employees.ExitEmployeeCommand(left.Id, Requests.Version(left.RowVersion), new DateOnly(2026, 10, 31), "Resigned")));

        var processed = await host.ProcessedAsync(run);

        Assert.Equal(1, processed.EmployeeCount);
        Assert.Empty(processed.Warnings);
        Assert.DoesNotContain(
            ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: run.Id))).Items,
            payslip => payslip.EmployeeId == later.Id || payslip.EmployeeId == left.Id);
    }

    [Fact]
    public async Task An_employee_without_a_salary_assignment_or_a_bank_account_is_skipped_with_a_warning()
    {
        var (seed, run) = await SeedWithRunAsync();
        var noSalary = await host.CreatePayrollEmployeeAsync(seed.Org, "nosalary.test@example.test");
        var noBank = await host.CreatePayrollEmployeeAsync(seed.Org, "nobank.test@example.test", withBank: false);
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(noBank.Id, seed.Structure.Id));

        var processed = await host.ProcessedAsync(run);

        Assert.Equal(1, processed.EmployeeCount);
        Assert.Equal(
            [
                $"{noSalary.EmployeeCode} Mia Hartley has no salary assignment and was skipped.",
                $"{noBank.EmployeeCode} Mia Hartley has no usable bank account and was skipped.",
            ],
            processed.Warnings);
        Assert.Equal(processed.Warnings, ResultAssert.Succeeded(await host.GetRun(run.Id)).Warnings);
    }

    [Fact]
    public async Task The_assignment_in_force_at_the_end_of_the_period_is_used()
    {
        var (seed, run) = await SeedWithRunAsync();
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 2000m, new DateOnly(2026, 11, 20)));
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(seed.Employee.Id, seed.Structure.Id, 3000m, new DateOnly(2026, 12, 1)));

        await host.ProcessedAsync(run);

        Assert.Equal(2000m, (await OnlyPayslipAsync(run.Id)).BasicSalary);
    }

    [Fact]
    public async Task A_period_with_nobody_to_pay_cannot_be_processed()
    {
        var run = await host.CreateRunAsync();

        var result = await host.Process(run);

        Assert.Equal("There are no employees to pay in this period.", ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Equal("Draft", ResultAssert.Succeeded(await host.GetRun(run.Id)).Status);
    }

    [Fact]
    public async Task Only_a_draft_can_be_processed_and_earlier_periods_must_be_finished_first()
    {
        await host.SeedPayrollAsync();
        var october = await host.CreateRunAsync(2026, 10);
        var november = await host.CreateRunAsync(2026, 11);

        var blocked = await host.Process(november);
        var processedOctober = await host.ProcessedAsync(october);
        var stillBlocked = await host.Process(ResultAssert.Succeeded(await host.GetRun(november.Id)));
        host.User.UserId = 2;
        host.User.DisplayName = "Alex Approver";
        ResultAssert.Succeeded(await host.Approve(processedOctober));
        host.User.UserId = 1;
        // The fake database stamps every record a refused change touched, so read the current row version again.
        var allowed = ResultAssert.Succeeded(await host.Process(ResultAssert.Succeeded(await host.GetRun(november.Id))));
        var again = await host.Process(ResultAssert.Succeeded(await host.GetRun(november.Id)));

        Assert.Equal(
            "Finish October 2026 (Draft) before processing November 2026.",
            ResultAssert.Failed(blocked, ErrorType.Conflict).Message);
        Assert.Equal(
            "Finish October 2026 (Pending Approval) before processing November 2026.",
            ResultAssert.Failed(stillBlocked, ErrorType.Conflict).Message);
        Assert.Equal("Pending Approval", allowed.Status);
        Assert.Equal(
            "Only a draft period can be processed (this one is Pending Approval).",
            ResultAssert.Failed(again, ErrorType.Conflict).Message);
    }

    [Fact]
    public async Task Acting_on_a_period_needs_a_signed_in_user()
    {
        var (_, run) = await SeedWithRunAsync();
        host.User.IsAuthenticated = false;
        host.User.UserId = null;

        ResultAssert.Failed(await host.Process(run), ErrorType.Unauthorized);
        ResultAssert.Failed(await host.Approve(run), ErrorType.Unauthorized);
    }

    [Fact]
    public async Task A_stale_row_version_is_a_conflict_and_a_missing_period_is_not_found()
    {
        var (_, run) = await SeedWithRunAsync();
        var processed = await host.ProcessedAsync(run);

        ResultAssert.Failed(await host.Process(run), ErrorType.Conflict); // the row version of the draft is stale now
        ResultAssert.Failed(
            await host.Command<ApprovePayrollRunCommand, PayrollRunDto>(new ApprovePayrollRunCommand(999, Requests.Version(processed.RowVersion))),
            ErrorType.NotFound);
        ResultAssert.FailedOnField(
            await host.Command<ApprovePayrollRunCommand, PayrollRunDto>(new ApprovePayrollRunCommand(processed.Id, null)),
            "rowVersion");
    }

    // ---- Approving and paying ----

    private async Task<PayrollRunDto> ApprovedAsync(PayrollRunDto processed)
    {
        host.User.UserId = 2;
        host.User.DisplayName = "Alex Approver";
        return ResultAssert.Succeeded(await host.Approve(processed));
    }

    [Fact]
    public async Task A_different_user_approves_and_the_run_keeps_who_processed_and_who_approved_it()
    {
        var (_, run) = await SeedWithRunAsync();
        var processed = await host.ProcessedAsync(run);

        var approved = await ApprovedAsync(processed);

        Assert.Equal("Approved", approved.Status);
        Assert.Equal("Test User", approved.ProcessedBy);
        Assert.Equal("Alex Approver", approved.ApprovedBy);
        Assert.Equal(new DateOnly(2026, 11, 9), approved.ApprovedOn);
        Assert.Equal("Payroll for November 2026 approved", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task Nobody_approves_a_run_they_processed()
    {
        var (_, run) = await SeedWithRunAsync();
        var processed = await host.ProcessedAsync(run);
        var saves = host.UnitOfWork.SaveCount;

        var result = await host.Approve(processed);

        Assert.Equal(
            "You processed this payroll period, so you cannot approve it. Another approver must do it.",
            ResultAssert.Failed(result, ErrorType.Conflict).Message);
        Assert.Equal(saves, host.UnitOfWork.SaveCount);
        Assert.Equal("Pending Approval", ResultAssert.Succeeded(await host.GetRun(run.Id)).Status);
    }

    [Fact]
    public async Task Only_a_pending_run_can_be_approved()
    {
        var (_, run) = await SeedWithRunAsync();

        Assert.Equal(
            "Only a period pending approval can be approved (this one is Draft).",
            ResultAssert.Failed(await host.Approve(run), ErrorType.Conflict).Message);
    }

    [Fact]
    public async Task An_approved_run_is_marked_paid_and_paid_is_final()
    {
        var (_, run) = await SeedWithRunAsync();
        var approved = await ApprovedAsync(await host.ProcessedAsync(run));

        var paid = ResultAssert.Succeeded(await host.MarkPaid(approved));

        Assert.Equal("Paid", paid.Status);
        Assert.Equal(new DateOnly(2026, 11, 9), paid.PaidOn);
        Assert.Equal(approved.TotalNet, paid.TotalNet);
        Assert.Equal("Payroll for November 2026 marked as paid (sample)", host.ActivityLog.LastMessage);
        ResultAssert.Failed(await host.MarkPaid(paid), ErrorType.Conflict);
        ResultAssert.Failed(await host.ReturnToDraft(paid), ErrorType.Conflict);
        ResultAssert.Failed(await host.Process(paid), ErrorType.Conflict);
        ResultAssert.Failed(await host.Approve(paid), ErrorType.Conflict);
    }

    [Fact]
    public async Task Only_an_approved_run_can_be_marked_paid()
    {
        var (_, run) = await SeedWithRunAsync();
        var processed = await host.ProcessedAsync(run);

        Assert.Equal(
            "Only an approved period can be marked as paid (this one is Pending Approval).",
            ResultAssert.Failed(await host.MarkPaid(processed), ErrorType.Conflict).Message);
    }

    // ---- Returning to draft ----

    [Fact]
    public async Task A_pending_run_returns_to_draft_and_loses_its_payslips_warnings_and_figures()
    {
        var (seed, run) = await SeedWithRunAsync();
        await host.CreatePayrollEmployeeAsync(seed.Org, "nosalary.test@example.test");
        var processed = await host.ProcessedAsync(run);
        Assert.NotEmpty(processed.Warnings);

        var draft = ResultAssert.Succeeded(await host.ReturnToDraft(processed, "  Wrong pay date  "));

        Assert.Equal("Draft", draft.Status);
        Assert.Equal(0, draft.EmployeeCount);
        Assert.Equal(0m, draft.TotalNet);
        Assert.Empty(draft.Warnings);
        Assert.Null(draft.ProcessedOn);
        Assert.Null(draft.ProcessedBy);
        Assert.Null(draft.ApprovedBy);
        Assert.Empty(ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: run.Id))).Items);
        Assert.Equal("Payroll for November 2026 returned to draft: Wrong pay date", host.ActivityLog.LastMessage);
    }

    [Fact]
    public async Task An_approved_run_returns_to_draft_and_can_be_processed_again()
    {
        var (_, run) = await SeedWithRunAsync();
        var approved = await ApprovedAsync(await host.ProcessedAsync(run));
        host.User.UserId = 1;

        var draft = ResultAssert.Succeeded(await host.ReturnToDraft(approved));
        var again = ResultAssert.Succeeded(await host.Process(draft));

        Assert.Equal("Draft", draft.Status);
        Assert.Null(draft.ApprovedBy);
        Assert.Null(draft.ApprovedOn);
        Assert.Equal("Pending Approval", again.Status);
        Assert.Equal("Payroll for November 2026 returned to draft", host.ActivityLog.Messages[^2]);
        Assert.Single(ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: run.Id))).Items);
    }

    [Fact]
    public async Task Returning_to_draft_saves_the_run_as_draft_before_the_payslips_and_warnings_are_deleted()
    {
        var (_, run) = await SeedWithRunAsync();
        var approved = await ApprovedAsync(await host.ProcessedAsync(run));
        var output = (FakePayrollRunOutput)host.Get<IPayrollRunOutput>();

        ResultAssert.Succeeded(await host.ReturnToDraft(approved));

        Assert.Equal(
            ["save", "delete payslips (run is Draft)", "delete warnings (run is Draft)"],
            output.Steps);
    }

    [Fact]
    public async Task A_draft_and_a_paid_run_cannot_return_to_draft_and_the_reason_is_limited()
    {
        var (_, run) = await SeedWithRunAsync();

        Assert.Equal(
            "Only a period pending approval or approved can be returned to draft (this one is Draft).",
            ResultAssert.Failed(await host.ReturnToDraft(run), ErrorType.Conflict).Message);
        var processed = await host.ProcessedAsync(run);
        ResultAssert.FailedOnField(await host.ReturnToDraft(processed, new string('x', 201)), "reason");
        Assert.True((await host.ReturnToDraft(processed, new string('x', 200))).IsSuccess);
    }

    [Fact]
    public async Task A_failed_save_when_returning_to_draft_deletes_nothing()
    {
        var (_, run) = await SeedWithRunAsync();
        var processed = await host.ProcessedAsync(run);
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("The record was changed or deleted by someone else. Reload it and try again.");

        var result = await host.ReturnToDraft(processed);

        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Single(ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: run.Id))).Items);
    }

    // ---- What the activity feed and the payslip list reveal ----

    [Fact]
    public async Task The_activity_entries_never_carry_an_amount_a_name_or_an_account_number()
    {
        var (seed, run) = await SeedWithRunAsync();
        var before = host.ActivityLog.Messages.Count;
        var processed = await host.ProcessedAsync(run);
        var approved = await ApprovedAsync(processed);
        await host.MarkPaid(approved);

        var messages = host.ActivityLog.Messages.Skip(before).ToList();
        Assert.Equal(3, messages.Count);
        foreach (var message in messages)
        {
            Assert.DoesNotContain("1000", message, StringComparison.Ordinal);
            Assert.DoesNotContain("1069", message, StringComparison.Ordinal);
            Assert.DoesNotContain("7890", message, StringComparison.Ordinal);
            Assert.DoesNotContain("Mia", message, StringComparison.Ordinal);
            Assert.DoesNotContain(seed.Employee.EmployeeCode, message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Payslips_are_listed_in_pages_by_run_employee_and_department_with_search_and_sort()
    {
        var (seed, run) = await SeedWithRunAsync();
        var second = await host.CreatePayrollEmployeeAsync(seed.Org, "ben.test@example.test");
        await host.CreateAssignmentAsync(PayrollFixtures.NewAssignment(second.Id, seed.Structure.Id, 2000m));
        await host.ProcessedAsync(run);
        var query = host.Get<IQueryHandler<PayslipDto, PayslipFilter>>();

        var page = ResultAssert.Succeeded(await query.HandleAsync(
            new PageRequest<PayslipFilter>(0, 1, new SortState("netSalary", "desc"), null, new PayslipFilter(RunId: run.Id)), default));
        var byEmployee = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(EmployeeId: seed.Employee.Id)));
        var byDepartment = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(DepartmentId: seed.Org.Department.Id)));
        var other = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(DepartmentId: 9999)));

        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(second.Id, page.Items[0].EmployeeId);
        Assert.Single(byEmployee.Items);
        Assert.Equal(2, byDepartment.Total);
        Assert.Empty(other.Items);
    }

    [Fact]
    public async Task A_payslip_that_does_not_exist_is_not_found()
    {
        ResultAssert.Failed(await host.Get<IGetByIdHandler<PayslipDto>>().HandleAsync(404, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task An_employee_with_a_payslip_cannot_be_deleted()
    {
        var (seed, run) = await SeedWithRunAsync();
        await host.ProcessedAsync(run);

        var result = await host.Get<IDeleteHandler<Apex.Hrms.Application.Employees.EmployeeDto>>()
            .HandleAsync(seed.Employee.Id, Requests.Version(seed.Employee.RowVersion), default);

        Assert.Contains("payslip(s)", ResultAssert.Failed(result, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }
}
