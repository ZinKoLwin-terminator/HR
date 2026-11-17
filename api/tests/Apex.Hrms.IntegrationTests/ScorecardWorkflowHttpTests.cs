using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Application.Performance.Periods;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.IntegrationTests.Support;
using Org = (Apex.Hrms.Application.Organization.Companies.CompanyDto Company, Apex.Hrms.Application.Organization.Branches.BranchDto Branch, Apex.Hrms.Application.Organization.Departments.DepartmentDto Department, Apex.Hrms.Application.Organization.JobGrades.JobGradeDto Grade, Apex.Hrms.Application.Organization.Positions.PositionDto Position);

namespace Apex.Hrms.IntegrationTests;

/// <summary>
/// The submission and review routes over HTTP against the test database, with real roles: the JSON shapes and the status and action
/// texts a screen shows, the error mapping (400 with field paths, 403, 404, 409), who may act (the employee, the reviewer, the HR tier,
/// never the employee on their own scorecard), the separation of duties, the review queue and my scorecards, and two approvals racing.
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
[Trait("Category", "Integration")]
public sealed class ScorecardWorkflowHttpTests : DatabaseTest, IDisposable
{
    private static readonly string[] EmployeeGrants = ["kpi:View", "kpi:Create"];
    private static readonly string[] ManagerGrants = ["kpi:View", "kpi:Create", "kpi:Approve"];
    private static readonly string[] HrGrants = ["kpi:View", "kpi:Create", "kpi:Approve", "employees:View", "employees:Edit"];

    private readonly ApiFactory factory = new();
    private readonly HttpClient admin;
    private readonly List<HttpClient> others = [];

    public ScorecardWorkflowHttpTests()
    {
        factory.EnsurePointsAtTestDatabase();
        admin = factory.CreateBrowserClient(out _);
    }

    public override Task InitializeAsync() => admin.SignInAsync(TestAdministrator.Account);

    protected override void Cleanup()
    {
        admin.Dispose();
        foreach (var other in others)
        {
            other.Dispose();
        }

        factory.Dispose();
    }

