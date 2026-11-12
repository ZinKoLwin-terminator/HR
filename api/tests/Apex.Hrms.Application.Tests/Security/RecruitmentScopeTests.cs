using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Payroll.Payslips;
using Apex.Hrms.Application.Payroll.Runs;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Recruitment.Views;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Security;

/// <summary>
/// "HR all, managers own" for recruitment: a caller with recruitment:Edit sees every opening, candidate, application and
/// interview; anyone else with recruitment:View sees only the openings they are hiring manager of and what hangs on them; no
/// linked employee and no Edit sees nothing. Records outside the scope are 404. The cast: two openings with a candidate,
/// an application and an interview each; Maya (the seeded manager) hires for the first. Today is Monday 2026-11-09.
/// </summary>
public sealed class RecruitmentScopeTests : IDisposable
{
    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private sealed record Cast(
        RecruitmentSeed Seed,
        JobOpeningDto MayasJob,
        JobOpeningDto OtherJob,
        CandidateDto MayasCandidate,
        CandidateDto OtherCandidate,
        JobApplicationDto MayasApplication,
        JobApplicationDto OtherApplication,
        InterviewDto MayasInterview,
        InterviewDto OtherInterview);

    private async Task<Cast> SeedAsync()
    {
        var seed = await host.SeedRecruitmentAsync();
        var mayasJob = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, "Maya's opening"));
        // The interviewer manages the second opening.
        var otherJob = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed, "Other opening") with { HiringManagerId = seed.Interviewer.Id });
        var mayasCandidate = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("candidate.one@example.test"));
        var otherCandidate = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("candidate.two@example.test", "Bea"));
        var mayasApplication = await host.MoveThroughAsync(await host.ApplyAsync(mayasJob.Id, mayasCandidate.Id), "Screening");
        var otherApplication = await host.MoveThroughAsync(await host.ApplyAsync(otherJob.Id, otherCandidate.Id), "Screening");
        var mayasInterview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(mayasApplication.Id, seed.Interviewer.Id, RecruitmentFixtures.Today, 9));
        var otherInterview = await host.CreateInterviewAsync(RecruitmentFixtures.NewInterview(otherApplication.Id, seed.Interviewer.Id, RecruitmentFixtures.Today, 13));
        return new Cast(seed, mayasJob, otherJob, mayasCandidate, otherCandidate, mayasApplication, otherApplication, mayasInterview, otherInterview);
    }

    /// <summary>What the host does for a caller who holds recruitment:View only: the account's employee and the managed records.</summary>
    private void SignInAsManager(Cast cast, params string[] grants)
    {
        host.User.EmployeeId = cast.Seed.Manager.Id;
        host.Permissions.Only(grants.Length == 0 ? ["recruitment:View"] : grants);
        host.Recruitment.Set(new ManagedRecruitment(
            [cast.MayasJob.Id],
            [cast.MayasApplication.Id],
            [cast.MayasCandidate.Id]));
    }

    // ---- HR tier ----

    [Fact]
    public async Task A_caller_with_recruitment_edit_sees_everything()
    {
        var cast = await SeedAsync();
        host.User.EmployeeId = null;
        host.Permissions.Only("recruitment:View", "recruitment:Edit");

        Assert.Equal(2, ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobOpeningDto>>().HandleAsync(default)).Count);
        Assert.Equal(2, ResultAssert.Succeeded(await host.Get<IGetAllHandler<CandidateDto>>().HandleAsync(default)).Count);
        Assert.Equal(2, ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobApplicationDto>>().HandleAsync(default)).Count);
        Assert.Equal(2, ResultAssert.Succeeded(await host.Get<IGetAllHandler<InterviewDto>>().HandleAsync(default)).Count);
        ResultAssert.Succeeded(await host.GetJob(cast.OtherJob.Id));
    }

    // ---- Manager tier ----

    [Fact]
    public async Task A_hiring_manager_sees_only_the_openings_they_manage_and_what_hangs_on_them()
    {
        var cast = await SeedAsync();
        SignInAsManager(cast);

        Assert.Equal([cast.MayasJob.Id], ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobOpeningDto>>().HandleAsync(default)).Select(job => job.Id));
        Assert.Equal([cast.MayasCandidate.Id], ResultAssert.Succeeded(await host.Get<IGetAllHandler<CandidateDto>>().HandleAsync(default)).Select(c => c.Id));
        Assert.Equal([cast.MayasApplication.Id], ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobApplicationDto>>().HandleAsync(default)).Select(a => a.Id));
        Assert.Equal([cast.MayasInterview.Id], ResultAssert.Succeeded(await host.Get<IGetAllHandler<InterviewDto>>().HandleAsync(default)).Select(i => i.Id));
    }

    [Fact]
    public async Task Queries_are_scoped_too()
    {
        var cast = await SeedAsync();
        SignInAsManager(cast);

        var jobs = ResultAssert.Succeeded(await host.Get<IQueryHandler<JobOpeningDto, JobOpeningFilter>>()
            .HandleAsync(new PageRequest<JobOpeningFilter>(0, 10, null, null, new JobOpeningFilter()), default));
        var candidates = ResultAssert.Succeeded(await host.Get<IQueryHandler<CandidateDto, CandidateFilter>>()
            .HandleAsync(new PageRequest<CandidateFilter>(0, 10, null, null, new CandidateFilter()), default));
        var applications = ResultAssert.Succeeded(await host.Get<IQueryHandler<JobApplicationDto, JobApplicationFilter>>()
            .HandleAsync(new PageRequest<JobApplicationFilter>(0, 10, null, null, new JobApplicationFilter()), default));
        var interviews = ResultAssert.Succeeded(await host.Get<IQueryHandler<InterviewDto, InterviewFilter>>()
            .HandleAsync(new PageRequest<InterviewFilter>(0, 10, null, null, new InterviewFilter()), default));

        Assert.Equal(1, jobs.Total);
        Assert.Equal(1, candidates.Total);
        Assert.Equal(1, applications.Total);
        Assert.Equal(1, interviews.Total);
    }

    [Fact]
    public async Task Records_outside_the_scope_are_not_found_on_read_update_delete_and_every_command()
    {
        var cast = await SeedAsync();
        SignInAsManager(cast);

        ResultAssert.Failed(await host.GetJob(cast.OtherJob.Id), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IGetByIdHandler<CandidateDto>>().HandleAsync(cast.OtherCandidate.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await host.GetJobApplication(cast.OtherApplication.Id), ErrorType.NotFound);
        ResultAssert.Failed(await host.GetInterview(cast.OtherInterview.Id), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IUpdateHandler<JobOpeningUpdateRequest, JobOpeningDto>>()
            .HandleAsync(cast.OtherJob.Id, RecruitmentFixtures.JobUpdateOf(cast.OtherJob, "Renamed"), default), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IUpdateHandler<CandidateUpdateRequest, CandidateDto>>()
            .HandleAsync(cast.OtherCandidate.Id, RecruitmentFixtures.CandidateUpdateOf(cast.OtherCandidate, years: 9m), default), ErrorType.NotFound);
        ResultAssert.Failed(await host.MoveStage(cast.OtherApplication, "Interview"), ErrorType.NotFound);
        ResultAssert.Failed(await host.Outcome(cast.OtherInterview), ErrorType.NotFound);
        ResultAssert.Failed(await host.CancelInterview(cast.OtherInterview), ErrorType.NotFound);
        ResultAssert.Failed(await host.Command<CloseJobOpeningCommand, JobOpeningDto>(
            new CloseJobOpeningCommand(cast.OtherJob.Id, Requests.Version(cast.OtherJob.RowVersion))), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IDeleteHandler<InterviewDto>>()
            .HandleAsync(cast.OtherInterview.Id, Requests.Version(cast.OtherInterview.RowVersion), default), ErrorType.NotFound);
    }

    [Fact]
    public async Task The_pipeline_and_counts_show_only_the_managed_openings()
    {
        var cast = await SeedAsync();
        SignInAsManager(cast);

        var pipeline = ResultAssert.Succeeded(await host.Get<IRecruitmentPipelineHandler>().HandleAsync(null, default));
        var counts = ResultAssert.Succeeded(await host.Get<IRecruitmentCountsHandler>().HandleAsync(default));

        Assert.Equal([cast.MayasApplication.Id], pipeline.Columns.SelectMany(column => column.Cards).Select(card => card.ApplicationId));
        Assert.Equal([cast.MayasJob.Id], counts.JobOpenings.Select(job => job.JobOpeningId));
        Assert.Equal([cast.MayasCandidate.Id], counts.Candidates.Select(candidate => candidate.CandidateId));
        ResultAssert.Failed(await host.Get<IRecruitmentPipelineHandler>().HandleAsync(cast.OtherJob.Id, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_manager_cannot_apply_to_or_schedule_for_records_they_do_not_manage()
    {
        var cast = await SeedAsync();
        var spare = await host.CreateCandidateAsync(RecruitmentFixtures.NewCandidate("spare@example.test", "Cy"));
        SignInAsManager(cast, "recruitment:View", "recruitment:Create");

        ResultAssert.FailedOnField(await host.Apply(cast.OtherJob.Id, spare.Id), "jobOpeningId");
        ResultAssert.FailedOnField(
            await host.CreateInterview(RecruitmentFixtures.NewInterview(cast.OtherApplication.Id, cast.Seed.Interviewer.Id, RecruitmentFixtures.Today, 15)),
            "applicationId");
        ResultAssert.Succeeded(await host.Apply(cast.MayasJob.Id, spare.Id));
    }

    [Fact]
    public async Task A_manager_with_no_openings_sees_nothing()
    {
        var cast = await SeedAsync();
        host.User.EmployeeId = 987654;
        host.Permissions.Only("recruitment:View");

        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobOpeningDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<CandidateDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobApplicationDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<InterviewDto>>().HandleAsync(default)));
    }

    [Fact]
    public async Task A_caller_with_no_linked_employee_and_no_edit_sees_nothing()
    {
        await SeedAsync();
        host.User.EmployeeId = null;
        host.Permissions.Only("recruitment:View");
        host.Recruitment.Set(new ManagedRecruitment([1, 2], [1, 2], [1, 2]));

        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobOpeningDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<CandidateDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobApplicationDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<InterviewDto>>().HandleAsync(default)));
    }

    [Fact]
    public async Task Until_the_host_has_read_what_a_manager_manages_only_their_own_openings_are_visible_and_nothing_hangs_on_them()
    {
        var cast = await SeedAsync();
        host.User.EmployeeId = cast.Seed.Manager.Id;
        host.Permissions.Only("recruitment:View");

        Assert.Single(ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobOpeningDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<JobApplicationDto>>().HandleAsync(default)));
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IGetAllHandler<CandidateDto>>().HandleAsync(default)));
    }

    // ---- Employee lookup ----

    [Fact]
    public async Task The_lookup_returns_only_the_picker_fields_of_current_employees()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(seed.Org, email: "left.person@example.test"));
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(former.Id, Requests.Version(former.RowVersion), new DateOnly(2026, 10, 31), "Resigned")));

        var page = ResultAssert.Succeeded(await host.Get<IEmployeeLookupHandler>().HandleAsync(null, 0, 50, default));

        Assert.Equal([seed.Manager.Id, seed.Interviewer.Id], page.Items.Select(item => item.Id).Order());
        Assert.DoesNotContain(page.Items, item => item.Id == former.Id);
        var first = page.Items[0];
        Assert.Equal("Mia Hartley", first.DisplayName);
        Assert.False(string.IsNullOrEmpty(first.JobTitle));
        Assert.Equal(seed.Org.Department.Name, first.DepartmentName);
        Assert.Equal(
            ["DepartmentName", "DisplayName", "EmployeeCode", "Id", "JobTitle"],
            typeof(EmployeeLookupDto).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_lookup_searches_by_code_or_name_and_pages()
    {
        var seed = await host.SeedRecruitmentAsync();

        var byCode = ResultAssert.Succeeded(await host.Get<IEmployeeLookupHandler>().HandleAsync(seed.Manager.EmployeeCode, 0, 10, default));
        var none = ResultAssert.Succeeded(await host.Get<IEmployeeLookupHandler>().HandleAsync("zzz-nobody", 0, 10, default));
        var paged = ResultAssert.Succeeded(await host.Get<IEmployeeLookupHandler>().HandleAsync(null, 0, 1, default));

        Assert.Equal([seed.Manager.Id], byCode.Items.Select(item => item.Id));
        Assert.Empty(none.Items);
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.Total);
        ResultAssert.Failed(await host.Get<IEmployeeLookupHandler>().HandleAsync(new string('a', 101), 0, 10, default), ErrorType.Validation);
    }

    // ---- My payslips ----

    private async Task<(PayrollSeed Seed, PayrollRunDto Run)> ProcessedRunAsync()
    {
        var seed = await host.SeedPayrollAsync();
        var run = await host.ProcessedAsync(await host.CreateRunAsync());
        return (seed, run);
    }

    [Fact]
    public async Task My_payslips_never_show_a_draft_or_pending_run()
    {
        var (seed, run) = await ProcessedRunAsync();
        host.User.EmployeeId = seed.Employee.Id;
        host.Permissions.Only("dashboard:View");

        Assert.Equal("Pending Approval", run.Status);
        Assert.Empty(ResultAssert.Succeeded(await host.Get<IMyPayslipsHandler>().QueryAsync(0, 10, default)).Items);
        var payslipId = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: run.Id))).Items.Single().Id;
        ResultAssert.Failed(await host.Get<IMyPayslipsHandler>().GetAsync(payslipId, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task My_payslips_show_only_my_own_of_an_approved_or_paid_run_with_the_masked_account()
    {
        var (seed, run) = await ProcessedRunAsync();
        var colleague = await host.CreatePayrollEmployeeAsync(seed.Org, "bea.test@example.test", accountNumber: "9999990000");
        ResultAssert.Succeeded(await host.CreateAssignment(PayrollFixtures.NewAssignment(colleague.Id, seed.Structure.Id)));
        var draft = ResultAssert.Succeeded(await host.ReturnToDraft(run));
        var again = await host.ProcessedAsync(draft);
        host.User.UserId = 2;
        var approved = ResultAssert.Succeeded(await host.Approve(again));
        var all = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: approved.Id))).Items;
        Assert.Equal(2, all.Count);

        host.User.EmployeeId = seed.Employee.Id;
        host.Permissions.Only("dashboard:View");
        var mine = ResultAssert.Succeeded(await host.Get<IMyPayslipsHandler>().QueryAsync(0, 10, default));
        var own = Assert.Single(mine.Items);
        Assert.Equal(seed.Employee.Id, own.EmployeeId);
        Assert.EndsWith("7890", own.AccountMasked, StringComparison.Ordinal);
        Assert.NotEqual("1234567890", own.AccountMasked);
        Assert.DoesNotContain("1234567890", System.Text.Json.JsonSerializer.Serialize(own), StringComparison.Ordinal);
        Assert.Equal(own.Id, ResultAssert.Succeeded(await host.Get<IMyPayslipsHandler>().GetAsync(own.Id, default)).Id);
        var colleaguesPayslip = all.Single(payslip => payslip.EmployeeId == colleague.Id);
        ResultAssert.Failed(await host.Get<IMyPayslipsHandler>().GetAsync(colleaguesPayslip.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await host.Get<IMyPayslipsHandler>().GetAsync(9999, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task A_paid_run_is_still_shown_and_a_caller_without_an_employee_gets_an_empty_page()
    {
        var (seed, run) = await ProcessedRunAsync();
        host.User.UserId = 2;
        var paid = ResultAssert.Succeeded(await host.MarkPaid(ResultAssert.Succeeded(await host.Approve(run))));
        Assert.Equal("Paid", paid.Status);

        host.User.EmployeeId = seed.Employee.Id;
        Assert.Single(ResultAssert.Succeeded(await host.Get<IMyPayslipsHandler>().QueryAsync(0, 10, default)).Items);

        host.User.EmployeeId = null;
        var page = ResultAssert.Succeeded(await host.Get<IMyPayslipsHandler>().QueryAsync(0, 10, default));
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
        var payslipId = ResultAssert.Succeeded(await host.Payslips(new PayslipFilter(RunId: run.Id))).Items.Single().Id;
        ResultAssert.Failed(await host.Get<IMyPayslipsHandler>().GetAsync(payslipId, default), ErrorType.NotFound);
    }
}
