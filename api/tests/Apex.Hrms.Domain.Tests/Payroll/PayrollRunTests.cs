using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Payroll;

namespace Apex.Hrms.Domain.Tests.Payroll;

/// <summary>The workflow of a payroll period: which moves are allowed, which are refused and what each one changes.</summary>
public sealed class PayrollRunTests
{
    private static readonly DateOnly Today = new(2026, 11, 9);
    private static readonly DateTime Now = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
    private static readonly PayrollActor Processor = new(1, "Pat Processor");
    private static readonly PayrollActor Approver = new(2, "Alex Approver");
    private static readonly PayrollTotals Totals = new(2, 1500m, 1725m, 120m, 1605m);

    private static PayrollRun Draft(int year = 2026, int month = 11, DateOnly? payDate = null) =>
        PayrollRun.Create(new PayrollRunDetails(year, month, payDate), Today).Value;

    private static PayrollRun Pending()
    {
        var run = Draft();
        Assert.True(run.Process(Processor, Totals, ["EMP-0009 Test Person has no salary assignment and was skipped."], Today, Now).IsSuccess);
        return run;
    }

    private static PayrollRun Approved()
    {
        var run = Pending();
        Assert.True(run.Approve(Approver, Today).IsSuccess);
        return run;
    }

    private static PayrollRun Paid()
    {
        var run = Approved();
        Assert.True(run.MarkPaid(Today).IsSuccess);
        return run;
    }

    private static AppError Conflict(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        return result.Error;
    }

    [Fact]
    public void A_new_period_is_a_draft_with_dates_and_a_name_worked_out_from_the_month()
    {
        var run = Draft();

        Assert.Equal(PayrollRunStatus.Draft, run.Status);
        Assert.Equal("November 2026", run.PeriodName);
        Assert.Equal(new DateOnly(2026, 11, 1), run.StartDate);
        Assert.Equal(new DateOnly(2026, 11, 30), run.EndDate);
        Assert.Equal(run.EndDate, run.PayDate);
        Assert.Equal(0, run.EmployeeCount);
        Assert.Equal(0m, run.TotalNet);
        Assert.Null(run.ProcessedOn);
    }

    [Fact]
    public void February_of_a_leap_year_ends_on_the_29th()
    {
        Assert.Equal(new DateOnly(2024, 2, 29), PayrollRun.Create(new PayrollRunDetails(2024, 2, null), Today).Value.EndDate);
    }

    [Theory]
    [InlineData(2026, 0, "month")]
    [InlineData(2026, 13, "month")]
    [InlineData(2026, 12, "month")] // next month: in the future
    [InlineData(2027, 1, "month")]
    [InlineData(1800, 1, "year")]
    public void A_period_needs_a_real_month_that_is_not_in_the_future(int year, int month, string field)
    {
        var result = PayrollRun.Create(new PayrollRunDetails(year, month, null), Today);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(field, result.Error.FieldErrors!.Keys);
    }

    [Fact]
    public void The_current_month_is_allowed_and_the_pay_date_cannot_be_before_the_period_starts()
    {
        Assert.True(PayrollRun.Create(new PayrollRunDetails(2026, 11, null), Today).IsSuccess);

        var early = PayrollRun.Create(new PayrollRunDetails(2026, 11, new DateOnly(2026, 10, 31)), Today);

        Assert.True(early.IsFailure);
        Assert.Contains("payDate", early.Error.FieldErrors!.Keys);
        Assert.Equal("The pay date cannot be before the period starts.", early.Error.Message);
    }

    [Fact]
    public void A_draft_can_be_changed_and_anything_else_cannot()
    {
        var draft = Draft();
        Assert.True(draft.Update(new PayrollRunDetails(2026, 10, new DateOnly(2026, 10, 28)), Today).IsSuccess);
        Assert.Equal("October 2026", draft.PeriodName);
        Assert.Equal(new DateOnly(2026, 10, 28), draft.PayDate);

        Assert.Equal(
            "Only a draft period can be changed. Return it to draft first.",
            Conflict(Pending().Update(new PayrollRunDetails(2026, 10, null), Today)).Message);
    }

    [Fact]
    public void Only_a_draft_can_be_deleted()
    {
        Assert.True(Draft().EnsureCanDelete().IsSuccess);
        Assert.Equal("Only a draft period can be deleted.", Conflict(Pending().EnsureCanDelete()).Message);
        Assert.Equal("Only a draft period can be deleted.", Conflict(Paid().EnsureCanDelete()).Message);
    }

    [Fact]
    public void Processing_a_draft_moves_it_to_pending_approval_and_keeps_the_sums_the_notes_and_who_processed_it()
    {
        var run = Pending();

        Assert.Equal(PayrollRunStatus.PendingApproval, run.Status);
        Assert.Equal(2, run.EmployeeCount);
        Assert.Equal(1500m, run.TotalBasic);
        Assert.Equal(225m, run.TotalAllowances);
        Assert.Equal(1725m, run.TotalGross);
        Assert.Equal(120m, run.TotalDeductions);
        Assert.Equal(1605m, run.TotalNet);
        Assert.Equal(Today, run.ProcessedOn);
        Assert.Equal(1, run.ProcessedByUserId);
        Assert.Equal("Pat Processor", run.ProcessedByName);
        Assert.Single(run.Warnings);
        Assert.Null(run.ApprovedByUserId);
    }