    public void Dispose() => Cleanup();

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())!;

    private static Task<HttpResponseMessage> Send(HttpClient http, HttpMethod method, string url, object? body) =>
        http.SendAsync(new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) });

    private async Task<HttpClient> SignedInAsync(string suffix, int? employeeId, params string[] grants)
    {
        var role = $"{Tag}-{suffix}";
        var roleId = await Sql.ScalarAsync<int>("INSERT INTO sec.Role (Name, IsSystem, Status) OUTPUT INSERTED.Id VALUES (@name, 0, 'Active');", ("@name", role));
        foreach (var grant in grants.OrderBy(grant => grant.EndsWith(":View", StringComparison.Ordinal) ? 0 : 1))
        {
            var parts = grant.Split(':');
            await Sql.ExecuteAsync(
                "INSERT INTO sec.RolePermission (RoleId, ModuleKey, Action) VALUES (@role, @module, @action);",
                ("@role", roleId),
                ("@module", parts[0]),
                ("@action", parts[1]));
        }

        var account = await TestAccounts.CreateAsync(TestAccounts.EmailFor(Tag, $"{suffix}-login"), roleName: role, employeeId: employeeId);
        var signedIn = factory.CreateBrowserClient(out _);
        others.Add(signedIn);
        await signedIn.SignInAsync(account);
        return signedIn;
    }

    private sealed record Setup(
        Org Org,
        EvaluationPeriodDto Period,
        EmployeeDto Anna,
        EmployeeDto Ben,
        EmployeeDto Zed,
        EmployeeDto Hana,
        EmployeeDto Cara,
        ScorecardDetailDto Card,
        int FirstItem,
        int SecondItem,
        int Template);

    private async Task<EmployeeDto> EmployeeAsync(Org org, string suffix, string local, int? managerId = null) =>
        Succeeded(await Create(
            Requests.Employee(org.Company.Id, org.Branch.Id, org.Department.Id, org.Position.Id, org.Grade.Id, suffix, local) with { ManagerId = managerId }));

    /// <summary>Anna manages Ben and Cara; Zed and Hana manage nobody. Ben's plan of two KPIs (60% and 40%, target 100) is final, so his scorecard is in Draft with Anna as reviewer.</summary>
    private async Task<Setup> SetupAsync(bool hrReview = false)
    {
        var org = await SeedOrganizationAsync();
        var category = await SeedKpiCategoryAsync();
        var first = await SeedKpiTemplateAsync(category.Id, "S-01", "Monthly sales");
        var second = await SeedKpiTemplateAsync(category.Id, "S-02", "Quality score");
        var template = await SeedKpiTemplateAsync(category.Id, "S-03", "Extra");
        var draft = Succeeded(await Create(Requests.Period(org.Company.Id, hr: hrReview)));
        var period = Succeeded(await RunCommand<ActivatePeriodCommand, EvaluationPeriodDto>(new ActivatePeriodCommand(draft.Id, Requests.Version(draft.RowVersion))));
        var anna = await EmployeeAsync(org, "1", "anna");
        var ben = await EmployeeAsync(org, "2", "ben", anna.Id);
        var zed = await EmployeeAsync(org, "3", "zed");
        var hana = await EmployeeAsync(org, "4", "hana");
        var cara = await EmployeeAsync(org, "5", "cara", anna.Id);
        var card = Succeeded(await FinalizePlan(await SeedScorecardAsync(ben.Id, period.Id, ItemOf(first.Id, weight: 60m), ItemOf(second.Id, weight: 40m))));
        return new Setup(org, period, anna, ben, zed, hana, cara, card, card.Items[0].Id, card.Items[1].Id, template.Id);
    }

    private static string Version(JsonElement json) => json.GetProperty("rowVersion").GetString()!;

    private static object Result(string rowVersion, int id, decimal? actual, string? comment = null) =>
        new { rowVersion, items = new[] { new { id, actualValue = actual, employeeComment = comment } } };

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        var json = await ReadAsync(response);
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {json}");
        return json;
    }

    /// <summary>Both KPIs filled (100 and 50, so 80 points) by the employee over HTTP; returns the scorecard.</summary>
    private sealed record ResultBody(int Id, decimal? ActualValue, string? EmployeeComment);

    private static async Task<JsonElement> FillAsync(HttpClient employee, Setup setup)
    {
        var body = new
        {
            rowVersion = Requests.Version(setup.Card.RowVersion),
            employeeComment = "A busy quarter",
            items = new[] { new ResultBody(setup.FirstItem, 100m, "Hit it"), new ResultBody(setup.SecondItem, 50m, null) },
        };
        return await OkAsync(await Send(employee, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/results", body));
    }

    private static async Task<JsonElement> SubmittedAsync(HttpClient employee, Setup setup)
    {
        var filled = await FillAsync(employee, setup);
        return await OkAsync(await Send(employee, HttpMethod.Post, $"/api/kpi/scorecards/{setup.Card.Id}/submit", new { rowVersion = Version(filled) }));
    }

    private static Task<HttpResponseMessage> Step(HttpClient http, int id, string action, JsonElement card, string? comment = null) =>
        Send(http, HttpMethod.Post, $"/api/kpi/scorecards/{id}/{action}", new { rowVersion = Version(card), comment });

    private static async Task<string> DetailAsync(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("detail").GetString()!;

    private async Task ChangeManagerAsync(EmployeeDto employee, int managerId)
    {
        var current = Succeeded(await Get<EmployeeDto>(employee.Id));
        Succeeded(await Update(employee.Id, Requests.EmployeeUpdate(current) with { ManagerId = managerId }));
    }

    // ---- The whole path with the shapes a screen needs ----

    [IntegrationFact]
    public async Task The_employee_saves_and_submits_and_the_manager_reviews_and_approves_with_the_documented_shapes()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);

        var filled = await FillAsync(ben, setup);

        Assert.Equal("Draft", filled.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, filled.GetProperty("overallScore").ValueKind);
        Assert.Equal("A busy quarter", filled.GetProperty("employeeComment").GetString());
        Assert.Equal("Active", filled.GetProperty("periodStatus").GetString());
        Assert.False(filled.GetProperty("requiresHrReview").GetBoolean());
        Assert.Equal("2027-01-10", filled.GetProperty("submissionDueDate").GetString());
        var first = filled.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetInt32() == setup.FirstItem);
        Assert.Equal(100m, first.GetProperty("actualValue").GetDecimal());
        Assert.Equal(100m, first.GetProperty("achievementPct").GetDecimal());
        Assert.Equal(60m, first.GetProperty("weightedScore").GetDecimal());
        Assert.Equal("Hit it", first.GetProperty("employeeComment").GetString());
        Assert.NotEqual(Requests.Version(setup.Card.RowVersion), Version(filled));

        var submitted = await OkAsync(await Send(ben, HttpMethod.Post, $"/api/kpi/scorecards/{setup.Card.Id}/submit", new { rowVersion = Version(filled) }));
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        Assert.Equal(80m, submitted.GetProperty("overallScore").GetDecimal());
        Assert.Equal(JsonValueKind.String, submitted.GetProperty("ratingLabel").ValueKind);
        Assert.Equal(JsonValueKind.String, submitted.GetProperty("submittedOn").ValueKind);

        var started = await OkAsync(await Step(anna, setup.Card.Id, "start-review", submitted));
        Assert.Equal("Under Review", started.GetProperty("status").GetString());

        var draft = await OkAsync(await Send(anna, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/review", new
        {
            rowVersion = Version(started),
            managerRating = 4.5m,
            managerComment = "Solid quarter",
            items = new[] { new { id = setup.FirstItem, managerRating = (decimal?)5m, managerComment = "Great" } },
        }));
        Assert.Equal("Under Review", draft.GetProperty("status").GetString());
        Assert.Equal(4.5m, draft.GetProperty("managerRating").GetDecimal());
        Assert.Equal("Solid quarter", draft.GetProperty("managerComment").GetString());

        var approved = await OkAsync(await Step(anna, setup.Card.Id, "approve", draft, "Well done"));
        Assert.Equal("Finalized", approved.GetProperty("status").GetString());
        Assert.Equal(80m, approved.GetProperty("overallScore").GetDecimal());
        Assert.Equal(JsonValueKind.String, approved.GetProperty("finalizedOn").ValueKind);
        Assert.Equal(JsonValueKind.String, approved.GetProperty("reviewedOn").ValueKind);

        var history = approved.GetProperty("history").EnumerateArray().ToList();
        Assert.Equal(["Finalize Plan", "Submit", "Start Review", "Approve"], history.Select(step => step.GetProperty("action").GetString()));
        Assert.Equal(["HR", "Employee", "Manager", "Manager"], history.Select(step => step.GetProperty("step").GetString()));
        Assert.Equal("Under Review", history[3].GetProperty("fromStatus").GetString());
        Assert.Equal("Finalized", history[3].GetProperty("toStatus").GetString());
        Assert.Equal("Well done", history[3].GetProperty("comment").GetString());
        Assert.Equal(80m, history[3].GetProperty("overallScore").GetDecimal());
    }

    [IntegrationFact]
    public async Task A_period_with_an_hr_review_makes_the_approval_wait_and_the_approver_cannot_give_the_final_review()
    {
        var setup = await SetupAsync(hrReview: true);
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var hrOne = await SignedInAsync("HrOne", null, HrGrants);
        var submitted = await SubmittedAsync(ben, setup);

        var approved = await OkAsync(await Step(anna, setup.Card.Id, "approve", submitted));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.True(approved.GetProperty("requiresHrReview").GetBoolean());
        Assert.Equal(JsonValueKind.Null, approved.GetProperty("finalizedOn").ValueKind);

        var managerFinalizes = await Step(anna, setup.Card.Id, "finalize", approved);
        Assert.Equal(HttpStatusCode.Forbidden, managerFinalizes.StatusCode);
        Assert.Equal("Only HR can give the final review of a scorecard.", await DetailAsync(managerFinalizes));

        var returned = await Step(anna, setup.Card.Id, "return", approved, "Too late");
        Assert.Equal(HttpStatusCode.Forbidden, returned.StatusCode);

        var finalized = await OkAsync(await Step(hrOne, setup.Card.Id, "finalize", approved, "Calibrated"));
        Assert.Equal("Finalized", finalized.GetProperty("status").GetString());
        Assert.Equal("Calibrated", finalized.GetProperty("hrComment").GetString());
        var last = finalized.GetProperty("history").EnumerateArray().Last();
        Assert.Equal(("HR", "Finalize", "Approved", "Finalized"), (last.GetProperty("step").GetString(), last.GetProperty("action").GetString(), last.GetProperty("fromStatus").GetString(), last.GetProperty("toStatus").GetString()));
    }

    [IntegrationFact]
    public async Task The_hr_user_who_approved_cannot_finalize_but_another_hr_user_can()
    {
        var setup = await SetupAsync(hrReview: true);
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var hrOne = await SignedInAsync("HrOne", null, HrGrants);
        var hrTwo = await SignedInAsync("HrTwo", setup.Hana.Id, HrGrants);
        var submitted = await SubmittedAsync(ben, setup);
        var approved = await OkAsync(await Step(hrOne, setup.Card.Id, "approve", submitted));

        var same = await Step(hrOne, setup.Card.Id, "finalize", approved);
        var sameReturn = await Step(hrOne, setup.Card.Id, "return", approved, "No");
        var other = await Step(hrTwo, setup.Card.Id, "finalize", approved);

        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.Equal("The person who approved this scorecard cannot also give the HR final review.", await DetailAsync(same));
        Assert.Equal(HttpStatusCode.Conflict, sameReturn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal("Finalized", (await ReadAsync(other)).GetProperty("status").GetString());
    }

    // ---- Who may act ----

    [IntegrationFact]
    public async Task Nobody_reviews_their_own_scorecard_even_the_hr_tier_and_only_the_own_employee_enters_results()
    {
        var setup = await SetupAsync();
        var benAsHr = await SignedInAsync("BenHr", setup.Ben.Id, HrGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var submitted = await SubmittedAsync(benAsHr, setup);

        foreach (var action in new[] { "start-review", "approve", "finalize" })
        {
            var own = await Step(benAsHr, setup.Card.Id, action, submitted);
            Assert.Equal(HttpStatusCode.Conflict, own.StatusCode);
            Assert.Equal("You cannot review your own scorecard.", await DetailAsync(own));
        }

        Assert.Equal(HttpStatusCode.Conflict, (await Step(benAsHr, setup.Card.Id, "return", submitted, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Step(benAsHr, setup.Card.Id, "reject", submitted, "x")).StatusCode);

        var asManager = await Send(anna, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/results", Result(Version(submitted), setup.FirstItem, 1m));
        var asAdmin = await Send(admin, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/results", Result(Version(submitted), setup.FirstItem, 1m));
        var submitAsManager = await Step(anna, setup.Card.Id, "submit", submitted);
        Assert.Equal(HttpStatusCode.Forbidden, asManager.StatusCode);
        Assert.Equal("You can only enter results on your own scorecard.", await DetailAsync(asManager));
        Assert.Equal(HttpStatusCode.Forbidden, asAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, submitAsManager.StatusCode);
        Assert.Equal("Submitted", Succeeded(await Get<ScorecardDetailDto>(setup.Card.Id)).Status);
    }

    [IntegrationFact]
    public async Task A_stranger_gets_a_404_and_a_manager_who_is_no_longer_the_reviewer_a_403_while_the_reviewer_still_acts()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var zed = await SignedInAsync("Zed", setup.Zed.Id, ManagerGrants);
        var submitted = await SubmittedAsync(ben, setup);

        var strangerGet = await zed.GetAsync($"/api/kpi/scorecards/{setup.Card.Id}");
        var strangerApprove = await Step(zed, setup.Card.Id, "approve", submitted);
        Assert.Equal(HttpStatusCode.NotFound, strangerGet.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerApprove.StatusCode);

        await ChangeManagerAsync(setup.Ben, setup.Zed.Id);

        var newManagerGet = await zed.GetAsync($"/api/kpi/scorecards/{setup.Card.Id}");
        var newManagerApprove = await Step(zed, setup.Card.Id, "approve", submitted);
        var oldManagerGet = await anna.GetAsync($"/api/kpi/scorecards/{setup.Card.Id}");
        var oldList = await ReadAsync(await anna.GetAsync("/api/kpi/scorecards/query?pageSize=200"));
        var newList = await ReadAsync(await zed.GetAsync("/api/kpi/scorecards/query?pageSize=200"));
        Assert.Equal(HttpStatusCode.OK, newManagerGet.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, newManagerApprove.StatusCode);
        Assert.Equal("Only the employee's reviewer or HR can review this scorecard.", await DetailAsync(newManagerApprove));
        Assert.Equal(HttpStatusCode.OK, oldManagerGet.StatusCode);
        Assert.Contains(setup.Card.Id, oldList.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()));
        Assert.Contains(setup.Card.Id, newList.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()));
        Assert.Equal(setup.Anna.Id, (await ReadAsync(oldManagerGet)).GetProperty("reviewerEmployeeId").GetInt32());

        var approved = await OkAsync(await Step(anna, setup.Card.Id, "approve", submitted));
        Assert.Equal("Finalized", approved.GetProperty("status").GetString());
    }

    [IntegrationFact]
    public async Task An_employee_role_cannot_use_the_review_routes_and_an_anonymous_caller_gets_401()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var submitted = await SubmittedAsync(ben, setup);
        using var anonymous = factory.CreateBrowserClient(out _);

        foreach (var action in new[] { "start-review", "approve", "return", "reject", "finalize" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Step(ben, setup.Card.Id, action, submitted, "x")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Step(anonymous, setup.Card.Id, action, submitted, "x")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(ben, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/review", new { rowVersion = Version(submitted), items = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ben.GetAsync("/api/kpi/reviews/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/kpi/reviews/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/kpi/my-scorecards")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(anonymous, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/results", Result(Version(submitted), setup.FirstItem, 1m))).StatusCode);
    }

    // ---- Errors ----

    [IntegrationFact]
    public async Task Bad_input_is_a_400_with_field_paths_and_a_wrong_status_or_version_is_a_409()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var version = Requests.Version(setup.Card.RowVersion);
        var url = $"/api/kpi/scorecards/{setup.Card.Id}";

        var noBody = await Send(ben, HttpMethod.Post, $"{url}/submit", null);
        var tooPrecise = await Send(ben, HttpMethod.Put, $"{url}/results", Result(version, setup.FirstItem, 1.23456m));
        var foreign = await Send(ben, HttpMethod.Put, $"{url}/results", Result(version, 2_000_000_000, 1m));
        var twice = await Send(ben, HttpMethod.Put, $"{url}/results", new { rowVersion = version, items = new[] { new { id = setup.FirstItem, actualValue = 1m }, new { id = setup.FirstItem, actualValue = 2m } } });
        var missing = await Send(ben, HttpMethod.Post, $"{url}/submit", new { rowVersion = version });
        var notSubmitted = await Send(anna, HttpMethod.Post, $"{url}/approve", new { rowVersion = version });
        var stale = await Send(ben, HttpMethod.Post, $"{url}/submit", new { rowVersion = "AAAAAAAAAAA=" });

        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);
        Assert.True((await ReadAsync(noBody)).GetProperty("errors").TryGetProperty("rowVersion", out _));
        Assert.True((await ReadAsync(tooPrecise)).GetProperty("errors").TryGetProperty("items[0].actualValue", out _));
        Assert.True((await ReadAsync(foreign)).GetProperty("errors").TryGetProperty("items[0].id", out _));
        Assert.True((await ReadAsync(twice)).GetProperty("errors").TryGetProperty("items[1].id", out _));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var errors = (await ReadAsync(missing)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("items[0].actualValue", out _));
        Assert.True(errors.TryGetProperty("items[1].actualValue", out _));
        Assert.Equal(HttpStatusCode.Conflict, notSubmitted.StatusCode);
        Assert.Contains("Draft", await DetailAsync(notSubmitted), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [IntegrationFact]
    public async Task Returning_or_rejecting_needs_a_reason_and_a_rejection_is_final()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var submitted = await SubmittedAsync(ben, setup);

        var noReturnReason = await Step(anna, setup.Card.Id, "return", submitted, "  ");
        var noRejectReason = await Step(anna, setup.Card.Id, "reject", submitted, null);
        var tooLong = await Step(anna, setup.Card.Id, "return", submitted, new string('c', 1001));
        foreach (var refused in new[] { noReturnReason, noRejectReason })
        {
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("A reason is required.", (await ReadAsync(refused)).GetProperty("errors").GetProperty("comment")[0].GetString());
        }

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var rejected = await OkAsync(await Step(anna, setup.Card.Id, "reject", submitted, "Not credible"));
        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Step(anna, setup.Card.Id, "approve", rejected)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Step(anna, setup.Card.Id, "return", rejected, "again")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(ben, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/results", Result(Version(rejected), setup.FirstItem, 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Step(ben, setup.Card.Id, "submit", rejected)).StatusCode);
    }

    [IntegrationFact]
    public async Task A_returned_scorecard_is_edited_and_resubmitted_over_http_and_the_trail_shows_every_step()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var submitted = await SubmittedAsync(ben, setup);

        var returned = await OkAsync(await Step(anna, setup.Card.Id, "return", submitted, "Quality score looks too low"));
        Assert.Equal("Returned", returned.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, returned.GetProperty("overallScore").ValueKind);
        var edited = await OkAsync(await Send(ben, HttpMethod.Put, $"/api/kpi/scorecards/{setup.Card.Id}/results", Result(Version(returned), setup.SecondItem, 90m, "Re-measured")));
        var again = await OkAsync(await Step(ben, setup.Card.Id, "submit", edited));

        Assert.Equal("Resubmitted", again.GetProperty("status").GetString());
        Assert.Equal(96m, again.GetProperty("overallScore").GetDecimal());
        var actions = again.GetProperty("history").EnumerateArray().Select(step => step.GetProperty("action").GetString()).ToList();
        Assert.Equal(["Finalize Plan", "Submit", "Return", "Resubmit"], actions);
        var returnStep = again.GetProperty("history")[2];
        Assert.Equal("Quality score looks too low", returnStep.GetProperty("comment").GetString());
        Assert.Equal(80m, returnStep.GetProperty("overallScore").GetDecimal());
    }

    [IntegrationFact]
    public async Task Two_approvals_racing_on_the_same_version_give_one_200_and_one_409()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var hrOne = await SignedInAsync("HrOne", null, HrGrants);
        var submitted = await SubmittedAsync(ben, setup);

        var responses = await Task.WhenAll(Step(anna, setup.Card.Id, "approve", submitted, "one"), Step(hrOne, setup.Card.Id, "approve", submitted, "two"));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], responses.Select(response => response.StatusCode).Order());
        Assert.Equal(1, await Sql.ScalarAsync<int>("SELECT COUNT(*) FROM kpi.ReviewAction WHERE ScorecardId = @id AND Action = 'Approve'", ("@id", setup.Card.Id)));
    }

    // ---- The queues ----

    [IntegrationFact]
    public async Task My_scorecards_are_the_callers_own_without_planning_and_the_administrator_has_none()
    {
        var setup = await SetupAsync();
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var cara = await SeedScorecardAsync(setup.Cara.Id, setup.Period.Id, ItemOf(setup.Template, weight: 100m));
        Assert.Equal("Planning", cara.Status);

        var mine = await ReadAsync(await ben.GetAsync("/api/kpi/my-scorecards"));
        var caraClient = await SignedInAsync("Cara", setup.Cara.Id, EmployeeGrants);
        var hers = await ReadAsync(await caraClient.GetAsync("/api/kpi/my-scorecards"));
        var adminPage = await admin.GetAsync("/api/kpi/my-scorecards?pageSize=5");

        Assert.Equal(1, mine.GetProperty("total").GetInt32());
        var card = mine.GetProperty("items")[0];
        Assert.Equal(setup.Card.Id, card.GetProperty("id").GetInt32());
        Assert.Equal(setup.Ben.EmployeeCode, card.GetProperty("employeeCode").GetString());
        Assert.Equal("Draft", card.GetProperty("status").GetString());
        Assert.Equal(0, hers.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, adminPage.StatusCode);
        var empty = await ReadAsync(adminPage);
        Assert.Equal((0, 0, 5), (empty.GetProperty("total").GetInt32(), empty.GetProperty("items").GetArrayLength(), empty.GetProperty("pageSize").GetInt32()));
    }

    [IntegrationFact]
    public async Task The_review_queue_shows_a_manager_the_scorecards_naming_them_and_the_hr_tier_every_waiting_one_without_its_own()
    {
        var setup = await SetupAsync(hrReview: true);
        var template = setup.Template;
        var caraCard = Succeeded(await FinalizePlan(await SeedScorecardAsync(setup.Cara.Id, setup.Period.Id, ItemOf(template, weight: 100m))));
        var hanaCard = Succeeded(await FinalizePlan(await SeedScorecardAsync(setup.Hana.Id, setup.Period.Id, ItemOf(template, weight: 100m))));
        var ben = await SignedInAsync("Ben", setup.Ben.Id, EmployeeGrants);
        var cara = await SignedInAsync("Cara", setup.Cara.Id, EmployeeGrants);
        var hana = await SignedInAsync("Hana", setup.Hana.Id, HrGrants);
        var anna = await SignedInAsync("Anna", setup.Anna.Id, ManagerGrants);
        var zed = await SignedInAsync("Zed", setup.Zed.Id, ManagerGrants);
        var hrOne = await SignedInAsync("HrOne", null, HrGrants);
        var benSubmitted = await SubmittedAsync(ben, setup);
        var caraFilled = await OkAsync(await Send(cara, HttpMethod.Put, $"/api/kpi/scorecards/{caraCard.Id}/results", Result(Requests.Version(caraCard.RowVersion), caraCard.Items[0].Id, 70m)));
        await OkAsync(await Step(cara, caraCard.Id, "submit", caraFilled));
        var hanaFilled = await OkAsync(await Send(hana, HttpMethod.Put, $"/api/kpi/scorecards/{hanaCard.Id}/results", Result(Requests.Version(hanaCard.RowVersion), hanaCard.Items[0].Id, 60m)));
        await OkAsync(await Step(hana, hanaCard.Id, "submit", hanaFilled));

        static async Task<int[]> IdsAsync(HttpClient http, string query = "") =>
            [.. (await ReadAsync(await http.GetAsync($"/api/kpi/reviews/pending?pageSize=200{query}"))).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).Order()];

        Assert.Equal([setup.Card.Id, caraCard.Id], await IdsAsync(anna));
        Assert.Empty(await IdsAsync(zed));
        Assert.Equal([setup.Card.Id, caraCard.Id, hanaCard.Id], await IdsAsync(hrOne));
        Assert.Equal([setup.Card.Id, caraCard.Id], await IdsAsync(hana));
        Assert.Equal([caraCard.Id], await IdsAsync(hrOne, $"&search={setup.Cara.EmployeeCode}"));
        Assert.Equal([setup.Card.Id, caraCard.Id, hanaCard.Id], await IdsAsync(hrOne, $"&periodId={setup.Period.Id}&departmentId={setup.Org.Department.Id}&status=Submitted"));
        Assert.Empty(await IdsAsync(hrOne, "&status=Approved"));

        var started = await OkAsync(await Step(anna, setup.Card.Id, "start-review", benSubmitted));
        Assert.Equal([setup.Card.Id], await IdsAsync(hrOne, "&status=Under%20Review"));
        var approved = await OkAsync(await Step(anna, setup.Card.Id, "approve", started));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.Equal([caraCard.Id], await IdsAsync(anna));
        Assert.Equal([setup.Card.Id], await IdsAsync(hrOne, "&status=Approved"));

        var page = await ReadAsync(await hrOne.GetAsync("/api/kpi/reviews/pending?pageIndex=0&pageSize=1"));
        Assert.Equal((3, 1, 0, 1), (page.GetProperty("total").GetInt32(), page.GetProperty("items").GetArrayLength(), page.GetProperty("pageIndex").GetInt32(), page.GetProperty("pageSize").GetInt32()));
        Assert.Equal(HttpStatusCode.BadRequest, (await hrOne.GetAsync("/api/kpi/reviews/pending?sort=rowVersion")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await hrOne.GetAsync($"/api/kpi/reviews/pending?search={new string('x', 101)}")).StatusCode);
    }
}
