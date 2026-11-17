using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Apex.Hrms.TestSupport;
using Microsoft.Extensions.Logging;
using static Apex.Hrms.Application.Tests.Fakes.ReviewFixtures;

namespace Apex.Hrms.Application.Tests.Performance;

/// <summary>
/// Evidence files on scorecard items: who may add, remove and download, when (status, period, row version), the limit per KPI, the
/// content checks, the write order and the rollback of the stored file, the activity line and what is logged.
/// </summary>
public sealed class ScorecardEvidenceTests : ScorecardWorkflowTestBase
{
    private TestHost host => Host;

    private static readonly byte[] Report = EvidenceSamples.Pdf(200);

    // ---- Adding ----

    [Fact]
    public async Task The_owner_adds_a_file_it_is_stored_under_a_random_key_and_the_scorecard_row_version_moves()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);

        var result = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "Q4 report.pdf", host.VersionOf(world.Card)));

        var row = Assert.Single(host.Repository<KpiAttachment>().Items);
        Assert.Equal((result.Attachment.Id, "Q4 report.pdf", "application/pdf", 200L, new DateOnly(2026, 11, 9)), (row.Id, row.FileName, row.ContentType, row.SizeBytes, row.UploadedOn));
        Assert.Equal(Of(world.Card, "A-1").Id, row.ScorecardItemId);
        Assert.True(KpiAttachment.IsValidStorageKey(row.StorageKey));
        Assert.Equal(Report, host.EvidenceStorage.Files[row.StorageKey]);
        Assert.NotEqual(world.Card.RowVersion, result.ScorecardRowVersion);
        Assert.Equal(Stored(world.Card.Id).RowVersion, result.ScorecardRowVersion);
        Assert.Equal(result.Attachment, new AttachmentDto(row.Id, row.FileName, row.ContentType, row.SizeBytes, row.UploadedOn));
    }

    [Fact]
    public async Task The_stored_content_type_is_the_detected_one_not_the_name_and_a_nameless_file_gets_a_generated_name()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var item = Of(world.Card, "A-1");

        var docx = ResultAssert.Succeeded(await host.UploadEvidence(item, EvidenceSamples.Docx(), @"C:\temp\..\Notes.DOCX", host.VersionOf(world.Card)));
        var png = ResultAssert.Succeeded(await host.UploadEvidence(item, EvidenceSamples.Png(), null, Requests.Version(docx.ScorecardRowVersion)));

        Assert.Equal(("Notes.DOCX", EvidenceFiles.DocxContentType), (docx.Attachment.FileName, docx.Attachment.ContentType));
        Assert.Equal(("evidence.png", "image/png"), (png.Attachment.FileName, png.Attachment.ContentType));
    }

    [Fact]
    public async Task The_scorecard_detail_lists_the_files_of_each_item_without_the_storage_key()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var first = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "one.pdf", host.VersionOf(world.Card)));
        ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), EvidenceSamples.Png(), "two.png", Requests.Version(first.ScorecardRowVersion)));

        var detail = ResultAssert.Succeeded(await host.Get<IGetByIdHandler<ScorecardDetailDto>>().HandleAsync(world.Card.Id, default));

        Assert.Equal(["one.pdf", "two.png"], Of(detail, "A-1").Attachments.Select(file => file.FileName));
        Assert.Empty(Of(detail, "A-2").Attachments);
        Assert.Equal(["Id", "FileName", "ContentType", "SizeBytes", "UploadedOn"], typeof(AttachmentDto).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task Adding_a_file_records_an_activity_line_with_the_employee_code_and_period_but_never_the_file_name()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);

        ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "salary-slip-ben.pdf", host.VersionOf(world.Card)));

        var entry = host.ActivityLog.Entries[^1];
        Assert.Equal(ActivityCategories.Performance, entry.Category);
        Assert.Equal($"Evidence added to the scorecard of EMP-0002 ({world.Plan.Period.Name})", entry.Message);
        Assert.DoesNotContain("salary", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_reviewer_or_hr_cannot_add_evidence_and_an_unrelated_employee_does_not_even_see_the_scorecard()
    {
        var world = await SeedAsync();
        var item = Of(world.Card, "A-1");
        var version = host.VersionOf(world.Card);

        AsManager(world.Anna, 10, world.Ben.Id);
        var manager = await host.UploadEvidence(item, Report, "a.pdf", version);
        AsHr(null, 1);
        var admin = await host.UploadEvidence(item, Report, "a.pdf", version);
        AsHr(world.Hr, 40);
        var hr = await host.UploadEvidence(item, Report, "a.pdf", version);
        AsEmployee(world.Dan, 30);
        var other = await host.UploadEvidence(item, Report, "a.pdf", version);

        ResultAssert.Failed(manager, ErrorType.Forbidden, "You can only add or remove evidence on your own scorecard.");
        ResultAssert.Failed(admin, ErrorType.Forbidden);
        ResultAssert.Failed(hr, ErrorType.Forbidden);
        ResultAssert.Failed(other, ErrorType.NotFound);
        Assert.Empty(host.Repository<KpiAttachment>().Items);
        Assert.Empty(host.EvidenceStorage.Files);
    }

    [Fact]
    public async Task An_item_that_does_not_exist_is_a_404()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);

        var result = await host.Get<IUploadEvidenceHandler>().HandleAsync(9999, new EvidenceUploadRequest(host.VersionOf(world.Card), "a.pdf", Report), default);

        ResultAssert.Failed(result, ErrorType.NotFound);
        Assert.Empty(host.EvidenceStorage.Files);
    }

    [Fact]
    public async Task The_row_version_is_required_and_a_stale_one_is_a_409_that_stores_nothing()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var item = Of(world.Card, "A-1");
        ResultAssert.Succeeded(await host.UploadEvidence(item, Report, "a.pdf", host.VersionOf(world.Card)));

        var missing = await host.UploadEvidence(item, Report, "b.pdf", rowVersion: null);
        var stale = await host.UploadEvidence(item, Report, "b.pdf", Requests.Version(world.Card.RowVersion));

        ResultAssert.FailedOnField(missing, "rowVersion");
        ResultAssert.Failed(stale, ErrorType.Conflict);
        Assert.Single(host.Repository<KpiAttachment>().Items);
        Assert.Single(host.EvidenceStorage.Files);
    }

    [Fact]
    public async Task Evidence_can_only_be_added_while_the_scorecard_is_draft_or_returned()
    {
        var world = await SeedAsync();
        var submitted = await SubmittedAsync(world);
        AsEmployee(world.Ben, 20);

        var whileSubmitted = await host.UploadEvidence(Of(submitted, "A-1"), Report, "a.pdf", host.VersionOf(submitted));

        ResultAssert.Failed(whileSubmitted, ErrorType.Conflict);
        AsManager(world.Anna, 10, world.Ben.Id);
        var returned = ResultAssert.Succeeded(await host.Return(host.Fresh(submitted), "Add proof"));
        AsEmployee(world.Ben, 20);
        var whileReturned = await host.UploadEvidence(Of(returned, "A-1"), Report, "a.pdf", host.VersionOf(returned));

        Assert.True(whileReturned.IsSuccess);
        Assert.Single(host.EvidenceStorage.Files);
    }

    [Fact]
    public async Task Evidence_can_only_be_added_while_the_period_is_active()
    {
        var world = await SeedAsync();
        await MovePeriodAsync(world, close: false);
        AsEmployee(world.Ben, 20);

        var result = await host.UploadEvidence(Of(world.Card, "A-1"), Report, "a.pdf", host.VersionOf(world.Card));

        var error = ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Contains("Active", error.Message, StringComparison.Ordinal);
        Assert.Empty(host.EvidenceStorage.Files);
    }

    [Fact]
    public async Task A_KPI_takes_at_most_five_files_and_the_sixth_is_a_409_while_another_KPI_still_has_room()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var first = Of(world.Card, "A-1");
        var version = host.VersionOf(world.Card);
        for (var count = 1; count <= EvidenceFiles.MaxPerItem; count++)
        {
            version = Requests.Version(ResultAssert.Succeeded(await host.UploadEvidence(first, Report, $"f{count}.pdf", version)).ScorecardRowVersion);
        }

        var sixth = await host.UploadEvidence(first, Report, "f6.pdf", version);
        var other = await host.UploadEvidence(Of(world.Card, "A-2"), Report, "g1.pdf", version);

        ResultAssert.Failed(sixth, ErrorType.Conflict, UploadEvidenceHandlerMessages.TooMany);
        Assert.True(other.IsSuccess);
        Assert.Equal(6, host.Repository<KpiAttachment>().Items.Count);
        Assert.Equal(6, host.EvidenceStorage.Files.Count);
    }

    [Fact]
    public async Task A_file_that_is_not_what_its_name_says_is_a_400_on_file_and_nothing_is_stored()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var item = Of(world.Card, "A-1");
        var version = host.VersionOf(world.Card);

        ResultAssert.FailedOnField(await host.UploadEvidence(item, EvidenceSamples.Executable(), "report.pdf", version), "file", EvidenceFiles.NotAllowedMessage);
        ResultAssert.FailedOnField(await host.UploadEvidence(item, EvidenceSamples.Html(), "page.pdf", version), "file");
        ResultAssert.FailedOnField(await host.UploadEvidence(item, EvidenceSamples.Pdf(), "report.png", version), "file");
        ResultAssert.FailedOnField(await host.UploadEvidence(item, [], "empty.pdf", version), "file");
        ResultAssert.FailedOnField(await host.UploadEvidence(item, EvidenceSamples.Pdf((int)EvidenceFiles.MaxBytes + 1), "big.pdf", version), "file");
        Assert.Empty(host.Repository<KpiAttachment>().Items);
        Assert.Empty(host.EvidenceStorage.Files);
        Assert.Equal(version, host.VersionOf(world.Card));
    }

    [Fact]
    public async Task When_the_database_save_fails_the_stored_file_is_deleted_again()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("The record was changed or deleted by someone else. Reload it and try again.");

        var result = await host.UploadEvidence(Of(world.Card, "A-1"), Report, "a.pdf", host.VersionOf(world.Card));

        ResultAssert.Failed(result, ErrorType.Conflict);
        Assert.Empty(host.EvidenceStorage.Files);
        var removed = Assert.Single(host.EvidenceStorage.Deleted);
        Assert.True(KpiAttachment.IsValidStorageKey(removed));
        Assert.DoesNotContain(host.ActivityLog.Entries, entry => entry.Message.StartsWith("Evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_cleanup_is_logged_without_the_file_name_and_the_original_failure_is_still_reported()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("stale");
        host.EvidenceStorage.FailDeleteWith = new IOException("locked");

        var result = await host.UploadEvidence(Of(world.Card, "A-1"), Report, "secret-name.pdf", host.VersionOf(world.Card));

        ResultAssert.Failed(result, ErrorType.Conflict);
        var warning = Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain("secret-name", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_the_storage_cannot_write_no_row_is_added()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        host.EvidenceStorage.FailSaveWith = new IOException("disk full");

        await Assert.ThrowsAsync<IOException>(() => host.UploadEvidence(Of(world.Card, "A-1"), Report, "a.pdf", host.VersionOf(world.Card)));

        Assert.Empty(host.Repository<KpiAttachment>().Items);
        Assert.Equal(world.Card.RowVersion, host.Fresh(world.Card).RowVersion);
    }

    // ---- Removing ----

    [Fact]
    public async Task The_owner_removes_a_file_the_row_and_the_file_go_and_the_scorecard_row_version_moves()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var added = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "mine.pdf", host.VersionOf(world.Card)));
        var key = Assert.Single(host.Repository<KpiAttachment>().Items).StorageKey;

        var removed = ResultAssert.Succeeded(await host.RemoveEvidence(added.Attachment.Id, Requests.Version(added.ScorecardRowVersion)));

        Assert.Empty(host.Repository<KpiAttachment>().Items);
        Assert.Empty(host.EvidenceStorage.Files);
        Assert.Equal([key], host.EvidenceStorage.Deleted);
        Assert.NotEqual(added.ScorecardRowVersion, removed.ScorecardRowVersion);
        Assert.Equal(Stored(world.Card.Id).RowVersion, removed.ScorecardRowVersion);
        var entry = host.ActivityLog.Entries[^1];
        Assert.Equal((ActivityCategories.Performance, $"Evidence removed from the scorecard of EMP-0002 ({world.Plan.Period.Name})"), (entry.Category, entry.Message));
    }

    [Fact]
    public async Task Only_the_owner_removes_evidence_and_it_is_refused_with_a_stale_version_or_once_submitted()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var added = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "mine.pdf", host.VersionOf(world.Card)));
        var version = Requests.Version(added.ScorecardRowVersion);

        AsManager(world.Anna, 10, world.Ben.Id);
        var manager = await host.RemoveEvidence(added.Attachment.Id, version);
        AsHr(null, 1);
        var admin = await host.RemoveEvidence(added.Attachment.Id, version);
        AsEmployee(world.Dan, 30);
        var other = await host.RemoveEvidence(added.Attachment.Id, version);
        AsEmployee(world.Ben, 20);
        var stale = await host.RemoveEvidence(added.Attachment.Id, Requests.Version(world.Card.RowVersion));
        var missing = await host.RemoveEvidence(added.Attachment.Id, null);
        var unknown = await host.RemoveEvidence(9999, version);

        ResultAssert.Failed(manager, ErrorType.Forbidden);
        ResultAssert.Failed(admin, ErrorType.Forbidden);
        ResultAssert.Failed(other, ErrorType.NotFound);
        ResultAssert.Failed(stale, ErrorType.Conflict);
        ResultAssert.FailedOnField(missing, "rowVersion");
        ResultAssert.Failed(unknown, ErrorType.NotFound);
        Assert.Single(host.Repository<KpiAttachment>().Items);
        Assert.Single(host.EvidenceStorage.Files);

        var filled = ResultAssert.Succeeded(await host.SaveResults(host.Fresh(world.Card), Actual(Of(world.Card, "A-1"), 100m), Actual(Of(world.Card, "A-2"), 50m)));
        var submitted = ResultAssert.Succeeded(await host.Submit(filled));
        var afterSubmit = await host.RemoveEvidence(added.Attachment.Id, host.VersionOf(submitted));
        ResultAssert.Failed(afterSubmit, ErrorType.Conflict);
        Assert.Single(host.EvidenceStorage.Files);
    }

    [Fact]
    public async Task A_file_that_cannot_be_deleted_from_storage_is_logged_by_id_and_does_not_fail_the_removal()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var added = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "private-name.pdf", host.VersionOf(world.Card)));
        host.EvidenceStorage.FailDeleteWith = new UnauthorizedAccessException("denied");

        var removed = await host.RemoveEvidence(added.Attachment.Id, Requests.Version(added.ScorecardRowVersion));

        Assert.True(removed.IsSuccess);
        Assert.Empty(host.Repository<KpiAttachment>().Items);
        var warning = Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(added.Attachment.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-name", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task When_removing_fails_to_save_the_file_stays()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var added = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "a.pdf", host.VersionOf(world.Card)));
        host.UnitOfWork.FailNextSaveWith = AppError.Conflict("stale");

        var removed = await host.RemoveEvidence(added.Attachment.Id, Requests.Version(added.ScorecardRowVersion));

        ResultAssert.Failed(removed, ErrorType.Conflict);
        Assert.Single(host.EvidenceStorage.Files);
        Assert.Empty(host.EvidenceStorage.Deleted);
    }

    // ---- Downloading ----

    [Fact]
    public async Task The_owner_the_reviewer_and_hr_download_the_same_bytes_and_an_unrelated_employee_gets_a_404()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var added = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), EvidenceSamples.Docx(), "plan.docx", host.VersionOf(world.Card)));
        var id = added.Attachment.Id;

        var owner = ResultAssert.Succeeded(await host.DownloadEvidence(id));
        AsManager(world.Anna, 10, world.Ben.Id);
        var reviewer = ResultAssert.Succeeded(await host.DownloadEvidence(id));
        AsHr(null, 1);
        var admin = ResultAssert.Succeeded(await host.DownloadEvidence(id));
        AsEmployee(world.Dan, 30);
        var other = await host.DownloadEvidence(id);

        Assert.Equal(("plan.docx", EvidenceFiles.DocxContentType), (owner.FileName, owner.ContentType));
        var expected = EvidenceSamples.Docx();
        Assert.Equal(expected, await owner.ReadAllAsync());
        Assert.Equal(expected, await reviewer.ReadAllAsync());
        Assert.Equal(expected, await admin.ReadAllAsync());
        ResultAssert.Failed(other, ErrorType.NotFound);
    }

    [Fact]
    public async Task A_manager_of_someone_else_cannot_download()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var added = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "a.pdf", host.VersionOf(world.Card)));
        AsManager(world.Dan, 30, world.Hr.Id);

        var result = await host.DownloadEvidence(added.Attachment.Id);

        ResultAssert.Failed(result, ErrorType.NotFound);
    }

    [Fact]
    public async Task A_file_missing_from_storage_is_a_404_and_a_warning_with_the_id_only()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);
        var added = ResultAssert.Succeeded(await host.UploadEvidence(Of(world.Card, "A-1"), Report, "hidden-name.pdf", host.VersionOf(world.Card)));
        var key = host.Repository<KpiAttachment>().Items.Single().StorageKey;
        host.EvidenceStorage.Files.Clear();

        var result = await host.DownloadEvidence(added.Attachment.Id);

        ResultAssert.Failed(result, ErrorType.NotFound);
        var warning = Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(added.Attachment.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden-name", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(key, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_attachment_that_does_not_exist_is_a_404()
    {
        var world = await SeedAsync();
        AsEmployee(world.Ben, 20);

        ResultAssert.Failed(await host.DownloadEvidence(9999), ErrorType.NotFound);
    }

    /// <summary>The wording the handler answers with when a KPI already has the most files.</summary>
    private static class UploadEvidenceHandlerMessages
    {
        public const string TooMany = "A KPI can have at most 5 evidence files. Remove one before adding another.";
    }
}
