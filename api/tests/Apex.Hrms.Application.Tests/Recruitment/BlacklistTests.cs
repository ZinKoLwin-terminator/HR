using System.Text.Json;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Employees.Blacklist;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.Security;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Recruitment;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Tests.Recruitment;

/// <summary>
/// The do-not-rehire list and its recruitment check: who may be listed, one active entry, lifting, how a candidate is matched,
/// what recruiters learn (a boolean only), and the hire gate with the administrator's override. Today is Monday 2026-11-09.
/// Everyone in these tests is fictional.
/// </summary>
public sealed class BlacklistTests : IDisposable
{
    private const string ListedEmail = "listed.person@example.test";

    private readonly TestHost host = new();

    public void Dispose() => host.Dispose();

    private IBlacklistHandler Blacklist => host.Get<IBlacklistHandler>();

    private async Task<EmployeeDto> FormerEmployeeAsync(RecruitmentSeed seed, string email = ListedEmail)
    {
        var employee = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(seed.Org, email: email));
        return ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(employee.Id, Requests.Version(employee.RowVersion), new DateOnly(2026, 10, 31), "Resigned")));
    }

    private async Task<BlacklistEntryDto> ListAsync(EmployeeDto former, string reason = "Left during an investigation.") =>
        ResultAssert.Succeeded(await Blacklist.AddAsync(new BlacklistAddRequest(former.Id, reason), default));

    private async Task SignInAsync(string roleName)
    {
        var role = ResultAssert.Succeeded(AppRole.Create(new RoleDetails(roleName, "Fictional role"), RecordStatus.Active));
        host.Repository<AppRole>().Add(role);
        Assert.True((await host.UnitOfWork.SaveChangesAsync(default)).IsSuccess);
        var account = AppAccount.Create(null, "Test Person", $"{roleName.Replace(' ', '.').ToLowerInvariant()}@example.test", role.Id, $"seed-{roleName}", AccountStatus.Active);
        host.Repository<AppAccount>().Add(account);
        Assert.True((await host.UnitOfWork.SaveChangesAsync(default)).IsSuccess);
        host.User.UserId = account.Id;
    }

    // ---- The list ----

    [Fact]
    public async Task Only_a_former_employee_can_be_put_on_the_list()
    {
        var seed = await host.SeedRecruitmentAsync();

        var error = ResultAssert.Failed(
            await Blacklist.AddAsync(new BlacklistAddRequest(seed.Manager.Id, "Reason"), default),
            ErrorType.Conflict);

        Assert.Equal("Only a former employee can be put on the do-not-rehire list.", error.Message);
    }

    [Fact]
    public async Task Adding_a_former_employee_keeps_the_reason_and_logs_the_code_without_it()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);

        var entry = await ListAsync(former, "Confidential reason text.");

        Assert.Equal(former.Id, entry.EmployeeId);
        Assert.Equal(former.EmployeeCode, entry.EmployeeCode);
        Assert.Equal("Mia Hartley", entry.EmployeeName);
        Assert.Equal("Confidential reason text.", entry.Reason);
        Assert.Equal(RecruitmentFixtures.Today, entry.BlacklistedOn);
        Assert.Equal("Test User", entry.BlacklistedBy);
        Assert.Null(entry.LiftedOn);
        Assert.Equal($"Employee {former.EmployeeCode} added to the do-not-rehire list", host.ActivityLog.LastMessage);
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("Confidential", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_employee_has_one_active_entry_at_a_time()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);
        await ListAsync(former);

        var error = ResultAssert.Failed(await Blacklist.AddAsync(new BlacklistAddRequest(former.Id, "Again"), default), ErrorType.Conflict);

        Assert.Equal("This employee is already on the do-not-rehire list.", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task The_reason_is_required(string? reason)
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);

        ResultAssert.FailedOnField(await Blacklist.AddAsync(new BlacklistAddRequest(former.Id, reason), default), "reason");
    }

    [Fact]
    public async Task The_reason_is_at_most_five_hundred_characters_and_the_employee_must_exist()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);

        ResultAssert.FailedOnField(await Blacklist.AddAsync(new BlacklistAddRequest(former.Id, new string('x', 501)), default), "reason");
        ResultAssert.FailedOnField(await Blacklist.AddAsync(new BlacklistAddRequest(9999, "Reason"), default), "employeeId");
        Assert.NotNull(ResultAssert.Succeeded(await Blacklist.AddAsync(new BlacklistAddRequest(former.Id, new string('x', 500)), default)));
    }

    [Fact]
    public async Task Lifting_needs_a_reason_keeps_the_entry_as_history_and_allows_a_new_entry()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);
        var entry = await ListAsync(former);

        ResultAssert.FailedOnField(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(Requests.Version(entry.RowVersion), " "), default), "reason");

        host.User.DisplayName = "Hana Administrator";
        var lifted = ResultAssert.Succeeded(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(Requests.Version(entry.RowVersion), "Reviewed and cleared."), default));

        Assert.Equal(RecruitmentFixtures.Today, lifted.LiftedOn);
        Assert.Equal("Hana Administrator", lifted.LiftedBy);
        Assert.Equal("Reviewed and cleared.", lifted.LiftReason);
        Assert.Equal($"Employee {former.EmployeeCode} removed from the do-not-rehire list", host.ActivityLog.LastMessage);
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("cleared", StringComparison.Ordinal));
        ResultAssert.Failed(await Blacklist.GetActiveOfEmployeeAsync(former.Id, default), ErrorType.NotFound);
        Assert.Equal(1, ResultAssert.Succeeded(await Blacklist.QueryAsync(new PageRequest<BlacklistFilter>(0, 10, null, null, new BlacklistFilter(Active: false)), default)).Total);

        var second = await ListAsync(former, "A second entry.");
        Assert.NotEqual(entry.Id, second.Id);
    }

    [Fact]
    public async Task A_lifted_entry_cannot_be_lifted_again_and_a_stale_version_is_a_conflict()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);
        var entry = await ListAsync(former);
        var lifted = ResultAssert.Succeeded(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(Requests.Version(entry.RowVersion), "Cleared."), default));

        ResultAssert.Failed(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(Requests.Version(lifted.RowVersion), "Again."), default), ErrorType.Conflict);
        ResultAssert.Failed(await Blacklist.LiftAsync(9999, new BlacklistLiftRequest(Requests.Version(lifted.RowVersion), "x"), default), ErrorType.NotFound);
        ResultAssert.Failed(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(null, "x"), default), ErrorType.Validation);
    }

    [Fact]
    public async Task The_list_filters_by_active_and_searches_by_employee_code_or_name()
    {
        var seed = await host.SeedRecruitmentAsync();
        var first = await FormerEmployeeAsync(seed, "first.person@example.test");
        var second = await FormerEmployeeAsync(seed, "second.person@example.test");
        await ListAsync(first);
        var secondEntry = await ListAsync(second);
        ResultAssert.Succeeded(await Blacklist.LiftAsync(secondEntry.Id, new BlacklistLiftRequest(Requests.Version(secondEntry.RowVersion), "Cleared."), default));

        var active = ResultAssert.Succeeded(await Blacklist.QueryAsync(new PageRequest<BlacklistFilter>(0, 10, null, null, new BlacklistFilter(Active: true)), default));
        var all = ResultAssert.Succeeded(await Blacklist.QueryAsync(new PageRequest<BlacklistFilter>(0, 10, null, null, new BlacklistFilter()), default));
        var byCode = ResultAssert.Succeeded(await Blacklist.QueryAsync(new PageRequest<BlacklistFilter>(0, 10, null, second.EmployeeCode, new BlacklistFilter()), default));

        Assert.Equal([first.Id], active.Items.Select(item => item.EmployeeId));
        Assert.Equal(2, all.Total);
        Assert.Equal([second.Id], byCode.Items.Select(item => item.EmployeeId));
        ResultAssert.Failed(await Blacklist.QueryAsync(new PageRequest<BlacklistFilter>(0, 10, new SortState("reason", "asc"), null, new BlacklistFilter()), default), ErrorType.Validation);
    }

    [Fact]
    public async Task The_active_entry_of_an_employee_is_read_and_an_employee_who_is_not_listed_is_not_found()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);
        var entry = await ListAsync(former);

        Assert.Equal(entry.Id, ResultAssert.Succeeded(await Blacklist.GetActiveOfEmployeeAsync(former.Id, default)).Id);
        Assert.Equal(entry.Id, ResultAssert.Succeeded(await Blacklist.GetAsync(entry.Id, default)).Id);
        ResultAssert.Failed(await Blacklist.GetActiveOfEmployeeAsync(seed.Manager.Id, default), ErrorType.NotFound);
        ResultAssert.Failed(await Blacklist.GetActiveOfEmployeeAsync(9999, default), ErrorType.NotFound);
        ResultAssert.Failed(await Blacklist.GetAsync(9999, default), ErrorType.NotFound);
    }

    [Fact]
    public async Task An_employee_with_blacklist_history_cannot_be_deleted()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);
        var entry = await ListAsync(former);
        ResultAssert.Succeeded(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(Requests.Version(entry.RowVersion), "Cleared."), default));
        var current = ResultAssert.Succeeded(await host.GetEmployee(former.Id));

        var error = ResultAssert.Failed(
            await host.Get<IDeleteHandler<EmployeeDto>>().HandleAsync(former.Id, Requests.Version(current.RowVersion), default),
            ErrorType.Conflict);

        Assert.Contains("do-not-rehire", error.Message, StringComparison.Ordinal);
    }

    // ---- Matching ----

    private async Task<CandidateDto> CandidateAsync(CandidateCreateRequest request) => await host.CreateCandidateAsync(request);

    [Fact]
    public async Task A_candidate_matches_by_work_email_ignoring_case_and_spaces()
    {
        var seed = await host.SeedRecruitmentAsync();
        await ListAsync(await FormerEmployeeAsync(seed));

        var candidate = await CandidateAsync(RecruitmentFixtures.NewCandidate("  Listed.Person@Example.Test"));

        Assert.True(candidate.BlacklistMatch);
        Assert.True(ResultAssert.Succeeded(await host.Get<IGetByIdHandler<CandidateDto>>().HandleAsync(candidate.Id, default)).BlacklistMatch);
    }

    [Fact]
    public async Task A_candidate_matches_by_personal_email()
    {
        var seed = await host.SeedRecruitmentAsync();
        await ListAsync(await FormerEmployeeAsync(seed));

        Assert.True((await CandidateAsync(RecruitmentFixtures.NewCandidate("MIA.PERSONAL@example.test"))).BlacklistMatch);
    }

    [Theory]
    [InlineData("95-9-555-0101")]
    [InlineData("+95 (9) 555 0101")]
    public async Task A_candidate_matches_by_phone_using_the_digits_only(string phone)
    {
        var seed = await host.SeedRecruitmentAsync();
        await ListAsync(await FormerEmployeeAsync(seed));

        var candidate = await CandidateAsync(RecruitmentFixtures.NewCandidate("different@example.test") with { Phone = phone });

        Assert.True(candidate.BlacklistMatch);
    }

    [Fact]
    public async Task A_candidate_matches_by_the_alternate_phone()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(seed.Org, email: "alt.person@example.test") with { Phone = "+95 9 777 0001", AlternatePhone = "+95 9 777 0002" });
        ResultAssert.Succeeded(await host.Command<ExitEmployeeCommand, EmployeeDto>(
            new ExitEmployeeCommand(former.Id, Requests.Version(former.RowVersion), new DateOnly(2026, 10, 31), "Terminated")));
        await ListAsync(former);

        Assert.True((await CandidateAsync(RecruitmentFixtures.NewCandidate("one@example.test") with { Phone = "959777 0002" })).BlacklistMatch);
    }

    [Fact]
    public async Task A_candidate_matches_by_national_id_only_when_one_is_given()
    {
        var seed = await host.SeedRecruitmentAsync();
        await ListAsync(await FormerEmployeeAsync(seed));

        var withId = await CandidateAsync(RecruitmentFixtures.NewCandidate("with.id@example.test") with { NationalId = " nrc-test-000001 " });
        var withoutId = await CandidateAsync(RecruitmentFixtures.NewCandidate("without.id@example.test"));
        var otherId = await CandidateAsync(RecruitmentFixtures.NewCandidate("other.id@example.test") with { NationalId = "NRC-TEST-999999" });

        Assert.True(withId.BlacklistMatch);
        Assert.False(withoutId.BlacklistMatch);
        Assert.False(otherId.BlacklistMatch);
    }

    [Fact]
    public async Task An_unrelated_candidate_does_not_match_and_a_lifted_entry_no_longer_matches()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);
        var entry = await ListAsync(former);
        var unrelated = await CandidateAsync(RecruitmentFixtures.NewCandidate("unrelated@example.test"));
        var listed = await CandidateAsync(RecruitmentFixtures.NewCandidate(ListedEmail));
        Assert.False(unrelated.BlacklistMatch);
        Assert.True(listed.BlacklistMatch);

        ResultAssert.Succeeded(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(Requests.Version(entry.RowVersion), "Cleared."), default));

        Assert.False(ResultAssert.Succeeded(await host.Get<IGetByIdHandler<CandidateDto>>().HandleAsync(listed.Id, default)).BlacklistMatch);
    }

    [Fact]
    public async Task A_current_employee_who_is_not_listed_never_matches()
    {
        var seed = await host.SeedRecruitmentAsync();

        Assert.False((await CandidateAsync(RecruitmentFixtures.NewCandidate("manager@example.test"))).BlacklistMatch);
    }

    [Fact]
    public async Task The_flag_is_set_on_candidate_lists_pages_updates_and_applications()
    {
        var seed = await host.SeedRecruitmentAsync();
        await ListAsync(await FormerEmployeeAsync(seed));
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var listed = await CandidateAsync(RecruitmentFixtures.NewCandidate(ListedEmail));
        var other = await CandidateAsync(RecruitmentFixtures.NewCandidate("other@example.test"));
        var application = await host.ApplyAsync(job.Id, listed.Id);
        await host.ApplyAsync(job.Id, other.Id);

        var all = ResultAssert.Succeeded(await host.Get<IGetAllHandler<CandidateDto>>().HandleAsync(default));
        var page = ResultAssert.Succeeded(await host.Get<IQueryHandler<CandidateDto, CandidateFilter>>()
            .HandleAsync(new PageRequest<CandidateFilter>(0, 10, null, null, new CandidateFilter()), default));
        var updated = ResultAssert.Succeeded(await host.Get<IUpdateHandler<CandidateUpdateRequest, CandidateDto>>()
            .HandleAsync(listed.Id, RecruitmentFixtures.CandidateUpdateOf(listed, years: 5m), default));
        var applications = ResultAssert.Succeeded(await host.Get<IQueryHandler<JobApplicationDto, JobApplicationFilter>>()
            .HandleAsync(new PageRequest<JobApplicationFilter>(0, 10, null, null, new JobApplicationFilter()), default));

        Assert.Equal([listed.Id], all.Where(c => c.BlacklistMatch).Select(c => c.Id));
        Assert.Equal([listed.Id], page.Items.Where(c => c.BlacklistMatch).Select(c => c.Id));
        Assert.True(updated.BlacklistMatch);
        Assert.Equal([application.Id], applications.Items.Where(a => a.BlacklistMatch).Select(a => a.Id));
        Assert.True(ResultAssert.Succeeded(await host.GetJobApplication(application.Id)).BlacklistMatch);
    }

    [Fact]
    public async Task A_national_id_given_on_create_is_kept_when_an_update_omits_it_and_cleared_when_it_is_blank()
    {
        var seed = await host.SeedRecruitmentAsync();
        await ListAsync(await FormerEmployeeAsync(seed));
        var candidate = await CandidateAsync(RecruitmentFixtures.NewCandidate("keeps.id@example.test") with { NationalId = "NRC-TEST-000001" });
        var update = host.Get<IUpdateHandler<CandidateUpdateRequest, CandidateDto>>();

        var omitted = ResultAssert.Succeeded(await update.HandleAsync(candidate.Id, RecruitmentFixtures.CandidateUpdateOf(candidate, years: 6m), default));
        var cleared = ResultAssert.Succeeded(await update.HandleAsync(candidate.Id, RecruitmentFixtures.CandidateUpdateOf(omitted, years: 7m) with { NationalId = "  " }, default));

        Assert.True(omitted.BlacklistMatch);
        Assert.False(cleared.BlacklistMatch);
        ResultAssert.FailedOnField(
            await update.HandleAsync(candidate.Id, RecruitmentFixtures.CandidateUpdateOf(cleared) with { NationalId = new string('9', 41) }, default),
            "nationalId");
    }

    [Fact]
    public async Task Recruiters_learn_a_boolean_and_never_the_employee_or_the_reason()
    {
        var seed = await host.SeedRecruitmentAsync();
        var former = await FormerEmployeeAsync(seed);
        await ListAsync(former, "Secret reason text.");
        var job = await host.OpenJobAsync(RecruitmentFixtures.NewJob(seed));
        var candidate = await CandidateAsync(RecruitmentFixtures.NewCandidate(ListedEmail) with { NationalId = "NRC-TEST-000001" });
        var application = await host.ApplyAsync(job.Id, candidate.Id);

        var json = JsonSerializer.Serialize(new object[]
        {
            ResultAssert.Succeeded(await host.Get<IGetByIdHandler<CandidateDto>>().HandleAsync(candidate.Id, default)),
            ResultAssert.Succeeded(await host.GetJobApplication(application.Id)),
        });

        Assert.Contains("\"BlacklistMatch\":true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain(former.EmployeeCode, json, StringComparison.Ordinal);
        Assert.DoesNotContain("NRC-TEST", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Mia", json, StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(CandidateDto).GetProperties(), property => property.Name.Contains("Employee", StringComparison.Ordinal) || property.Name == "NationalId");
        Assert.DoesNotContain(typeof(JobApplicationDto).GetProperties(), property => property.Name.Contains("Override", StringComparison.Ordinal));
    }

    // ---- The hire gate ----

    private async Task<(RecruitmentSeed Seed, JobApplicationDto Application)> ReadyListedCandidateAsync()
    {
        var seed = await host.SeedRecruitmentAsync();
        await ListAsync(await FormerEmployeeAsync(seed), "Secret reason text.");
        var (_, _, application, _) = await host.ReadyToSelectAsync(seed, email: ListedEmail);
        return (seed, application);
    }

    [Fact]
    public async Task Selecting_a_matching_candidate_is_refused_without_an_override()
    {
        var (_, application) = await ReadyListedCandidateAsync();

        var error = ResultAssert.Failed(await host.MoveStage(application, "Selected"), ErrorType.Conflict);

        Assert.Equal("This candidate matches the do-not-rehire list. An HR Administrator must approve the hire with a reason.", error.Message);
        Assert.Equal("Interview", ResultAssert.Succeeded(await host.GetJobApplication(application.Id)).Stage);
    }

    [Fact]
    public async Task A_non_administrator_who_sends_an_override_is_forbidden()
    {
        var (_, application) = await ReadyListedCandidateAsync();
        await SignInAsync("Recruiter");

        var result = await host.Command<MoveApplicationStageCommand, JobApplicationDto>(
            new MoveApplicationStageCommand(application.Id, Requests.Version(application.RowVersion), "Selected", null, "Approved by me."));

        ResultAssert.Failed(result, ErrorType.Forbidden);
        Assert.Equal("Interview", ResultAssert.Succeeded(await host.GetJobApplication(application.Id)).Stage);
    }

    [Fact]
    public async Task An_administrator_can_approve_the_hire_with_a_reason_that_is_stored_and_logged_without_it()
    {
        var (_, application) = await ReadyListedCandidateAsync();
        await SignInAsync(SecurityPolicy.AdministratorRoleName);

        var selected = ResultAssert.Succeeded(await host.Command<MoveApplicationStageCommand, JobApplicationDto>(
            new MoveApplicationStageCommand(application.Id, Requests.Version(application.RowVersion), "Selected", null, "  Cleared by the director.  ")));

        Assert.Equal("Selected", selected.Stage);
        Assert.True(selected.BlacklistMatch);
        var stored = host.Repository<JobApplication>().Items.Single(a => a.Id == application.Id);
        Assert.Equal("Cleared by the director.", stored.BlacklistOverrideReason);
        Assert.Equal(host.User.UserId, stored.BlacklistOverrideByUserId);
        Assert.Contains($"Hire of application #{application.Id} approved despite a do-not-rehire match", host.ActivityLog.Messages);
        Assert.DoesNotContain(host.ActivityLog.Messages, message => message.Contains("director", StringComparison.Ordinal) || message.Contains("Secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_override_reason_is_at_most_three_hundred_characters()
    {
        var (_, application) = await ReadyListedCandidateAsync();
        await SignInAsync(SecurityPolicy.AdministratorRoleName);

        ResultAssert.FailedOnField(
            await host.Command<MoveApplicationStageCommand, JobApplicationDto>(
                new MoveApplicationStageCommand(application.Id, Requests.Version(application.RowVersion), "Selected", null, new string('x', 301))),
            "blacklistOverrideReason");
    }

    [Fact]
    public async Task A_candidate_without_a_match_is_selected_without_an_override_and_an_unneeded_override_is_not_stored()
    {
        var seed = await host.SeedRecruitmentAsync();
        var (_, _, application, _) = await host.ReadyToSelectAsync(seed, email: "fine.candidate@example.test");

        var selected = ResultAssert.Succeeded(await host.Command<MoveApplicationStageCommand, JobApplicationDto>(
            new MoveApplicationStageCommand(application.Id, Requests.Version(application.RowVersion), "Selected", null, "Not needed.")));

        Assert.Equal("Selected", selected.Stage);
        Assert.Null(host.Repository<JobApplication>().Items.Single(a => a.Id == application.Id).BlacklistOverrideReason);
    }

    [Fact]
    public async Task After_the_entry_is_lifted_the_candidate_can_be_selected_normally()
    {
        var seed = await host.SeedRecruitmentAsync();
        var entry = await ListAsync(await FormerEmployeeAsync(seed));
        var (_, _, application, _) = await host.ReadyToSelectAsync(seed, email: ListedEmail);
        ResultAssert.Succeeded(await Blacklist.LiftAsync(entry.Id, new BlacklistLiftRequest(Requests.Version(entry.RowVersion), "Cleared."), default));

        Assert.Equal("Selected", ResultAssert.Succeeded(await host.MoveStage(application, "Selected")).Stage);
    }
}
