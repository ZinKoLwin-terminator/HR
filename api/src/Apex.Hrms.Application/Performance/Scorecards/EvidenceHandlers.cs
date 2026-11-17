using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Microsoft.Extensions.Logging;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// The employee adds an evidence file to a KPI of their own scorecard. Only the scorecard's own employee (a reviewer or HR gets 403, a
/// scorecard outside the caller's scope is a 404), only while the scorecard is Draft or Returned and its period Active (or Completed, for a Returned one), with the
/// scorecard's current row version, at most 5 files per KPI. The content is checked by what it is (<see cref="EvidenceFiles"/>), never by
/// what the client says. The file is stored first under a new random key, then the metadata row is saved together with the scorecard's new
/// row version and the activity line; if that save fails the stored file is deleted again, so no file is left without a row.
/// </summary>
internal sealed partial class UploadEvidenceHandler(
    ScorecardSteps steps,
    IRepository<ScorecardItem> items,
    IRepository<KpiAttachment> attachments,
    IReadModel read,
    IEvidenceStorage storage,
    ILogger<UploadEvidenceHandler> logger) : IUploadEvidenceHandler
{
    public const string TooMany = "A KPI can have at most 5 evidence files. Remove one before adding another.";

    /// <summary>The one 404 of the upload route, whether the item does not exist or its scorecard is outside the caller's scope.</summary>
    public static AppError ItemNotFound(int itemId) => NotFoundError.For("KPI item", itemId);

    public async Task<Result> PrecheckAsync(int itemId, CancellationToken cancellationToken)
    {
        var scorecardId = await read.FirstOrDefaultAsync(
            read.Query<ScorecardItem>().Where(item => item.Id == itemId).Select(item => (int?)item.ScorecardId),
            cancellationToken);
        return scorecardId is { } id
            ? await steps.PrecheckEvidenceAsync(id, ItemNotFound(itemId), cancellationToken)
            : Result.Failure(ItemNotFound(itemId));
    }

    public async Task<Result<EvidenceUploadResult>> HandleAsync(int itemId, EvidenceUploadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var file = EvidenceFiles.Inspect(request.FileName, request.Content);
        if (file.IsFailure)
        {
            return Result.Failure<EvidenceUploadResult>(file.Error);
        }

        var item = await items.FindAsync(itemId, cancellationToken);
        if (item is null)
        {
            return Result.Failure<EvidenceUploadResult>(ItemNotFound(itemId));
        }

        var opened = await steps.OpenForEvidenceAsync(item.ScorecardId, request.RowVersion, ItemNotFound(itemId), cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<EvidenceUploadResult>(opened.Error);
        }

        var (scorecard, period) = opened.Value;
        var existing = await read.CountAsync(read.Query<KpiAttachment>().Where(attachment => attachment.ScorecardItemId == itemId), cancellationToken);
        if (existing >= EvidenceFiles.MaxPerItem)
        {
            return Result.Failure<EvidenceUploadResult>(AppError.Conflict(TooMany));
        }

        var key = IEvidenceStorage.NewKey();
        var created = KpiAttachment.Create(itemId, file.Value.FileName, file.Value.ContentType, request.Content.LongLength, key, steps.Today);
        if (created.IsFailure)
        {
            return Result.Failure<EvidenceUploadResult>(created.Error);
        }

        var committed = false;
        try
        {
            await storage.SaveAsync(key, request.Content, cancellationToken);
            attachments.Add(created.Value);
            var saved = await steps.CommitAsync(scorecard, period, entry: null, ScorecardActivity.EvidenceAdded, cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<EvidenceUploadResult>(saved.Error);
            }

            committed = true;
            return Result.Success(new EvidenceUploadResult(
                ScorecardMapping.ToDto(created.Value),
                await steps.RowVersionOfAsync(scorecard.Id, cancellationToken)));
        }
        finally
        {
            if (!committed)
            {
                await DiscardAsync(key, itemId);
            }
        }
    }

    /// <summary>Removes a stored file whose row was not saved. Never throws: the original failure is the one to report.</summary>
    private async Task DiscardAsync(string key, int itemId)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogDiscardFailed(itemId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "An evidence file of scorecard item {ItemId} could not be removed after its record failed to save.")]
    private partial void LogDiscardFailed(int itemId);
}

