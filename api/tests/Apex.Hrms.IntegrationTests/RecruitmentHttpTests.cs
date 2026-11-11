using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.IntegrationTests.Support;

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The recruitment routes over HTTP: routing, JSON shapes, the "HH:mm" time, the command bodies, the error mapping, the
/// delete protection wording and the server-owned clock. The host uses the real clock and the default business time
/// zone (Asia/Yangon, UTC+6:30), so every date is computed from the business "today".
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class RecruitmentHttpTests : DatabaseTest, IDisposable
{
    private static readonly TimeSpan BusinessOffset = TimeSpan.FromMinutes(390);

    private readonly ApiFactory factory = new();
    private readonly HttpClient client;

    public RecruitmentHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        client = factory.CreateBrowserClient(out _);
    }

    public override Task InitializeAsync() => client.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        client.Dispose();
        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private static DateOnly BusinessToday => DateOnly.FromDateTime(DateTime.UtcNow + BusinessOffset);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static string Version(JsonElement body) => body.GetProperty("rowVersion").GetString()!;

    private static int Id(JsonElement body) => body.GetProperty("id").GetInt32();

    private async Task<(int ManagerId, int InterviewerId, int DepartmentId)> SeedPeopleAsync()
    {
        var org = await SeedOrganizationAsync();
        var manager = await SeedEmployeeAsync(org, "1", "manager");
        var interviewer = await SeedEmployeeAsync(org, "2", "interviewer");
        return (manager.Id, interviewer.Id, org.Department.Id);
    }

    private async Task<JsonElement> PostAsync(string url, object body, HttpStatusCode expected)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(expected, response.StatusCode);
        return await ReadAsync(response);
    }

    private Task<HttpResponseMessage> DeleteAsync(string url, JsonElement record) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(new { rowVersion = Version(record) }),
        });

    private async Task<JsonElement> CreateJobAsync(int departmentId, int managerId, string title = "Engineer", int vacancies = 1, bool open = true)
    {
        var job = await PostAsync(
            "/api/job-openings",
            new
            {
                title = Requests.JobOpening(departmentId, managerId, BusinessToday, title).Title,
                departmentId,
                hiringManagerId = managerId,
                employmentType = "Full-time",
                vacancies,
                closingDate = Iso(BusinessToday.AddDays(60)),
                description = "Builds test services.",
            },
            HttpStatusCode.Created);
        return open ? await PostAsync($"/api/job-openings/{Id(job)}/open", new { rowVersion = Version(job) }, HttpStatusCode.OK) : job;
    }

    private async Task<JsonElement> CreateCandidateAsync(string local = "ada") =>
        await PostAsync(
            "/api/candidates",
            new
            {
                firstName = "Ada",
                lastName = "Testcandidate",
                email = Requests.CandidateEmail(local).ToUpperInvariant(),
                phone = "+95 9 555 0103",
                source = "Referral",
                currentEmployer = "Sample Employer",
                experienceYears = 4.5,
                resumeName = "cv.pdf",
            },
            HttpStatusCode.Created);

    private async Task<JsonElement> ApplyAsync(JsonElement job, JsonElement candidate) =>
        await PostAsync("/api/applications", new { jobOpeningId = Id(job), candidateId = Id(candidate) }, HttpStatusCode.Created);

    private Task<JsonElement> MoveAsync(JsonElement application, string stage, HttpStatusCode expected = HttpStatusCode.OK, string? reason = null) =>
        PostAsync($"/api/applications/{Id(application)}/stage", new { rowVersion = Version(application), stage, reason }, expected);

    private async Task<JsonElement> GetAsync(string url) => await ReadAsync(await client.GetAsync(url));

    private static object InterviewBody(int applicationId, int interviewerId, DateOnly date, string startTime = "10:00", int minutes = 60) => new
    {
        applicationId,
        round = "Technical",
        date = Iso(date),
        startTime,
        durationMinutes = minutes,
        mode = "Video",
        interviewerId,
        location = "Meeting link",
    };

    [IntegrationFact]
    public async Task The_whole_hiring_flow_works_over_http()
    {
        var (managerId, interviewerId, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId, open: false);
        Assert.Equal("Draft", job.GetProperty("status").GetString());
        Assert.Matches(@"^JOB-\d{4}-\d{3,}$", job.GetProperty("code").GetString()!);
        var open = await PostAsync($"/api/job-openings/{Id(job)}/open", new { rowVersion = Version(job) }, HttpStatusCode.OK);
        Assert.Equal("Open", open.GetProperty("status").GetString());

        var candidate = await CreateCandidateAsync();
        Assert.Equal(Requests.CandidateEmail("ada"), candidate.GetProperty("email").GetString());
        var application = await ApplyAsync(open, candidate);
        Assert.Equal("Applied", application.GetProperty("stage").GetString());
        Assert.Equal(Iso(BusinessToday), application.GetProperty("appliedOn").GetString());

        application = await MoveAsync(application, "Screening");
        var interviewResponse = await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday));
        Assert.Equal(HttpStatusCode.Created, interviewResponse.StatusCode);
        var interview = await ReadAsync(interviewResponse);
        Assert.Equal($"/api/interviews/{Id(interview)}", interviewResponse.Headers.Location?.OriginalString);
        Assert.Equal("10:00", interview.GetProperty("startTime").GetString());
        Assert.Equal("Scheduled", interview.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, interview.GetProperty("score").ValueKind);

        application = await GetAsync($"/api/applications/{Id(application)}");
        Assert.Equal("Interview", application.GetProperty("stage").GetString());
        var completed = await PostAsync(
            $"/api/interviews/{Id(interview)}/outcome",
            new { rowVersion = Version(interview), score = 5, recommendation = "Strong hire", feedback = "Excellent." },
            HttpStatusCode.OK);
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.Equal(5, completed.GetProperty("score").GetInt32());
        Assert.Equal("Strong hire", completed.GetProperty("recommendation").GetString());

        var selected = await MoveAsync(application, "Selected");
        Assert.Equal("Selected", selected.GetProperty("stage").GetString());
        var filled = await GetAsync($"/api/job-openings/{Id(job)}");
        Assert.Equal("Filled", filled.GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task Lists_use_the_query_contract_and_return_the_page_shape()
    {
        var (managerId, _, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId, "Backend");
        await CreateJobAsync(departmentId, managerId, "Designer", open: false);
        await CreateCandidateAsync();

        var jobs = await GetAsync($"/api/job-openings/query?search={Tag}&status=Open&pageSize=5&sort=title:asc");
        var candidates = await GetAsync($"/api/candidates/query?search={Tag.ToLowerInvariant()}&source=Referral");
        var all = await GetAsync("/api/job-openings");

        Assert.Equal(Id(job), Id(Assert.Single(jobs.GetProperty("items").EnumerateArray())));
        Assert.Equal(1, jobs.GetProperty("total").GetInt32());
        Assert.Equal(4.5, Assert.Single(candidates.GetProperty("items").EnumerateArray()).GetProperty("experienceYears").GetDouble());
        Assert.Equal(JsonValueKind.Array, all.ValueKind);
    }

    [IntegrationFact]
    public async Task Errors_use_the_problem_shape_for_validation_conflict_and_not_found()
    {
        var (managerId, _, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId);
        var candidate = await CreateCandidateAsync();
        var application = await ApplyAsync(job, candidate);

        var duplicateEmail = await client.PostAsJsonAsync(
            "/api/candidates",
            new { firstName = "B", lastName = "C", email = Requests.CandidateEmail("ada"), phone = "123456", source = "Agency", experienceYears = 1 });
        var skip = await client.PostAsJsonAsync($"/api/applications/{Id(application)}/stage", new { rowVersion = Version(application), stage = "Interview" });
        var noReason = await client.PostAsJsonAsync($"/api/applications/{Id(application)}/stage", new { rowVersion = Version(application), stage = "Rejected" });
        var noVersion = await client.PostAsJsonAsync($"/api/applications/{Id(application)}/stage", new { stage = "Screening" });
        var missing = await client.GetAsync("/api/applications/99999999");
        var badVacancies = await client.PostAsJsonAsync(
            "/api/job-openings",
            new { title = "x", departmentId, hiringManagerId = managerId, employmentType = "Full-time", vacancies = 0, closingDate = Iso(BusinessToday.AddDays(5)) });

        Assert.Equal(HttpStatusCode.BadRequest, duplicateEmail.StatusCode);
        var duplicateProblem = await ReadAsync(duplicateEmail);
        Assert.Equal("Validation", duplicateProblem.GetProperty("code").GetString());
        Assert.Equal("Email must be unique.", duplicateProblem.GetProperty("errors").GetProperty("email")[0].GetString());

        Assert.Equal(HttpStatusCode.Conflict, skip.StatusCode);
        var skipProblem = await ReadAsync(skip);
        Assert.Equal("Conflict", skipProblem.GetProperty("code").GetString());
        Assert.Equal("An application at \"Applied\" cannot move to \"Interview\".", skipProblem.GetProperty("detail").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("A reason is required.", (await ReadAsync(noReason)).GetProperty("errors").GetProperty("reason")[0].GetString());
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        Assert.True((await ReadAsync(noVersion)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("NotFound", (await ReadAsync(missing)).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, badVacancies.StatusCode);
        Assert.Equal("Enter 1 to 50.", (await ReadAsync(badVacancies)).GetProperty("errors").GetProperty("vacancies")[0].GetString());
    }

    [IntegrationFact]
    public async Task An_edit_with_a_stale_row_version_is_a_conflict_and_the_status_never_changes_through_put()
    {
        var (managerId, _, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId, open: false);
        var body = new
        {
            title = job.GetProperty("title").GetString(),
            departmentId,
            hiringManagerId = managerId,
            employmentType = "Full-time",
            vacancies = 2,
            postedOn = job.GetProperty("postedOn").GetString(),
            closingDate = job.GetProperty("closingDate").GetString(),
            status = "Open",
            rowVersion = Version(job),
        };

        var updated = await client.PutAsJsonAsync($"/api/job-openings/{Id(job)}", body);
        var stale = await client.PutAsJsonAsync($"/api/job-openings/{Id(job)}", body);

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = await ReadAsync(updated);
        Assert.Equal(2, result.GetProperty("vacancies").GetInt32());
        Assert.Equal("Draft", result.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [IntegrationFact]
    public async Task The_server_clock_decides_whether_an_interview_is_in_the_past_and_double_booking_is_refused()
    {
        var (managerId, interviewerId, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId);
        var application = await MoveAsync(await ApplyAsync(job, await CreateCandidateAsync()), "Screening");

        var past = await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday.AddDays(-1)));
        var first = await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday.AddDays(2)));
        var clash = await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday.AddDays(2), "10:30", 30));
        var badTime = await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday.AddDays(3), "25:99"));

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal("Choose today or a later date.", (await ReadAsync(past)).GetProperty("errors").GetProperty("date")[0].GetString());
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        Assert.Equal("Interviewer is busy from 10:00.", (await ReadAsync(clash)).GetProperty("errors").GetProperty("startTime")[0].GetString());
        Assert.Equal(HttpStatusCode.BadRequest, badTime.StatusCode);
    }

    [IntegrationFact]
    public async Task A_cancelled_interview_is_locked_and_a_completed_one_cannot_be_deleted()
    {
        var (managerId, interviewerId, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId);
        var application = await MoveAsync(await ApplyAsync(job, await CreateCandidateAsync()), "Screening");
        var toCancel = await ReadAsync(await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday.AddDays(2))));
        var toComplete = await ReadAsync(await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday, "09:00")));

        var cancelled = await PostAsync($"/api/interviews/{Id(toCancel)}/cancel", new { rowVersion = Version(toCancel), reason = "Candidate withdrew" }, HttpStatusCode.OK);
        var locked = await client.PostAsJsonAsync(
            $"/api/interviews/{Id(cancelled)}/outcome",
            new { rowVersion = Version(cancelled), score = 3, recommendation = "Hire" });
        var completed = await PostAsync(
            $"/api/interviews/{Id(toComplete)}/outcome",
            new { rowVersion = Version(toComplete), score = 3, recommendation = "Hire" },
            HttpStatusCode.OK);
        var refused = await DeleteAsync($"/api/interviews/{Id(completed)}", completed);

        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Equal("A cancelled interview cannot be changed. Schedule a new one instead.", (await ReadAsync(locked)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(
            "A completed interview is part of the hiring record and cannot be deleted.",
            (await ReadAsync(refused)).GetProperty("detail").GetString());
        var removed = await DeleteAsync($"/api/interviews/{Id(cancelled)}", cancelled);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
    }

    [IntegrationFact]
    public async Task Delete_protection_has_the_mock_wording_over_http()
    {
        var (managerId, interviewerId, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId);
        var candidate = await CreateCandidateAsync();
        var application = await MoveAsync(await ApplyAsync(job, candidate), "Screening");
        await client.PostAsJsonAsync("/api/interviews", InterviewBody(Id(application), interviewerId, BusinessToday.AddDays(2)));
        application = await GetAsync($"/api/applications/{Id(application)}");

        var jobDelete = await DeleteAsync($"/api/job-openings/{Id(job)}", job);
        var candidateDelete = await DeleteAsync($"/api/candidates/{Id(candidate)}", candidate);
        var applicationDelete = await DeleteAsync($"/api/applications/{Id(application)}", application);

        Assert.Equal(HttpStatusCode.Conflict, jobDelete.StatusCode);
        var jobDetail = (await ReadAsync(jobDelete)).GetProperty("detail").GetString()!;
        Assert.Contains("cannot be deleted because it is used by 1 application(s). Deactivate it instead.", jobDetail, StringComparison.Ordinal);
        Assert.StartsWith("\"JOB-", jobDetail, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, candidateDelete.StatusCode);
        Assert.Equal(
            "\"Ada Testcandidate\" cannot be deleted because it is used by 1 application(s). Deactivate it instead.",
            (await ReadAsync(candidateDelete)).GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, applicationDelete.StatusCode);
        Assert.EndsWith(
            "cannot be deleted because it is used by 1 interview(s). Deactivate it instead.",
            (await ReadAsync(applicationDelete)).GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task The_pipeline_and_counts_views_have_the_documented_shapes()
    {
        var (managerId, _, departmentId) = await SeedPeopleAsync();
        var job = await CreateJobAsync(departmentId, managerId);
        var application = await ApplyAsync(job, await CreateCandidateAsync());

        var pipeline = await GetAsync($"/api/job-openings/{Id(job)}/pipeline");
        var everything = await GetAsync($"/api/recruitment/pipeline?jobOpeningId={Id(job)}");
        var counts = await GetAsync("/api/recruitment/counts");
        var unknown = await client.GetAsync("/api/job-openings/99999999/pipeline");

        var columns = pipeline.GetProperty("columns").EnumerateArray().ToList();
        Assert.Equal(["Applied", "Screening", "Interview", "Selected", "Rejected"], columns.Select(column => column.GetProperty("stage").GetString()));
        var card = Assert.Single(columns[0].GetProperty("cards").EnumerateArray());
        Assert.Equal(Id(application), card.GetProperty("applicationId").GetInt32());
        Assert.Equal("Ada Testcandidate", card.GetProperty("candidateName").GetString());
        Assert.Equal(JsonValueKind.Null, card.GetProperty("nextInterview").ValueKind);
        Assert.Equal(0, card.GetProperty("completedInterviews").GetInt32());
        Assert.Equal(Version(application), card.GetProperty("rowVersion").GetString());
        Assert.Equal(Id(job), everything.GetProperty("jobOpeningId").GetInt32());
        Assert.Contains(counts.GetProperty("jobOpenings").EnumerateArray(), row => row.GetProperty("jobOpeningId").GetInt32() == Id(job) && row.GetProperty("applications").GetInt32() == 1);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [IntegrationFact]
    public async Task An_anonymous_caller_is_refused()
    {
        using var anonymous = factory.CreateBrowserClient(out _);

        var response = await anonymous.GetAsync("/api/job-openings/query");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
