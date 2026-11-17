using Apex.Hrms.Application.Organization.Companies;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>Evaluation periods: validation, the overlap rule and its boundaries, status commands, edit and delete restrictions, the picker.</summary>
public sealed class EvaluationPeriodHandlerTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private Task<Result<EvaluationPeriodDto>> Create(EvaluationPeriodCreateRequest request) =>
        host.Create<EvaluationPeriodCreateRequest, EvaluationPeriodDto>(request);

    private Task<Result<EvaluationPeriodDto>> Update(EvaluationPeriodDto current, EvaluationPeriodUpdateRequest request) =>
        host.Update<EvaluationPeriodUpdateRequest, EvaluationPeriodDto>(current.Id, request);

    private async Task<EvaluationPeriodDto> ActiveAsync(int companyId, string name = "2026 Q4")
    {
        var draft = await host.CreatePeriodAsync(companyId, name);
        return ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(draft));
    }

    // ---- Create and validation ----

    [Fact]
    public async Task A_period_is_created_in_draft_with_its_fields()
    {
        var company = await host.CreateCompanyAsync();

        var period = ResultAssert.Succeeded(await Create(KpiFixtures.NewPeriod(company.Id, "  2026 Q4 ", hr: true)));

        Assert.Equal("2026 Q4", period.Name);
        Assert.Equal("Draft", period.Status);
        Assert.Equal("Quarterly", period.PeriodType);
        Assert.Equal(new DateOnly(2026, 10, 1), period.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), period.EndDate);
        Assert.Equal(new DateOnly(2027, 1, 10), period.SubmissionDueDate);
        Assert.True(period.RequiresHrReview);
        Assert.NotEmpty(period.RowVersion);
    }

    [Fact]
    public async Task The_shape_and_the_date_rules_are_validated_with_a_field_error()
    {
        var company = await host.CreateCompanyAsync();

        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(company.Id, " ")), "name");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(company.Id, new string('n', 101))), "name");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(company.Id, type: "Weekly")), "periodType");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(company.Id, start: "2026-10-01", end: "2026-10-01")), "endDate");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(company.Id, start: "2026-10-01", end: "2026-09-30")), "endDate");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(company.Id, due: "2026-09-30")), "submissionDueDate");
    }

    [Fact]
    public async Task A_submission_due_date_on_the_start_date_or_after_the_end_date_or_missing_is_accepted()
    {
        var company = await host.CreateCompanyAsync();

        Assert.True((await Create(KpiFixtures.NewPeriod(company.Id, "A", "Custom", "2026-01-01", "2026-01-31", due: "2026-01-01"))).IsSuccess);
        Assert.True((await Create(KpiFixtures.NewPeriod(company.Id, "B", "Custom", "2026-02-01", "2026-02-28", due: "2026-03-30"))).IsSuccess);
        Assert.True((await Create(KpiFixtures.NewPeriod(company.Id, "C", "Custom", "2026-03-01", "2026-03-31", due: null))).IsSuccess);
    }

    [Fact]
    public async Task The_company_must_exist_and_be_active()
    {
        var inactive = await host.CreateCompanyAsync("OLD", "Old Company", "Inactive");

        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(9999)), "companyId");
        ResultAssert.FailedOnField(await Create(KpiFixtures.NewPeriod(inactive.Id)), "companyId");
    }

    [Fact]
    public async Task The_name_must_be_unique_within_a_company_but_may_repeat_in_another_company()
    {
        var first = await host.CreateCompanyAsync("AHT", "Apex Holdings");
        var second = await host.CreateCompanyAsync("BHT", "Beta Holdings");
        await host.CreatePeriodAsync(first.Id, "2026 Q4");

        var same = await Create(KpiFixtures.NewPeriod(first.Id, "2026 Q4", "Custom", "2027-01-01", "2027-01-31"));
        var otherCompany = await Create(KpiFixtures.NewPeriod(second.Id, "2026 Q4"));

        ResultAssert.FailedOnField(same, "name", "Period name \"2026 Q4\" already exists in this company.");
        Assert.True(otherCompany.IsSuccess);
    }

    // ---- Overlap ----

    [Theory]
    [InlineData("2026-12-31", "2027-03-31", true)]
    [InlineData("2026-09-30", "2026-10-01", true)]
    [InlineData("2026-11-01", "2026-11-30", true)]
    [InlineData("2026-09-01", "2027-02-28", true)]
    [InlineData("2027-01-01", "2027-03-31", false)]
    [InlineData("2026-07-01", "2026-09-30", false)]
    public async Task Periods_of_the_same_type_in_a_company_overlap_when_they_share_a_day_boundaries_included(string start, string end, bool overlaps)
    {
        var company = await host.CreateCompanyAsync();
        await host.CreatePeriodAsync(company.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31");

        var result = await Create(KpiFixtures.NewPeriod(company.Id, "Candidate", "Quarterly", start, end));

        if (overlaps)
        {
            var error = ResultAssert.Failed(result, ErrorType.Conflict);
            Assert.Contains("\"2026 Q4\"", error.Message, StringComparison.Ordinal);
            Assert.Contains("2026-10-01 to 2026-12-31", error.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.True(result.IsSuccess);
        }
    }

    [Fact]
    public async Task A_period_of_another_type_or_another_company_may_overlap()
    {
        var first = await host.CreateCompanyAsync("AHT", "Apex Holdings");
        var second = await host.CreateCompanyAsync("BHT", "Beta Holdings");
        await host.CreatePeriodAsync(first.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31");

        var yearly = await Create(KpiFixtures.NewPeriod(first.Id, "2026 Year", "Yearly", "2026-01-01", "2026-12-31"));
        var custom = await Create(KpiFixtures.NewPeriod(first.Id, "Q4 push", "Custom", "2026-11-01", "2026-11-30"));
        var otherCompany = await Create(KpiFixtures.NewPeriod(second.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31"));

        Assert.True(yearly.IsSuccess);
        Assert.True(custom.IsSuccess);
        Assert.True(otherCompany.IsSuccess);
    }

    [Fact]
    public async Task The_message_names_the_clashing_period_when_several_exist_it_names_the_earliest()
    {
        var company = await host.CreateCompanyAsync();
        await host.CreatePeriodAsync(company.Id, "Q3", "Custom", "2026-07-01", "2026-09-30");
        await host.CreatePeriodAsync(company.Id, "Q4", "Custom", "2026-10-01", "2026-12-31");

        var result = await Create(KpiFixtures.NewPeriod(company.Id, "Wide", "Custom", "2026-08-01", "2026-11-01"));

        Assert.Contains("\"Q3\"", ResultAssert.Failed(result, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Moving_a_draft_period_onto_another_period_is_refused_but_keeping_its_own_dates_is_not()
    {
        var company = await host.CreateCompanyAsync();
        var q4 = await host.CreatePeriodAsync(company.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31");
        var q1 = await host.CreatePeriodAsync(company.Id, "2027 Q1", "Quarterly", "2027-01-01", "2027-03-31");

        var same = ResultAssert.Succeeded(await Update(q4, KpiFixtures.PeriodUpdateOf(q4, name: "2026 Q4 plan")));
        var stretched = await Update(same, KpiFixtures.PeriodUpdateOf(same, end: new DateOnly(2027, 1, 1)));
        var asCustom = await Update(same, KpiFixtures.PeriodUpdateOf(same, type: "Custom", end: new DateOnly(2027, 1, 1)));

        Assert.Equal("2026 Q4 plan", same.Name);
        Assert.Contains("\"2027 Q1\"", ResultAssert.Failed(stretched, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.True(asCustom.IsSuccess);
        Assert.NotNull(q1);
    }

    // ---- Edits by status ----

    [Fact]
    public async Task A_draft_period_can_change_every_field()
    {
        var company = await host.CreateCompanyAsync();
        var period = await host.CreatePeriodAsync(company.Id);

        var changed = ResultAssert.Succeeded(await Update(period, KpiFixtures.PeriodUpdateOf(period, "2026 Q4 (new)", end: new DateOnly(2026, 12, 30), type: "Custom") with { RequiresHrReview = true }));

        Assert.Equal("2026 Q4 (new)", changed.Name);
        Assert.Equal("Custom", changed.PeriodType);
        Assert.Equal(new DateOnly(2026, 12, 30), changed.EndDate);
        Assert.True(changed.RequiresHrReview);
        Assert.Equal("Draft", changed.Status);
    }

    [Fact]
    public async Task An_active_period_changes_only_its_name_and_submission_due_date_and_anything_else_is_a_conflict()
    {
        var company = await host.CreateCompanyAsync();
        var period = await ActiveAsync(company.Id);

        var allowed = ResultAssert.Succeeded(await Update(period, KpiFixtures.PeriodUpdateOf(period, "2026 Q4 final", due: new DateOnly(2027, 1, 31))));
        var endChanged = await Update(allowed, KpiFixtures.PeriodUpdateOf(allowed, end: new DateOnly(2026, 12, 30)));
        var hrChanged = await Update(allowed, KpiFixtures.PeriodUpdateOf(allowed) with { RequiresHrReview = true });

        Assert.Equal("2026 Q4 final", allowed.Name);
        Assert.Equal(new DateOnly(2027, 1, 31), allowed.SubmissionDueDate);
        Assert.Equal("Active", allowed.Status);
        Assert.Contains("only its name and submission due date", ResultAssert.Failed(endChanged, ErrorType.Conflict).Message, StringComparison.Ordinal);
        ResultAssert.Failed(hrChanged, ErrorType.Conflict);
    }

    [Fact]
    public async Task An_active_period_still_needs_a_valid_and_unique_name()
    {
        var company = await host.CreateCompanyAsync();
        await host.CreatePeriodAsync(company.Id, "Taken", "Custom", "2027-01-01", "2027-01-31");
        var period = await ActiveAsync(company.Id);

        ResultAssert.FailedOnField(await Update(period, KpiFixtures.PeriodUpdateOf(period, "Taken")), "name");
        ResultAssert.FailedOnField(await Update(period, KpiFixtures.PeriodUpdateOf(period, " ")), "name");
        ResultAssert.FailedOnField(await Update(period, KpiFixtures.PeriodUpdateOf(period, due: new DateOnly(2026, 9, 30))), "submissionDueDate");
    }

    [Fact]
    public async Task A_completed_or_closed_period_cannot_be_edited()
    {
        var company = await host.CreateCompanyAsync();
        var active = await ActiveAsync(company.Id);
        var completed = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(active));

        var whenCompleted = await Update(completed, KpiFixtures.PeriodUpdateOf(completed, "Renamed"));
        var closed = ResultAssert.Succeeded(await host.MoveAsync<ClosePeriodCommand>(completed));
        var whenClosed = await Update(closed, KpiFixtures.PeriodUpdateOf(closed, "Renamed"));

        Assert.Contains("Completed", ResultAssert.Failed(whenCompleted, ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("Closed", ResultAssert.Failed(whenClosed, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stale_row_version_on_update_is_a_conflict_and_the_status_never_changes_through_put()
    {
        var company = await host.CreateCompanyAsync();
        var period = await host.CreatePeriodAsync(company.Id);
        ResultAssert.Succeeded(await Update(period, KpiFixtures.PeriodUpdateOf(period, "First")));

        var stale = await Update(period, KpiFixtures.PeriodUpdateOf(period, "Second"));
        var current = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<EvaluationPeriodDto>>().HandleAsync(period.Id, default));

        ResultAssert.Failed(stale, ErrorType.Conflict);
        Assert.Equal("Draft", current.Status);
        Assert.Equal("First", current.Name);
        Assert.DoesNotContain(typeof(EvaluationPeriodUpdateRequest).GetProperties(), property => property.Name == "Status");
    }

    // ---- Status commands ----

    [Fact]
    public async Task The_commands_move_a_period_through_its_life_and_return_the_new_row_version()
    {
        var company = await host.CreateCompanyAsync();
        var draft = await host.CreatePeriodAsync(company.Id);

        var active = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(draft));
        var completed = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(active));
        var closed = ResultAssert.Succeeded(await host.MoveAsync<ClosePeriodCommand>(completed));

        Assert.Equal(["Active", "Completed", "Closed"], [active.Status, completed.Status, closed.Status]);
        Assert.NotEqual(draft.RowVersion, active.RowVersion);
        Assert.NotEqual(active.RowVersion, completed.RowVersion);
        Assert.NotEqual(completed.RowVersion, closed.RowVersion);
    }

    [Fact]
    public async Task A_command_out_of_order_is_a_conflict_that_names_the_period_and_changes_nothing()
    {
        var company = await host.CreateCompanyAsync();
        var draft = await host.CreatePeriodAsync(company.Id);

        var complete = await host.MoveAsync<CompletePeriodCommand>(draft);
        var close = await host.MoveAsync<ClosePeriodCommand>(draft);
        var active = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(draft));
        var again = await host.MoveAsync<ActivatePeriodCommand>(active);

        Assert.Contains("only a Active period can be completed", ResultAssert.Failed(complete, ErrorType.Conflict).Message, StringComparison.Ordinal);
        ResultAssert.Failed(close, ErrorType.Conflict);
        ResultAssert.Failed(again, ErrorType.Conflict);
        Assert.Equal("Active", ResultAssert.Succeeded(await host.Get<IGetByIdHandler<EvaluationPeriodDto>>().HandleAsync(draft.Id, default)).Status);
    }

    [Fact]
    public async Task A_command_needs_the_current_row_version_and_an_existing_period()
    {
        var company = await host.CreateCompanyAsync();
        var draft = await host.CreatePeriodAsync(company.Id);
        ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(draft));

        var stale = await host.MoveAsync<CompletePeriodCommand>(draft);
        var missing = await host.Command<ActivatePeriodCommand, EvaluationPeriodDto>(new ActivatePeriodCommand(9999, Requests.Version(draft.RowVersion)));
        var noVersion = await host.Command<ActivatePeriodCommand, EvaluationPeriodDto>(new ActivatePeriodCommand(draft.Id, null));

        ResultAssert.Failed(stale, ErrorType.Conflict);
        ResultAssert.Failed(missing, ErrorType.NotFound);
        ResultAssert.FailedOnField(noVersion, "rowVersion");
    }

    [Fact]
    public async Task Each_move_and_every_other_change_is_logged_under_performance()
    {
        var company = await host.CreateCompanyAsync();
        var draft = await host.CreatePeriodAsync(company.Id);
        var changed = ResultAssert.Succeeded(await Update(draft, KpiFixtures.PeriodUpdateOf(draft, "2026 Q4 plan")));
        var active = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(changed));
        var completed = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(active));
        ResultAssert.Succeeded(await host.MoveAsync<ClosePeriodCommand>(completed));

        var performance = host.ActivityLog.Entries.Where(entry => entry.Category == ActivityCategories.Performance).Select(entry => entry.Message).ToList();

        Assert.Equal(
            [
                "Evaluation period \"2026 Q4\" created",
                "Evaluation period \"2026 Q4 plan\" updated",
                "Evaluation period \"2026 Q4 plan\" activated",
                "Evaluation period \"2026 Q4 plan\" completed",
                "Evaluation period \"2026 Q4 plan\" closed",
            ],
            performance);
    }

    // ---- Delete ----

    [Fact]
    public async Task An_unused_draft_period_can_be_deleted_and_the_deletion_is_logged()
    {
        var company = await host.CreateCompanyAsync();
        var draft = await host.CreatePeriodAsync(company.Id);

        var result = await host.Delete<EvaluationPeriodDto>(draft.Id, draft.RowVersion);

        Assert.True(result.IsSuccess);
        ResultAssert.Failed(await host.Get<IGetByIdHandler<EvaluationPeriodDto>>().HandleAsync(draft.Id, default), ErrorType.NotFound);
        Assert.Equal("Evaluation period \"2026 Q4\" deleted", host.ActivityLog.LastMessage);
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Completed")]
    [InlineData("Closed")]
    public async Task Only_a_draft_period_can_be_deleted(string status)
    {
        var company = await host.CreateCompanyAsync();
        var period = await host.CreatePeriodAsync(company.Id);
        if (status != "Draft")
        {
            period = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(period));
        }

        if (status is "Completed" or "Closed")
        {
            period = ResultAssert.Succeeded(await host.MoveAsync<CompletePeriodCommand>(period));
        }

        if (status == "Closed")
        {
            period = ResultAssert.Succeeded(await host.MoveAsync<ClosePeriodCommand>(period));
        }

        var result = await host.Delete<EvaluationPeriodDto>(period.Id, period.RowVersion);

        Assert.Contains($"is {status}; only a Draft period can be deleted", ResultAssert.Failed(result, ErrorType.Conflict).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_period_with_scorecards_cannot_be_deleted_and_the_count_is_in_the_message()
    {
        var org = await host.SeedOrgAsync();
        var period = await host.CreatePeriodAsync(org.Company.Id);
        host.Store(Scorecard.Create(10, period.Id, org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, null).Value);
        host.Store(Scorecard.Create(11, period.Id, org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, null).Value);

        var result = await host.Delete<EvaluationPeriodDto>(period.Id, period.RowVersion);

        Assert.Equal(
            "\"2026 Q4\" cannot be deleted because it is used by 2 scorecard(s).",
            ResultAssert.Failed(result, ErrorType.Conflict).Message);
    }

    [Fact]
    public async Task A_company_with_periods_cannot_be_deleted()
    {
        var company = await host.CreateCompanyAsync();
        await host.CreatePeriodAsync(company.Id);

        var result = await host.Delete<CompanyDto>(company.Id, company.RowVersion);

        ResultAssert.Failed(result, ErrorType.Conflict, "\"Apex Holdings\" cannot be deleted because it is used by 1 evaluation period(s). Deactivate it instead.");
    }

    // ---- Query and the picker ----

    [Fact]
    public async Task Periods_are_filtered_by_company_type_status_year_and_search()
    {
        var first = await host.CreateCompanyAsync("AHT", "Apex Holdings");
        var second = await host.CreateCompanyAsync("BHT", "Beta Holdings");
        var q4 = await host.CreatePeriodAsync(first.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31");
        await host.CreatePeriodAsync(first.Id, "2027 Q1", "Quarterly", "2027-01-01", "2027-03-31");
        await host.CreatePeriodAsync(first.Id, "2026 Year", "Yearly", "2026-01-01", "2026-12-31");
        await host.CreatePeriodAsync(second.Id, "Beta 2026", "Custom", "2026-05-01", "2026-05-31");
        ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(q4));
        var query = host.Get<IQueryHandler<EvaluationPeriodDto, EvaluationPeriodFilter>>();

        async Task<string[]> Names(string? search = null, EvaluationPeriodFilter? filter = null) =>
            [.. ResultAssert.Succeeded(await query.HandleAsync(new PageRequest<EvaluationPeriodFilter>(0, 50, new SortState("startDate", "asc"), search, filter ?? new EvaluationPeriodFilter()), default)).Items.Select(p => p.Name)];

        Assert.Equal(["2026 Year", "Beta 2026", "2026 Q4", "2027 Q1"], await Names());
        Assert.Equal(["2026 Year", "2026 Q4", "2027 Q1"], await Names(filter: new EvaluationPeriodFilter(CompanyId: first.Id)));
        Assert.Equal(["2026 Q4", "2027 Q1"], await Names(filter: new EvaluationPeriodFilter(PeriodType: "Quarterly")));
        Assert.Equal(["2026 Q4"], await Names(filter: new EvaluationPeriodFilter(Status: "Active")));
        Assert.Equal(["2026 Year", "Beta 2026", "2026 Q4"], await Names(filter: new EvaluationPeriodFilter(Year: 2026)));
        Assert.Equal(["2027 Q1"], await Names(filter: new EvaluationPeriodFilter(Year: 2027)));
        Assert.Equal(["Beta 2026"], await Names("Beta"));
        Assert.Empty(await Names(filter: new EvaluationPeriodFilter(PeriodType: "Weekly")));
        Assert.Empty(await Names(filter: new EvaluationPeriodFilter(Status: "Open")));
    }

    [Fact]
    public async Task The_picker_lists_periods_that_are_not_draft_newest_first_and_can_be_narrowed_by_company()
    {
        var first = await host.CreateCompanyAsync("AHT", "Apex Holdings");
        var second = await host.CreateCompanyAsync("BHT", "Beta Holdings");
        var draft = await host.CreatePeriodAsync(first.Id, "Draft one", "Custom", "2027-04-01", "2027-04-30", due: null);
        var q3 = await host.CreatePeriodAsync(first.Id, "2026 Q3", "Quarterly", "2026-07-01", "2026-09-30", due: null, hr: true);
        var q4 = await host.CreatePeriodAsync(first.Id, "2026 Q4", "Quarterly", "2026-10-01", "2026-12-31");
        var beta = await host.CreatePeriodAsync(second.Id, "Beta Q4", "Quarterly", "2026-10-01", "2026-12-31");
        ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(q3));
        var activeQ4 = ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(q4));
        ResultAssert.Succeeded(await host.MoveAsync<ActivatePeriodCommand>(beta));
        var picker = host.Get<IEvaluationPeriodOptionsHandler>();

        var all = ResultAssert.Succeeded(await picker.HandleAsync(null, default));
        var firstOnly = ResultAssert.Succeeded(await picker.HandleAsync(first.Id, default));
        var nobody = ResultAssert.Succeeded(await picker.HandleAsync(9999, default));

        Assert.Equal(["2026 Q4", "Beta Q4", "2026 Q3"], all.Select(option => option.Name));
        Assert.Equal(["2026 Q4", "2026 Q3"], firstOnly.Select(option => option.Name));
        Assert.Empty(nobody);
        Assert.DoesNotContain(all, option => option.Id == draft.Id);
        var option = firstOnly[0];
        Assert.Equal((activeQ4.Id, first.Id, "Quarterly", "Active", false), (option.Id, option.CompanyId, option.PeriodType, option.Status, option.RequiresHrReview));
        Assert.Equal(new DateOnly(2026, 10, 1), option.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), option.EndDate);
        Assert.True(firstOnly[1].RequiresHrReview);
        Assert.Equal(new DateOnly(2027, 1, 10), option.SubmissionDueDate);
        Assert.Null(firstOnly[1].SubmissionDueDate);
    }

    // ---- The period lock (the overlap check cannot race) ----

    [Fact]
    public async Task Create_and_update_run_in_a_committed_transaction_under_the_period_lock_of_the_company()
    {
        var company = await host.CreateCompanyAsync();
        host.UnitOfWork.TransactionLocks.Clear();
        var commits = host.UnitOfWork.Commits;

        var created = ResultAssert.Succeeded(await Create(KpiFixtures.NewPeriod(company.Id)));
        ResultAssert.Succeeded(await Update(created, KpiFixtures.PeriodUpdateOf(created, "Renamed")));

        Assert.Equal([$"kpi-period:{company.Id}", $"kpi-period:{company.Id}"], host.UnitOfWork.TransactionLocks);
        Assert.Equal(commits + 2, host.UnitOfWork.Commits);
    }

    [Fact]
    public async Task A_refused_create_is_not_committed()
    {
        var company = await host.CreateCompanyAsync();
        ResultAssert.Succeeded(await Create(KpiFixtures.NewPeriod(company.Id, "First")));
        var commits = host.UnitOfWork.Commits;

        var clash = await Create(KpiFixtures.NewPeriod(company.Id, "Second"));

        ResultAssert.Failed(clash, ErrorType.Conflict);
        Assert.Equal(commits, host.UnitOfWork.Commits);
    }

    [Fact]
    public async Task While_another_change_holds_the_company_period_lock_a_create_or_update_is_a_409_that_stores_nothing()
    {
        var company = await host.CreateCompanyAsync();
        var other = await host.CreateCompanyAsync("OTHER", "Other Company");
        var existing = ResultAssert.Succeeded(await Create(KpiFixtures.NewPeriod(company.Id, "Existing", start: "2027-01-01", end: "2027-03-31", due: null)));
        host.UnitOfWork.HeldElsewhere.Add($"kpi-period:{company.Id}");

        var created = await Create(KpiFixtures.NewPeriod(company.Id));
        var updated = await Update(existing, KpiFixtures.PeriodUpdateOf(existing, "Renamed"));
        var elsewhere = await Create(KpiFixtures.NewPeriod(other.Id));

        Assert.Equal("Another change to the evaluation periods of this company is in progress. Try again.", ResultAssert.Failed(created, ErrorType.Conflict).Message);
        Assert.Equal("Another change to the evaluation periods of this company is in progress. Try again.", ResultAssert.Failed(updated, ErrorType.Conflict).Message);
        ResultAssert.Succeeded(elsewhere);
        Assert.Equal(["Existing", "2026 Q4"], host.Repository<EvaluationPeriod>().Items.OrderBy(period => period.Id).Select(period => period.Name));
        Assert.Equal(other.Id, host.Repository<EvaluationPeriod>().Items.Single(period => period.Name == "2026 Q4").CompanyId);
    }
}