/// <summary>
/// The employee removes an evidence file (<c>DELETE /api/kpi/attachments/{id}</c>): the same rules as adding one. The row is deleted
/// (with the scorecard's new row version and the activity line) first, then the file; a file that cannot be deleted is logged by the
/// attachment id and does not fail the request.
/// </summary>
internal sealed partial class RemoveEvidenceHandler(
    ScorecardSteps steps,
    IRepository<ScorecardItem> items,
    IRepository<KpiAttachment> attachments,
    IEvidenceStorage storage,
    ILogger<RemoveEvidenceHandler> logger) : IRemoveEvidenceHandler
{
    public async Task<Result<EvidenceRemovalResult>> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken)
    {
        var notFound = NotFoundError.For("Attachment", id);
        var attachment = await attachments.FindAsync(id, cancellationToken);
        var item = attachment is null ? null : await items.FindAsync(attachment.ScorecardItemId, cancellationToken);
        if (attachment is null || item is null)
        {
            return Result.Failure<EvidenceRemovalResult>(notFound);
        }

        var opened = await steps.OpenForEvidenceAsync(item.ScorecardId, rowVersion, notFound, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<EvidenceRemovalResult>(opened.Error);
        }

        var (scorecard, period) = opened.Value;
        var key = attachment.StorageKey;
        attachments.Remove(attachment);
        var saved = await steps.CommitAsync(scorecard, period, entry: null, ScorecardActivity.EvidenceRemoved, cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<EvidenceRemovalResult>(saved.Error);
        }

        await DeleteFileAsync(id, key);
        return Result.Success(new EvidenceRemovalResult(await steps.RowVersionOfAsync(scorecard.Id, cancellationToken)));
    }

    private async Task DeleteFileAsync(int id, string key)
    {
        try
        {
            if (KpiAttachment.IsValidStorageKey(key))
            {
                await storage.DeleteAsync(key, CancellationToken.None);
            }
            else
            {
                LogFileNotRemoved(id);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogFileNotRemoved(id);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The file of the removed evidence attachment {AttachmentId} could not be deleted from storage.")]
    private partial void LogFileNotRemoved(int attachmentId);
}

/// <summary>
/// Opens an evidence file for download. It is visible to everyone who can see the scorecard (its employee, their reviewer and manager, HR:
/// the scorecard scope of the caller); a file outside that scope is a 404, as is a record whose file is missing from storage (logged by
/// attachment id only). The content type is the one detected when the file was added.
/// </summary>
internal sealed partial class DownloadEvidenceHandler(
    ScorecardReader reader,
    IReadModel read,
    IEvidenceStorage storage,
    ILogger<DownloadEvidenceHandler> logger) : IDownloadEvidenceHandler
{
    public async Task<Result<EvidenceDownload>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var row = await read.FirstOrDefaultAsync(
            from attachment in read.Query<KpiAttachment>().Where(attachment => attachment.Id == id)
            join item in read.Query<ScorecardItem>() on attachment.ScorecardItemId equals item.Id
            join scorecard in reader.Scoped() on item.ScorecardId equals scorecard.Id
            select new { attachment.FileName, attachment.ContentType, attachment.StorageKey },
            cancellationToken);
        if (row is null)
        {
            return Result.Failure<EvidenceDownload>(NotFoundError.For("Attachment", id));
        }

        var content = KpiAttachment.IsValidStorageKey(row.StorageKey) ? await storage.OpenReadAsync(row.StorageKey, cancellationToken) : null;
        if (content is null)
        {
            LogFileMissing(id);
            return Result.Failure<EvidenceDownload>(AppError.NotFound("The file of this attachment is not available."));
        }

        var contentType = EvidenceFiles.IsStoredContentType(row.ContentType) ? row.ContentType : "application/octet-stream";
        return Result.Success(new EvidenceDownload(row.FileName, contentType, content));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The stored file of evidence attachment {AttachmentId} is missing.")]
    private partial void LogFileMissing(int attachmentId);
}
