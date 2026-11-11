using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Recruitment.Applications;
using Apex.Hrms.Application.Recruitment.Candidates;
using Apex.Hrms.Application.Recruitment.Interviews;
using Apex.Hrms.Application.Recruitment.JobOpenings;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

/// <summary>The organization, a hiring manager and an interviewer every recruitment test needs.</summary>
internal sealed record RecruitmentSeed(OrgSeed Org, EmployeeDto Manager, EmployeeDto Interviewer);

/// <summary>
/// Fictional recruitment requests and the handler calls the recruitment tests share. The fake clock says today is Monday
/// 2026-11-09.
/// </summary>
internal static class RecruitmentFixtures
{
    public static readonly DateOnly Today = new(2026, 11, 9);

    public static async Task<RecruitmentSeed> SeedRecruitmentAsync(this TestHost host)
    {
        var org = await host.SeedOrgAsync();
        var manager = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: "manager@example.test"));
        var interviewer = await host.CreateEmployeeAsync(EmployeeFixtures.NewEmployee(org, email: "interviewer@example.test"));
        return new RecruitmentSeed(org, manager, interviewer);
    }

    public static JobOpeningCreateRequest NewJob(
        RecruitmentSeed seed,
        string title = "Backend Engineer",
        int vacancies = 1,
        DateOnly? posted = null,
        DateOnly? closing = null) => new(
        title,
        seed.Org.Department.Id,
        seed.Manager.Id,
        "Full-time",
        vacancies,
        posted,
        closing ?? Today.AddDays(30),
        "Builds services.");

    public static JobOpeningUpdateRequest JobUpdateOf(JobOpeningDto current, string? title = null, int? vacancies = null, DateOnly? closing = null) => new(
        title ?? current.Title,
        current.DepartmentId,
        current.HiringManagerId,
        current.EmploymentType,
        vacancies ?? current.Vacancies,
        current.PostedOn,
        closing ?? current.ClosingDate,
        current.Description,
        Requests.Version(current.RowVersion));

    public static CandidateCreateRequest NewCandidate(string email = "ada.test@example.test", string first = "Ada", decimal years = 4m) => new(
        first,
        "Test",
        email,
        "+95 9 555 0100",
        "Referral",
        "Sample Employer",
        years,
        "ada-cv.pdf",
        null);

    public static CandidateUpdateRequest CandidateUpdateOf(CandidateDto current, string? email = null, decimal? years = null) => new(
        current.FirstName,
        current.LastName,
        email ?? current.Email,
        current.Phone,
        current.Source,
        current.CurrentEmployer,
        years ?? current.ExperienceYears,
        current.ResumeName,
        current.Notes,
        Requests.Version(current.RowVersion));

    public static InterviewCreateRequest NewInterview(
        int applicationId,
        int interviewerId,
        DateOnly? date = null,
        int startHour = 10,
        int minutes = 60,
        string round = "Technical") => new(
        applicationId,
        round,
        date ?? Today.AddDays(2),
        new TimeOnly(startHour, 0),
        minutes,
        "Video",
        interviewerId,
        "Meeting link");

    public static InterviewUpdateRequest InterviewUpdateOf(InterviewDto current, DateOnly? date = null, int? startHour = null, int? interviewerId = null) => new(
        current.ApplicationId,
        current.Round,
        date ?? current.Date,
        startHour is { } hour ? new TimeOnly(hour, 0) : current.StartTime,
        current.DurationMinutes,
        current.Mode,
        interviewerId ?? current.InterviewerId,
        current.Location,
        Requests.Version(current.RowVersion));

    // ---- Calls ----

    public static Task<Result<JobOpeningDto>> CreateJob(this TestHost host, JobOpeningCreateRequest request) =>
        host.Get<ICreateHandler<JobOpeningCreateRequest, JobOpeningDto>>().HandleAsync(request, default);

    public static async Task<JobOpeningDto> CreateJobAsync(this TestHost host, JobOpeningCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreateJob(request));

    public static Task<Result<JobOpeningDto>> GetJob(this TestHost host, int id) =>
        host.Get<IGetByIdHandler<JobOpeningDto>>().HandleAsync(id, default);

    public static Task<Result<JobOpeningDto>> OpenJob(this TestHost host, JobOpeningDto job) =>
        host.Command<OpenJobOpeningCommand, JobOpeningDto>(new OpenJobOpeningCommand(job.Id, Requests.Version(job.RowVersion)));

    /// <summary>Creates a draft opening and opens it.</summary>
    public static async Task<JobOpeningDto> OpenJobAsync(this TestHost host, JobOpeningCreateRequest request) =>
        ResultAssert.Succeeded(await host.OpenJob(await host.CreateJobAsync(request)));

    public static Task<Result<CandidateDto>> CreateCandidate(this TestHost host, CandidateCreateRequest request) =>
        host.Get<ICreateHandler<CandidateCreateRequest, CandidateDto>>().HandleAsync(request, default);

    public static async Task<CandidateDto> CreateCandidateAsync(this TestHost host, CandidateCreateRequest? request = null) =>
        ResultAssert.Succeeded(await host.CreateCandidate(request ?? NewCandidate()));

    public static Task<Result<JobApplicationDto>> Apply(this TestHost host, int jobId, int candidateId, string? notes = null) =>
        host.Get<ICreateHandler<JobApplicationCreateRequest, JobApplicationDto>>().HandleAsync(new JobApplicationCreateRequest(jobId, candidateId, notes), default);

    public static async Task<JobApplicationDto> ApplyAsync(this TestHost host, int jobId, int candidateId) =>
        ResultAssert.Succeeded(await host.Apply(jobId, candidateId));

    public static Task<Result<JobApplicationDto>> GetJobApplication(this TestHost host, int id) =>
        host.Get<IGetByIdHandler<JobApplicationDto>>().HandleAsync(id, default);

    public static Task<Result<JobApplicationDto>> MoveStage(this TestHost host, JobApplicationDto application, string stage, string? reason = null) =>
        host.Command<MoveApplicationStageCommand, JobApplicationDto>(
            new MoveApplicationStageCommand(application.Id, Requests.Version(application.RowVersion), stage, reason));

    /// <summary>Moves an application through the given stages in order, failing the test when a move is refused.</summary>
    public static async Task<JobApplicationDto> MoveThroughAsync(this TestHost host, JobApplicationDto application, params string[] stages)
    {
        foreach (var stage in stages)
        {
            application = ResultAssert.Succeeded(await host.MoveStage(application, stage, stage == "Rejected" ? "Not a fit" : null));
        }

        return application;
    }

    public static Task<Result<InterviewDto>> CreateInterview(this TestHost host, InterviewCreateRequest request) =>
        host.Get<ICreateHandler<InterviewCreateRequest, InterviewDto>>().HandleAsync(request, default);

    public static async Task<InterviewDto> CreateInterviewAsync(this TestHost host, InterviewCreateRequest request) =>
        ResultAssert.Succeeded(await host.CreateInterview(request));

    public static Task<Result<InterviewDto>> GetInterview(this TestHost host, int id) =>
        host.Get<IGetByIdHandler<InterviewDto>>().HandleAsync(id, default);

    public static Task<Result<InterviewDto>> Outcome(this TestHost host, InterviewDto interview, int? score = 4, string? recommendation = "Hire", string? feedback = "Good.") =>
        host.Command<RecordInterviewOutcomeCommand, InterviewDto>(
            new RecordInterviewOutcomeCommand(interview.Id, Requests.Version(interview.RowVersion), score, recommendation, feedback));

    public static Task<Result<InterviewDto>> CancelInterview(this TestHost host, InterviewDto interview, string? reason = null) =>
        host.Command<CancelInterviewCommand, InterviewDto>(
            new CancelInterviewCommand(interview.Id, Requests.Version(interview.RowVersion), reason));

    /// <summary>An open opening with one applicant who is in the Interview stage and has a completed positive interview.</summary>
    public static async Task<(JobOpeningDto Job, CandidateDto Candidate, JobApplicationDto Application, InterviewDto Interview)> ReadyToSelectAsync(
        this TestHost host,
        RecruitmentSeed seed,
        int vacancies = 1,
        string email = "ada.test@example.test")
    {
        var job = await host.OpenJobAsync(NewJob(seed, vacancies: vacancies));
        var candidate = await host.CreateCandidateAsync(NewCandidate(email));
        var application = await host.MoveThroughAsync(await host.ApplyAsync(job.Id, candidate.Id), "Screening");
        var interview = await host.CreateInterviewAsync(NewInterview(application.Id, seed.Interviewer.Id, Today));
        interview = ResultAssert.Succeeded(await host.Outcome(interview));
        application = ResultAssert.Succeeded(await host.GetJobApplication(application.Id));
        return (job, candidate, application, interview);
    }
}
