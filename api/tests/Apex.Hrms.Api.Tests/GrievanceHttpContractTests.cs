using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The grievance routes through the real pipeline with the application services replaced by recorders (no database): the shared-tier
/// policies, the upload checks before the body is read, the multipart fields, the size limit, the rate limit and the 204 of markSeen.
/// </summary>
public sealed class GrievanceHttpContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Version = "AAAAAAAAAAE=";
    private static readonly byte[] SmallPdf = [.. "%PDF-1.4\n%fictional grievance file\n"u8];

    private readonly RecordingMyService my = new();
    private readonly RecordingCaseWork work = new();
    private readonly WebApplicationFactory<Program> factory;

    public GrievanceHttpContractTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    private WebApplicationFactory<Program> Host(params string[] grants) =>
        SignedInHost.Customize(factory, AccessFakes.Account("Employee"), grants)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton<IMyGrievanceService>(my);
                services.AddSingleton<IGrievanceCaseWork>(work);
            }));

    private static HttpClient Client(WebApplicationFactory<Program> host) => BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);

    [Fact]
    public async Task Recording_a_grievance_for_an_employee_needs_management_create()
    {
        using var host = Host("grievance-management:View", "grievance-management:Edit");
        using var client = Client(host);

        var response = await client.PostAsJsonAsync("/api/grievances/cases", new { employeeId = 5, categoryId = 1, receivedVia = "InPerson", declarationAccepted = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_submitters_upload_reaches_the_service_with_the_row_version_the_name_and_the_exact_bytes()
    {
        using var host = Host("grievance:View", "grievance:Create");
        using var client = Client(host);

        var response = await client.PostAsync("/api/grievances/my/5/attachments", Form(SmallPdf, "rota.pdf", Version));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/grievances/my/attachments/7", response.Headers.Location?.OriginalString);
        var call = Assert.Single(my.Uploads);
        Assert.Equal((5, Version, "rota.pdf"), (call.Id, call.Upload.RowVersion, call.Upload.FileName));
        Assert.Equal(SmallPdf, call.Upload.Content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("You", body.GetProperty("attachment").GetProperty("uploadedBy").GetString());
        Assert.True(body.TryGetProperty("caseRowVersion", out _));
        Assert.False(body.GetProperty("attachment").TryGetProperty("storageKey", out _));
    }

    [Fact]
    public async Task A_refused_precheck_answers_before_the_body_is_read()
    {
        using var host = Host("grievance:View", "grievance:Create");
        using var client = Client(host);
        my.Refusal = AppError.NotFound("Grievance case #5 was not found.");

        var response = await client.PostAsync("/api/grievances/my/5/attachments", Form(SmallPdf, "rota.pdf", Version));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(my.Uploads);
        Assert.Single(my.Prechecks);
    }

    [Fact]
    public async Task A_case_team_upload_carries_its_visibility_and_needs_management_or_investigation_edit()
    {
        using var investigator = Host("grievance-investigation:View", "grievance-investigation:Edit");
        using var client = Client(investigator);
        using var form = Form(SmallPdf, "letter.pdf", Version);
        form.Add(new StringContent("CaseTeam"), "visibility");

        var response = await client.PostAsync("/api/grievances/cases/9/attachments", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/grievances/attachments/8", response.Headers.Location?.OriginalString);
        Assert.Equal((9, "CaseTeam"), (work.Uploads.Single().Id, work.Uploads.Single().Visibility));

        using var reviewer = Host("grievance-review:View", "grievance-review:Approve");
        using var refusedClient = Client(reviewer);
        var refused = await refusedClient.PostAsync("/api/grievances/cases/9/attachments", Form(SmallPdf, "letter.pdf", Version));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Single(work.Uploads);
    }

    [Fact]
    public async Task A_file_over_ten_megabytes_is_a_400_on_file_and_never_reaches_the_service()
    {
        using var host = Host("grievance:View", "grievance:Create");
        using var client = Client(host);

        var response = await client.PostAsync("/api/grievances/my/5/attachments", Form(new byte[(int)EvidenceFiles.MaxBytes + 1], "big.pdf", Version));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(EvidenceFiles.TooLargeMessage, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("file")[0].GetString());
        Assert.Empty(my.Uploads);
    }

    [Fact]
    public async Task The_eleventh_grievance_upload_within_a_minute_is_a_429()
    {
        using var host = Host("grievance:View", "grievance:Create");
        using var client = Client(host);

        for (var attempt = 0; attempt < GrievanceUploadRateLimit.DefaultPermitsPerMinute; attempt++)
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsync("/api/grievances/my/5/attachments", Form(SmallPdf, "a.pdf", Version))).StatusCode);
        }

        var refused = await client.PostAsync("/api/grievances/my/5/attachments", Form(SmallPdf, "a.pdf", Version));

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(GrievanceUploadRateLimit.RejectedMessage, (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Marking_seen_is_a_204_without_a_body_and_needs_only_the_grievance_view()
    {
        using var host = Host("grievance:View");
        using var client = Client(host);

        var response = await client.PostAsync("/api/grievances/my/5/seen", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([5], my.Seen);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Writing_a_grievance_needs_grievance_create()
    {
        using var host = Host("grievance:View");
        using var client = Client(host);

        var response = await client.PostAsJsonAsync("/api/grievances/my", new { categoryId = 1, subject = "TST subject", isAnonymous = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(my.Drafts);
    }

    [Theory]
    [InlineData("grievance-management:View")]
    [InlineData("grievance-investigation:View")]
    [InlineData("grievance-review:View")]
    public async Task Any_one_case_team_view_passes_the_shared_case_policy_and_the_scope_decides(string grant)
    {
        using var host = SignedInHost.Customize(factory, AccessFakes.Account("Employee"), [grant])
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IGrievanceCaseReader>(new NotFoundReader())));
        using var client = Client(host);

        var response = await client.GetAsync("/api/grievances/cases/3");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_caller_without_any_case_team_view_is_refused_by_the_shared_policy()
    {
        using var host = SignedInHost.Customize(factory, AccessFakes.Account("Employee"), ["grievance:View", "grievance:Create"])
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IGrievanceCaseReader>(new NotFoundReader())));
        using var client = Client(host);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/grievances/cases/3")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/grievances/reports/summary")).StatusCode);
    }

    [Fact]
    public async Task Triage_without_the_legal_hold_answer_is_a_400_on_legal_hold()
    {
        // The real handler: the validator answers before the case is loaded, so no database is needed.
        using var host = SignedInHost.Customize(factory, AccessFakes.Account("Employee"), ["grievance-management:View", "grievance-management:Edit"]);
        using var client = Client(host);

        var response = await client.PutAsJsonAsync(
            "/api/grievances/cases/3/triage",
            new { rowVersion = Version, categoryId = 1, priority = "Medium", targetResolutionDate = "2026-12-31", subjectEmployeeId = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Say whether the case is on legal hold.", errors.GetProperty("legalHold")[0].GetString());
    }

    private static MultipartFormDataContent Form(byte[] file, string fileName, string rowVersion) => new()
    {
        { new StringContent(rowVersion), "rowVersion" },
        { new ByteArrayContent(file), "file", fileName },
    };

    private sealed class RecordingMyService : IMyGrievanceService
    {
        public List<(int Id, GrievanceFileUpload Upload)> Uploads { get; } = [];

        public List<int> Prechecks { get; } = [];

        public List<int> Seen { get; } = [];

        public List<GrievanceDraftCreateRequest> Drafts { get; } = [];

        public AppError? Refusal { get; set; }

        public Task<Result> PrecheckUploadAsync(int id, CancellationToken cancellationToken)
        {
            Prechecks.Add(id);
            return Task.FromResult(Refusal is { } refusal ? Result.Failure(refusal) : Result.Success());
        }

        public Task<Result<MyGrievanceAttachmentUploadResult>> UploadAttachmentAsync(int id, GrievanceFileUpload upload, CancellationToken cancellationToken)
        {
            Uploads.Add((id, upload));
            return Task.FromResult(Result.Success(new MyGrievanceAttachmentUploadResult(
                new MyGrievanceAttachmentDto(7, "rota.pdf", "application/pdf", upload.Content.Length, "You", new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc)),
                [0, 0, 0, 0, 0, 0, 0, 1])));
        }

        public Task<Result> MarkSeenAsync(int id, CancellationToken cancellationToken)
        {
            Seen.Add(id);
            return Task.FromResult(Result.Success());
        }

        public Task<Result<MyGrievanceDetailDto>> CreateDraftAsync(GrievanceDraftCreateRequest request, CancellationToken cancellationToken)
        {
            Drafts.Add(request);
            return Task.FromResult(Result.Failure<MyGrievanceDetailDto>(AppError.Validation("Not used.")));
        }

        public Task<Result<GrievanceOptionsDto>> OptionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<PagedResult<MyGrievanceDto>>> QueryAsync(PageRequest<MyGrievanceFilter> request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<MyGrievanceDetailDto>> GetAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<MyGrievanceDashboardDto>> DashboardAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<EvidenceDownload>> DownloadAttachmentAsync(int attachmentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<MyGrievanceDetailDto>> UpdateDraftAsync(int id, GrievanceDraftUpdateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> DeleteDraftAsync(int id, string? rowVersion, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<MyGrievanceDetailDto>> SubmitAsync(int id, SubmitGrievanceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<MyGrievanceDetailDto>> SendMessageAsync(int id, MyGrievanceMessageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingCaseWork : IGrievanceCaseWork
    {
        public List<(int Id, string? Visibility, GrievanceFileUpload Upload)> Uploads { get; } = [];

        public Task<Result> PrecheckUploadAsync(int id, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result<GrievanceAttachmentUploadResult>> UploadAttachmentAsync(int id, string? visibility, GrievanceFileUpload upload, CancellationToken cancellationToken)
        {
            Uploads.Add((id, visibility, upload));
            return Task.FromResult(Result.Success(new GrievanceAttachmentUploadResult(
                new GrievanceAttachmentDto(8, "letter.pdf", "application/pdf", upload.Content.Length, visibility ?? "Shared", "Investigator", new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc), null, null),
                [0, 0, 0, 0, 0, 0, 0, 2])));
        }

        public Task<Result<GrievanceCorrectiveActionDto>> AddCorrectiveActionAsync(int id, GrievanceCorrectiveActionCreateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<GrievanceCorrectiveActionDto>> UpdateCorrectiveActionAsync(int actionId, GrievanceCorrectiveActionUpdateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NotFoundReader : IGrievanceCaseReader
    {
        private static Task<Result<T>> Missing<T>() => Task.FromResult(Result.Failure<T>(AppError.NotFound("Grievance case #3 was not found.")));

        public Task<Result<GrievanceCaseDetailDto>> GetAsync(int id, CancellationToken cancellationToken) => Missing<GrievanceCaseDetailDto>();

        public Task<Result<PagedResult<GrievanceCaseDto>>> QueryAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken) => Missing<PagedResult<GrievanceCaseDto>>();

        public Task<Result<PagedResult<GrievanceCaseDto>>> InvestigationsAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken) => Missing<PagedResult<GrievanceCaseDto>>();

        public Task<Result<PagedResult<GrievanceCaseDto>>> PendingReviewsAsync(PageRequest<GrievanceCaseFilter> request, CancellationToken cancellationToken) => Missing<PagedResult<GrievanceCaseDto>>();

        public Task<Result<IReadOnlyList<GrievanceHistoryEntryDto>>> HistoryAsync(int id, CancellationToken cancellationToken) => Missing<IReadOnlyList<GrievanceHistoryEntryDto>>();

        public Task<Result<PagedResult<GrievanceAuditEntryDto>>> AuditAsync(int id, PageRequest<GrievanceAuditFilter> request, CancellationToken cancellationToken) => Missing<PagedResult<GrievanceAuditEntryDto>>();

        public Task<Result<PagedResult<GrievanceAuditEntryDto>>> AuditLogAsync(PageRequest<GrievanceAuditFilter> request, CancellationToken cancellationToken) => Missing<PagedResult<GrievanceAuditEntryDto>>();

        public Task<Result<IReadOnlyList<GrievanceInvestigatorOptionDto>>> InvestigatorsAsync(string? search, CancellationToken cancellationToken) => Missing<IReadOnlyList<GrievanceInvestigatorOptionDto>>();

        public Task<Result<IReadOnlyList<GrievanceEmployeeOptionDto>>> EmployeeOptionsAsync(string? search, CancellationToken cancellationToken) => Missing<IReadOnlyList<GrievanceEmployeeOptionDto>>();

        public Task<Result<EvidenceDownload>> DownloadAttachmentAsync(int attachmentId, CancellationToken cancellationToken) => Missing<EvidenceDownload>();
    }
}
