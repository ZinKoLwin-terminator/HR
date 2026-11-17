using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Apex.Hrms.Api.Auth;
using Apex.Hrms.Api.Http;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Apex.Hrms.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Api.Tests;

/// <summary>
/// The upload route through the real pipeline with the application handler replaced by a recorder (no database): the antiforgery header
/// is required before a byte of the body is read, the request size is capped just above the file limit however the body arrives, the
/// multipart body is read into the request the handler receives, and a file over the limit is a 400 on <c>file</c>.
/// </summary>
public sealed class EvidenceUploadHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Url = "/api/kpi/scorecard-items/5/attachments";

    private static readonly byte[] SmallPdf = [.. "%PDF-1.4\n%fictional evidence\n"u8];

    private readonly RecordingUploadHandler handler = new();
    private readonly WebApplicationFactory<Program> host;

    public EvidenceUploadHttpTests(WebApplicationFactory<Program> factory) =>
        host = SignedInHost.Customize(factory, AccessFakes.Account("Employee"), ["kpi:View", "kpi:Create"])
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IUploadEvidenceHandler>(handler)));

    [Fact]
    public async Task A_multipart_upload_reaches_the_handler_with_the_row_version_the_file_name_and_the_exact_bytes()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);

        var response = await client.PostAsync(Url, Form(SmallPdf, "Q4 report.pdf", "AAAAAAAAAAE="));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/kpi/attachments/7", response.Headers.Location?.OriginalString);
        var call = Assert.Single(handler.Calls);
        Assert.Equal((5, "AAAAAAAAAAE=", "Q4 report.pdf"), (call.ItemId, call.Request.RowVersion, call.Request.FileName));
        Assert.Equal(SmallPdf, call.Request.Content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, body.GetProperty("attachment").GetProperty("id").GetInt32());
        Assert.False(body.GetProperty("attachment").TryGetProperty("storageKey", out _));
        Assert.True(body.TryGetProperty("scorecardRowVersion", out _));
    }

    [Fact]
    public async Task An_upload_without_the_antiforgery_header_is_refused_before_its_body_is_read()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _, sendXsrfHeader: false);

        var response = await client.PostAsync(Url, Form(SmallPdf, "a.pdf", "AAAAAAAAAAE="));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AntiforgeryMiddleware.RejectedMessage, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task A_request_declared_larger_than_the_limit_is_refused_with_413_and_never_reaches_the_handler()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
        var tooBig = new byte[(int)EvidenceUploadReader.MaxRequestBytes + 1];

        var response = await client.PostAsync(Url, Form(tooBig, "big.pdf", "AAAAAAAAAAE="));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task A_streamed_body_with_no_declared_length_is_cut_off_at_the_limit()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
        var content = new UnknownLengthContent((int)EvidenceUploadReader.MaxRequestBytes + 4096);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=\"limit-test\"");

        var response = await client.PostAsync(Url, content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task A_file_over_ten_megabytes_inside_the_request_limit_is_a_400_on_file()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
        var file = new byte[(int)EvidenceFiles.MaxBytes + 1];

        var response = await client.PostAsync(Url, Form(file, "big.pdf", "AAAAAAAAAAE="));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(EvidenceFiles.TooLargeMessage, FieldMessage(await response.Content.ReadFromJsonAsync<JsonElement>(), "file"));
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task A_file_of_exactly_ten_megabytes_is_passed_on()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
        var file = new byte[(int)EvidenceFiles.MaxBytes];

        var response = await client.PostAsync(Url, Form(file, "exact.pdf", "AAAAAAAAAAE="));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(file.Length, Assert.Single(handler.Calls).Request.Content.Length);
    }

    [Fact]
    public async Task A_body_that_is_not_multipart_form_data_is_a_400_on_file()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);

        var response = await client.PostAsJsonAsync(Url, new { rowVersion = "AAAAAAAAAAE=" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(FieldMessage(await response.Content.ReadFromJsonAsync<JsonElement>(), "file"));
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Two_files_or_a_misnamed_file_field_are_a_400_on_file()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
        using var two = Form(SmallPdf, "a.pdf", "AAAAAAAAAAE=");
        two.Add(new ByteArrayContent(SmallPdf), "file", "b.pdf");
        using var misnamed = new MultipartFormDataContent { { new ByteArrayContent(SmallPdf), "document", "a.pdf" } };

        var twoFiles = await client.PostAsync(Url, two);
        var wrongField = await client.PostAsync(Url, misnamed);

        Assert.Equal(HttpStatusCode.BadRequest, twoFiles.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongField.StatusCode);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task A_missing_row_version_is_left_to_the_handler_to_refuse()
    {
        using var client = BrowserHandler.CreateClient(host.Server.CreateHandler(), out _);
        using var form = new MultipartFormDataContent { { new ByteArrayContent(SmallPdf), "file", "a.pdf" } };

        var response = await client.PostAsync(Url, form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(Assert.Single(handler.Calls).Request.RowVersion);
    }

    private static MultipartFormDataContent Form(byte[] file, string fileName, string rowVersion) => new()
    {
        { new StringContent(rowVersion), "rowVersion" },
        { new ByteArrayContent(file), "file", fileName },
    };

    private static string? FieldMessage(JsonElement problem, string field) =>
        problem.GetProperty("errors").GetProperty(field)[0].GetString();

    /// <summary>A body whose length the client does not declare, so it is sent chunked.</summary>
    private sealed class UnknownLengthContent(int length) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            await stream.WriteAsync("--limit-test\r\nContent-Disposition: form-data; name=\"padding\"\r\n\r\n"u8.ToArray());
            var chunk = new byte[8192];
            for (var sent = 0; sent < length; sent += chunk.Length)
            {
                await stream.WriteAsync(chunk.AsMemory(0, Math.Min(chunk.Length, length - sent)));
            }
        }

        protected override bool TryComputeLength(out long computed)
        {
            computed = 0;
            return false;
        }
    }

    private sealed class RecordingUploadHandler : IUploadEvidenceHandler
    {
        public List<(int ItemId, EvidenceUploadRequest Request)> Calls { get; } = [];

        public Task<Result<EvidenceUploadResult>> HandleAsync(int itemId, EvidenceUploadRequest request, CancellationToken cancellationToken)
        {
            Calls.Add((itemId, request));
            return Task.FromResult(Result.Success(new EvidenceUploadResult(
                new AttachmentDto(7, "evidence.pdf", "application/pdf", request.Content.Length, new DateOnly(2026, 11, 9)),
                [0, 0, 0, 0, 0, 0, 0, 2])));
        }
    }
}
