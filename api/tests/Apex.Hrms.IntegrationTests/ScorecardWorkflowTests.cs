using Apex.Hrms.Application;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Categories;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Org = (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position);

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The employee's results and submission and the reviewer's steps against SQL Server: the handlers with the real EF mapping, the CHECK
/// constraints of the scorecard tables, the single row version that guards a scorecard and its items, the temporal history that keeps the
/// figures of an earlier submission, and the append-only review trail. Fictional data only; every row carries the tag of its test. These
/// handler calls hold every grant (the HR tier), so who may act is tested over HTTP with real roles; here the caller's account and
/// employee decide the rules that do not depend on grants (own scorecard, separation of duties).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ScorecardWorkflowTests : DatabaseTest
{
    private sealed record Setup(
        Org Org,
        EvaluationPeriodDto Period,
        EmployeeDto Manager,
        EmployeeDto Employee,
        int ManagerUser,
        int EmployeeUser,
        ScorecardDetailDto Card,
        ScorecardItemDto First,
        ScorecardItemDto Second);

    private async Task<EmployeeDto> EmployeeAsync(Org org, string suffix, string local, int? managerId = null) =>
        Succeeded(await Create(
            Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, suffix, local) with { ManagerId = managerId }));

    /// <summary>
    /// A manager and a report of hers, a plan of two KPIs (60% and 40%, target 100) finalized for the report in an Active period. The
    /// reviewer is the manager. The caller is left as the administrator.
    /// </summary>
    private async Task<Setup> SetupAsync(bool hrReview = false)
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var first = await SeedKpiTemplateAsync(category.Id, "S-01", "Monthly sales");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02", "Quality score");
        var draft = Succeeded(await Create(Requests.Period(org.Company.Id, hr: hrReview)));
        var period = Succeeded(await RunCommand<ActivatePeriodCommand, EvaluationPeriodDto>(new ActivatePeriodCommand(draft.Id, Requests.Version(draft.RowVersion))));
        var manager = await EmployeeAsync(org, "1", "anna");
        var employee = await EmployeeAsync(org, "2", "ben", manager.Id);
        var managerUser = (await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "anna-login"), employeeId: manager.Id)).AccountId;
        var employeeUser = (await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, "ben-login"), employeeId: employee.Id)).AccountId;
        var card = Succeeded(await FinalizePlan(await SeedScorecardAsync(employee.Id, period.Id, ItemOf(first.Id, weight: 60m), ItemOf(second.Id, weight: 40m))));
        return new Setup(org, period, manager, employee, managerUser, employeeUser, card, ItemNamed(card, "S-01"), ItemNamed(card, "S-02"));
    }

    private ScorecardItemDto ItemNamed(ScorecardDetailDto card, string suffix) => card.Items.Single(item => item.KpiCode == Code(suffix));

    private void ActAsEmployee(Setup setup)
    {
        CurrentUserId = setup.EmployeeUser;
        CurrentEmployeeId = setup.Employee.Id;
        CurrentUserName = $"{Tag} Ben";
    }

    private void ActAsManager(Setup setup)
    {
        CurrentUserId = setup.ManagerUser;
        CurrentEmployeeId = setup.Manager.Id;
        CurrentUserName = $"{Tag} Anna";
    }

    private void ActAsAdministrator()
    {
        CurrentUserId = null;
        CurrentEmployeeId = null;
        CurrentUserName = null;
    }

    private async Task<int> ActAsOtherHrUserAsync(string name)
    {
        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, name));
        CurrentUserId = account.AccountId;
        CurrentEmployeeId = null;
        CurrentUserName = $"{Tag} {name}";
        return account.AccountId;
    }

    private static ResultItemRequest Actual(ScorecardItemDto item, decimal? actual, string? comment = null) => new(item.Id, actual, comment);

    private Task<Result<ScorecardDetailDto>> SaveResults(ScorecardDetailDto card, params ResultItemRequest[] items) =>
        InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<SaveScorecardResultsRequest, ScorecardDetailDto>>()
            .HandleAsync(card.Id, new SaveScorecardResultsRequest(Requests.Version(card.RowVersion), null, items), default));

    private Task<Result<ScorecardDetailDto>> SaveReview(ScorecardDetailDto card, decimal? rating, params ReviewItemRequest[] items) =>
        InScopeAsync(sp => sp.GetRequiredService<IUpdateHandler<SaveScorecardReviewRequest, ScorecardDetailDto>>()
            .HandleAsync(card.Id, new SaveScorecardReviewRequest(Requests.Version(card.RowVersion), rating, "Solid quarter", items), default));

    private Task<Result<ScorecardDetailDto>> Submit(ScorecardDetailDto card, string? rowVersion = null) =>
        RunCommand<SubmitScorecardCommand, ScorecardDetailDto>(new SubmitScorecardCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion)));

    private Task<Result<ScorecardDetailDto>> StartReview(ScorecardDetailDto card) =>
        RunCommand<StartScorecardReviewCommand, ScorecardDetailDto>(new StartScorecardReviewCommand(card.Id, Requests.Version(card.RowVersion)));

    private Task<Result<ScorecardDetailDto>> Approve(ScorecardDetailDto card, string? comment = null, string? rowVersion = null) =>
        RunCommand<ApproveScorecardCommand, ScorecardDetailDto>(new ApproveScorecardCommand(card.Id, rowVersion ?? Requests.Version(card.RowVersion), comment));

    private Task<Result<ScorecardDetailDto>> Return(ScorecardDetailDto card, string? comment) =>
        RunCommand<ReturnScorecardCommand, ScorecardDetailDto>(new ReturnScorecardCommand(card.Id, Requests.Version(card.RowVersion), comment));

    private Task<Result<ScorecardDetailDto>> Reject(ScorecardDetailDto card, string? comment) =>
        RunCommand<RejectScorecardCommand, ScorecardDetailDto>(new RejectScorecardCommand(card.Id, Requests.Version(card.RowVersion), comment));

    private Task<Result<ScorecardDetailDto>> FinalizeByHr(ScorecardDetailDto card, string? comment = null) =>
        RunCommand<FinalizeScorecardCommand, ScorecardDetailDto>(new FinalizeScorecardCommand(card.Id, Requests.Version(card.RowVersion), comment));

    private async Task<ScorecardDetailDto> FilledAsync(Setup setup, decimal first = 100m, decimal second = 50m)
    {
        ActAsEmployee(setup);
        return Succeeded(await SaveResults(setup.Card, Actual(setup.First, first), Actual(setup.Second, second)));
    }

    private async Task<ScorecardDetailDto> SubmittedAsync(Setup setup) => Succeeded(await Submit(await FilledAsync(setup)));

    private static Task<T> Scalar<T>(string sql, params (string Name, object? Value)[] parameters) => Sql.ScalarAsync<T>(sql, parameters);

    private static Task<string> StatusOf(int id) => Scalar<string>("SELECT Status FROM kpi.Scorecard WHERE Id = @id", ("@id", id));

    private static Task<string> Trail(int id) => Scalar<string>(
        "SELECT ISNULL(STRING_AGG(CONCAT(Step, '/', Action, '/', FromStatus, '>', ToStatus), ';') WITHIN GROUP (ORDER BY Id), '') FROM kpi.ReviewAction WHERE ScorecardId = @id",
        ("@id", id));

    private static Task<string> RowVersionOf(int id) => Scalar<string>("SELECT CONVERT(varchar(20), RowVersion, 1) FROM kpi.Scorecard WHERE Id = @id", ("@id", id));

    // ---- The whole path ----

    [IntegrationFact]
    public async Task A_scorecard_goes_from_draft_to_finalized_with_every_figure_stored_and_every_step_in_the_trail()
    {
        var setup = await SetupAsync();
        var filled = await FilledAsync(setup);

        Assert.Equal((100m, 60m), (ItemOf(filled, "S-01").AchievementPct, ItemOf(filled, "S-01").WeightedScore));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @id AND OverallScore IS NULL AND CalculationVersion IS NULL AND Status = 'Draft'", ("@id", setup.Card.Id)));

        var submitted = Succeeded(await Submit(filled));

        Assert.Equal(("Submitted", 80m), (submitted.Status, submitted.OverallScore));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @id AND OverallScore = 80 AND CalculationVersion = 1 AND SubmittedOn = '2026-11-09' AND RatingLabel IS NOT NULL",
            ("@id", setup.Card.Id)));
        var expectedLabel = await Scalar<string>("SELECT TOP 1 Label FROM kpi.RatingBand WHERE Status = 'Active' AND MinScore <= 80 ORDER BY MinScore DESC");
        Assert.Equal(expectedLabel, submitted.RatingLabel);

        ActAsManager(setup);
        var started = Succeeded(await StartReview(submitted));
        var reviewed = Succeeded(await SaveReview(started, 4.5m, new ReviewItemRequest(setup.First.Id, 5m, "Great"), new ReviewItemRequest(setup.Second.Id, null, null)));
        Assert.Equal(("Under Review", 4.5m, "Solid quarter"), (reviewed.Status, reviewed.ManagerRating, reviewed.ManagerComment));
        var finalized = Succeeded(await Approve(reviewed, "Well done"));

        Assert.Equal(("Finalized", 80m, "2026-11-09"), (finalized.Status, finalized.OverallScore, finalized.FinalizedOn?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @id AND Status = 'Finalized' AND FinalizedOn = '2026-11-09' AND ReviewedOn = '2026-11-09' AND FinalizedByUserId = @user AND OverallScore = 80",
            ("@id", setup.Card.Id),
            ("@user", setup.ManagerUser)));
        Assert.Equal(
            "HR/Finalize Plan/Planning>Draft;Employee/Submit/Draft>Submitted;Manager/Start Review/Submitted>Under Review;Manager/Approve/Under Review>Finalized",
            await Trail(setup.Card.Id));
        Assert.Equal(1, await Scalar<int>(
            "SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @id AND Action = 'Approve' AND OverallScore = 80 AND Comment = N'Well done' AND ActorUserId = @user AND ActorEmployeeId = @employee AND ActorName = @name",
            ("@id", setup.Card.Id),
            ("@user", setup.ManagerUser),
            ("@employee", setup.Manager.Id),
            ("@name", $"{Tag} Anna")));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM app.ActivityLog WHERE Category = 'performance' AND Message LIKE '%approved and finalized%' AND Message LIKE @code", ("@code", $"%{setup.Employee.EmployeeCode}%")));
    }

    private static ScorecardItemDto ItemOf(ScorecardDetailDto card, string suffix) => card.Items.Single(item => item.KpiCode.EndsWith(suffix, StringComparison.Ordinal));

    [IntegrationFact]
    public async Task A_period_with_an_hr_review_holds_the_scorecard_as_approved_and_the_approver_cannot_finalize_it_but_another_user_can()
    {
        var setup = await SetupAsync(hrReview: true);
        var submitted = await SubmittedAsync(setup);
        ActAsManager(setup);
        var approved = Succeeded(await Approve(submitted));

        Assert.Equal("Approved", approved.Status);
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @id AND Status = 'Approved' AND FinalizedOn IS NULL AND FinalizedByUserId IS NULL AND OverallScore = 80", ("@id", setup.Card.Id)));

        var same = Failed(await FinalizeByHr(approved), ErrorType.Conflict);
        Assert.Equal("The person who approved this scorecard cannot also give the HR final review.", same.Message);
        Assert.Equal("Approved", await StatusOf(setup.Card.Id));

        var hrUser = await ActAsOtherHrUserAsync("hr-two");
        var finalized = Succeeded(await FinalizeByHr(approved, "Calibrated"));

        Assert.Equal(("Finalized", "Calibrated"), (finalized.Status, finalized.HrComment));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @id AND Status = 'Finalized' AND FinalizedByUserId = @user AND HrComment = N'Calibrated'", ("@id", setup.Card.Id), ("@user", hrUser)));
        Assert.EndsWith("Manager/Approve/Submitted>Approved;HR/Finalize/Approved>Finalized", await Trail(setup.Card.Id), StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task A_returned_scorecard_is_resubmitted_and_the_figures_of_the_earlier_submission_stay_in_the_temporal_history()
    {
        var setup = await SetupAsync();
        var submitted = await SubmittedAsync(setup);
        ActAsManager(setup);
        var returned = Succeeded(await Return(submitted, "Quality score looks too low"));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.Scorecard WHERE Id = @id AND Status = 'Returned' AND OverallScore IS NULL AND CalculationVersion IS NULL", ("@id", setup.Card.Id)));

        ActAsEmployee(setup);
        var edited = Succeeded(await SaveResults(returned, Actual(setup.Second, 90m, "Re-measured")));
        var again = Succeeded(await Submit(edited));

        Assert.Equal(("Resubmitted", 96m), (again.Status, again.OverallScore));
        Assert.Equal(90m, await Scalar<decimal>("SELECT ActualValue FROM kpi.ScorecardItem WHERE Id = @id", ("@id", setup.Second.Id)));
        Assert.True(await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardItemHistory WHERE Id = @id AND ActualValue = 50 AND AchievementPct = 50 AND WeightedScore = 20", ("@id", setup.Second.Id)) >= 1);
        Assert.True(await Scalar<int>("SELECT COUNT(*) FROM kpi.ScorecardHistory WHERE Id = @id AND OverallScore = 80 AND Status = 'Submitted'", ("@id", setup.Card.Id)) >= 1);
        Assert.EndsWith(
            "Employee/Submit/Draft>Submitted;Manager/Return/Submitted>Returned;Employee/Resubmit/Returned>Resubmitted",
            await Trail(setup.Card.Id),
            StringComparison.Ordinal);
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @id AND Action = 'Return' AND Comment = N'Quality score looks too low' AND OverallScore = 80", ("@id", setup.Card.Id)));
    }

    [IntegrationFact]
    public async Task A_rejected_scorecard_is_final_and_the_database_keeps_it_that_way()
    {
        var setup = await SetupAsync();
        var submitted = await SubmittedAsync(setup);
        ActAsManager(setup);
        var rejected = Succeeded(await Reject(submitted, "Not credible"));

        Assert.Equal("Rejected", rejected.Status);
        Assert.Contains("Rejected", Failed(await Approve(rejected), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("Rejected", Failed(await Return(rejected, "again"), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("Rejected", Failed(await StartReview(rejected), ErrorType.Conflict).Message, StringComparison.Ordinal);
        ActAsEmployee(setup);
        Assert.Contains("Rejected", Failed(await Submit(rejected), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Equal("Rejected", await StatusOf(setup.Card.Id));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @id AND Action = 'Reject' AND Step = 'Manager'", ("@id", setup.Card.Id)));
    }

    [IntegrationFact]
    public async Task Nobody_reviews_their_own_scorecard_not_even_with_every_grant()
    {
        var setup = await SetupAsync();
        var submitted = await SubmittedAsync(setup);

        // Still the employee, who in these tests holds every grant.
        var error = Failed(await Approve(submitted), ErrorType.Conflict);

        Assert.Equal("You cannot review your own scorecard.", error.Message);
        Assert.Equal("Submitted", await StatusOf(setup.Card.Id));
    }

    // ---- Guarding the scorecard ----

    [IntegrationFact]
    public async Task Saving_results_moves_the_scorecard_row_version_so_a_second_save_with_the_old_one_is_a_conflict()
    {
        var setup = await SetupAsync();
        ActAsEmployee(setup);
        var before = await RowVersionOf(setup.Card.Id);

        var saved = Succeeded(await SaveResults(setup.Card, Actual(setup.First, 10m)));

        Assert.NotEqual(before, await RowVersionOf(setup.Card.Id));
        Assert.NotEqual(setup.Card.RowVersion, saved.RowVersion);
        Assert.Equal(RowVersionToken.Stale, Failed(await SaveResults(setup.Card, Actual(setup.First, 20m)), ErrorType.Conflict));
        Assert.Equal(10m, await Scalar<decimal>("SELECT ActualValue FROM kpi.ScorecardItem WHERE Id = @id", ("@id", setup.First.Id)));
    }

    [IntegrationFact]
    public async Task Two_approvals_of_the_same_version_cannot_both_succeed_the_loser_is_a_conflict_and_leaves_one_history_row()
    {
        var setup = await SetupAsync();
        var submitted = await SubmittedAsync(setup);
        ActAsManager(setup);
        BeforeNextSave = async () =>
        {
            // Another session approves between this request's load and its save.
            var other = await RunCommand<ApproveScorecardCommand, ScorecardDetailDto>(new ApproveScorecardCommand(submitted.Id, Requests.Version(submitted.RowVersion), "other"));
            Assert.True(other.IsSuccess, other.IsFailure ? other.Error.Message : null);
        };

        var result = await Approve(submitted, "mine");

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @id AND Action = 'Approve'", ("@id", setup.Card.Id)));
        Assert.Equal(1, await Scalar<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @id AND Action = 'Approve' AND Comment = N'other'", ("@id", setup.Card.Id)));
        Assert.Equal("Finalized", await StatusOf(setup.Card.Id));
    }

    [IntegrationFact]
    public async Task Two_result_saves_of_the_same_version_cannot_both_succeed_even_though_only_items_change()
    {
        var setup = await SetupAsync();
        ActAsEmployee(setup);
        BeforeNextSave = async () =>
        {
            // Another tab of the same employee saves between this request's load and its save.
            var other = await SaveResults(setup.Card, Actual(setup.First, 11m));
            Assert.True(other.IsSuccess, other.IsFailure ? other.Error.Message : null);
        };

        var result = await SaveResults(setup.Card, Actual(setup.First, 22m));

        Assert.Equal(RowVersionToken.Stale, Failed(result, ErrorType.Conflict));
        Assert.Equal(11m, await Scalar<decimal>("SELECT ActualValue FROM kpi.ScorecardItem WHERE Id = @id", ("@id", setup.First.Id)));
    }

    [IntegrationFact]
    public async Task Review_actions_and_results_are_refused_with_a_conflict_once_the_period_is_closed()
    {
        var setup = await SetupAsync();
        var submitted = await SubmittedAsync(setup);
        var completed = Succeeded(await RunCommand<CompletePeriodCommand, EvaluationPeriodDto>(new CompletePeriodCommand(setup.Period.Id, Requests.Version(setup.Period.RowVersion))));
        ActAsManager(setup);
        var started = Succeeded(await StartReview(submitted));
        Succeeded(await RunCommand<ClosePeriodCommand, EvaluationPeriodDto>(new ClosePeriodCommand(setup.Period.Id, Requests.Version(completed.RowVersion))));

        Assert.Contains("Closed", Failed(await Approve(started), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("Closed", Failed(await Return(started, "x"), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Contains("Closed", Failed(await Reject(started, "x"), ErrorType.Conflict).Message, StringComparison.Ordinal);
        Assert.Equal("Under Review", await StatusOf(setup.Card.Id));
    }

    // ---- The review trail ----

    [IntegrationFact]
    public async Task The_database_refuses_a_reject_or_return_step_without_a_reason_and_accepts_one_with_a_reason()
    {
        var setup = await SetupAsync();
        const string insert = "INSERT INTO kpi.ReviewAction (ScorecardId, Step, Action, FromStatus, ToStatus, ActorName, Comment) VALUES (@id, 'Manager', @action, 'Submitted', 'Rejected', @name, @comment)";
        var name = $"{Tag} Actor";

        Assert.Equal(547, await Sql.ErrorNumberOfAsync(insert, ("@id", setup.Card.Id), ("@action", "Reject"), ("@name", name), ("@comment", null)));
        Assert.Equal(547, await Sql.ErrorNumberOfAsync(insert, ("@id", setup.Card.Id), ("@action", "Return"), ("@name", name), ("@comment", "   ")));
        Assert.Equal(0, await Sql.ErrorNumberOfAsync(insert, ("@id", setup.Card.Id), ("@action", "Reject"), ("@name", name), ("@comment", "A reason")));
    }

    [IntegrationFact]
    public async Task Returning_without_a_reason_is_a_validation_error_before_the_database_is_reached()
    {
        var setup = await SetupAsync();
        var submitted = await SubmittedAsync(setup);
        ActAsManager(setup);

        Assert.Equal("A reason is required.", FailedOnField(await Return(submitted, "  "), "comment"));
        Assert.Equal("A reason is required.", FailedOnField(await Reject(submitted, null), "comment"));
        Assert.Equal("Submitted", await StatusOf(setup.Card.Id));
    }

    [IntegrationFact]
    public async Task Results_that_break_a_rule_name_the_item_and_a_missing_result_blocks_the_submission()
    {
        var setup = await SetupAsync();
        ActAsEmployee(setup);

        var tooMany = Failed(await SaveResults(setup.Card, new ResultItemRequest(setup.First.Id, 1.23456m, null)), ErrorType.Validation);
        var foreign = Failed(await SaveResults(setup.Card, new ResultItemRequest(2_000_000_000, 1m, null)), ErrorType.Validation);
        var missing = Failed(await Submit(setup.Card), ErrorType.Validation);

        Assert.Contains("items[0].actualValue", tooMany.FieldErrors!.Keys);
        Assert.Contains("items[0].id", foreign.FieldErrors!.Keys);
        Assert.Equal(["items[0].actualValue", "items[1].actualValue"], missing.FieldErrors!.Keys.Order().ToArray());
        Assert.Equal("Draft", await StatusOf(setup.Card.Id));
    }

    // ---- The queries ----

    [IntegrationFact]
    public async Task My_scorecards_and_the_review_queue_run_against_the_database()
    {
        var setup = await SetupAsync(hrReview: true);
        var submitted = await SubmittedAsync(setup);

        var mine = Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IMyScorecardsHandler>()
            .HandleAsync(new PageRequest<ScorecardFilter>(0, 10, null, null, new ScorecardFilter()), default)));
        Assert.Equal([setup.Card.Id], mine.Items.Select(card => card.Id).ToArray());

        ActAsManager(setup);
        var forManager = await PendingAsync();
        Assert.Equal([setup.Card.Id], forManager.Items.Select(card => card.Id).ToArray());
        var approved = Succeeded(await Approve(submitted));
        Assert.Equal("Approved", approved.Status);

        // This caller holds every grant, so it is the HR tier: Approved scorecards are waiting for it, except the ones it approved itself.
        Assert.Empty((await PendingAsync()).Items);
        await ActAsOtherHrUserAsync("hr-queue");
        Assert.Equal([setup.Card.Id], (await PendingAsync()).Items.Select(card => card.Id).ToArray());
        ActAsEmployee(setup);
        Assert.Empty((await PendingAsync()).Items);
    }

    private async Task<PagedResult<ScorecardDto>> PendingAsync() =>
        Succeeded(await InScopeAsync(sp => sp.GetRequiredService<IPendingReviewsHandler>()
            .HandleAsync(new PageRequest<ScorecardFilter>(0, 10, null, null, new ScorecardFilter()), default)));
}