    [Fact]
    public void Only_a_draft_can_be_processed()
    {
        Assert.Equal(
            "Only a draft period can be processed (this one is Pending Approval).",
            Conflict(Pending().Process(Processor, Totals, [], Today, Now)).Message);
        Assert.Equal(
            "Only a draft period can be processed (this one is Approved).",
            Conflict(Approved().Process(Processor, Totals, [], Today, Now)).Message);
        Assert.Equal(
            "Only a draft period can be processed (this one is Paid).",
            Conflict(Paid().Process(Processor, Totals, [], Today, Now)).Message);
    }

    [Fact]
    public void A_different_user_approves_a_pending_run()
    {
        var run = Pending();

        Assert.True(run.Approve(Approver, Today).IsSuccess);

        Assert.Equal(PayrollRunStatus.Approved, run.Status);
        Assert.Equal(2, run.ApprovedByUserId);
        Assert.Equal("Alex Approver", run.ApprovedByName);
        Assert.Equal(Today, run.ApprovedOn);
    }

    [Fact]
    public void Nobody_approves_a_run_they_processed()
    {
        var run = Pending();

        var error = Conflict(run.Approve(Processor, Today));

        Assert.Equal("You processed this payroll period, so you cannot approve it. Another approver must do it.", error.Message);
        Assert.Equal(PayrollRunStatus.PendingApproval, run.Status);
        Assert.Null(run.ApprovedByUserId);
    }

    [Fact]
    public void Only_a_pending_run_can_be_approved()
    {
        Assert.Equal("Only a period pending approval can be approved (this one is Draft).", Conflict(Draft().Approve(Approver, Today)).Message);
        Assert.Equal("Only a period pending approval can be approved (this one is Approved).", Conflict(Approved().Approve(Approver, Today)).Message);
        Assert.Equal("Only a period pending approval can be approved (this one is Paid).", Conflict(Paid().Approve(Approver, Today)).Message);
    }

    [Fact]
    public void Marking_paid_records_the_date_and_leaves_everything_else_alone()
    {
        var run = Approved();
        var before = (run.TotalNet, run.TotalGross, run.ProcessedByUserId, run.ApprovedByUserId, run.ApprovedOn, run.EmployeeCount);

        Assert.True(run.MarkPaid(new DateOnly(2026, 11, 10)).IsSuccess);

        Assert.Equal(PayrollRunStatus.Paid, run.Status);
        Assert.Equal(new DateOnly(2026, 11, 10), run.PaidOn);
        Assert.Equal(before, (run.TotalNet, run.TotalGross, run.ProcessedByUserId, run.ApprovedByUserId, run.ApprovedOn, run.EmployeeCount));
    }

    [Fact]
    public void Only_an_approved_run_can_be_marked_paid()
    {
        Assert.Equal("Only an approved period can be marked as paid (this one is Draft).", Conflict(Draft().MarkPaid(Today)).Message);
        Assert.Equal("Only an approved period can be marked as paid (this one is Pending Approval).", Conflict(Pending().MarkPaid(Today)).Message);
        Assert.Equal("Only an approved period can be marked as paid (this one is Paid).", Conflict(Paid().MarkPaid(Today)).Message);
    }

    [Fact]
    public void A_pending_run_returns_to_draft_with_its_figures_and_milestones_cleared()
    {
        var run = Pending();

        Assert.True(run.ReturnToDraft().IsSuccess);

        AssertCleared(run);
    }

    [Fact]
    public void An_approved_run_returns_to_draft_with_its_approval_cleared_too()
    {
        var run = Approved();

        Assert.True(run.ReturnToDraft().IsSuccess);

        AssertCleared(run);
    }

    [Fact]
    public void A_draft_and_a_paid_run_cannot_return_to_draft()
    {
        Assert.Equal(
            "Only a period pending approval or approved can be returned to draft (this one is Draft).",
            Conflict(Draft().ReturnToDraft()).Message);
        var paid = Paid();
        Assert.Equal(
            "Only a period pending approval or approved can be returned to draft (this one is Paid).",
            Conflict(paid.ReturnToDraft()).Message);
        Assert.Equal(PayrollRunStatus.Paid, paid.Status);
    }

    [Fact]
    public void Returning_to_draft_leaves_the_warning_collection_for_the_caller_to_delete_after_the_run_is_saved()
    {
        // Clearing the collection here would make the change tracker delete the notes in the same save as the run's update,
        // possibly before it: the database accepts that delete only once the run is Draft.
        var run = Approved();

        Assert.True(run.ReturnToDraft().IsSuccess);

        Assert.Single(run.Warnings);
    }

    private static void AssertCleared(PayrollRun run)
    {
        Assert.Equal(PayrollRunStatus.Draft, run.Status);
        Assert.Equal(0, run.EmployeeCount);
        Assert.Equal(0m, run.TotalBasic);
        Assert.Equal(0m, run.TotalAllowances);
        Assert.Equal(0m, run.TotalGross);
        Assert.Equal(0m, run.TotalDeductions);
        Assert.Equal(0m, run.TotalNet);
        Assert.Null(run.ProcessedOn);
        Assert.Null(run.ProcessedByUserId);
        Assert.Null(run.ProcessedByName);
        Assert.Null(run.ApprovedByUserId);
        Assert.Null(run.ApprovedByName);
        Assert.Null(run.ApprovedOn);
        Assert.Null(run.PaidOn);
    }
}
