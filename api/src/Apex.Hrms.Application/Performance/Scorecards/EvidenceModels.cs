using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// One evidence file on a KPI of a scorecard. Only the metadata: the storage key is never exposed, and the file is downloaded from
/// <c>GET /api/kpi/attachments/{id}</c>. <c>fileName</c> is the cleaned name the employee uploaded; <c>contentType</c> is the one the API
/// detected from the content.
/// </summary>
public sealed record AttachmentDto(int Id, string FileName, string ContentType, long SizeBytes, DateOnly UploadedOn);

/// <summary>An upload: the scorecard's row version, the client's file name (cleaned by the API, never trusted) and the file content.</summary>
public sealed record EvidenceUploadRequest(string? RowVersion, string? FileName, byte[] Content);

/// <summary>
/// The answer to an upload (201): the new attachment and the scorecard's new row version. Adding evidence changes the scorecard's
/// row version (it is the one concurrency token of the whole scorecard), so use this value for the next write.
/// </summary>
public sealed record EvidenceUploadResult(AttachmentDto Attachment, byte[] ScorecardRowVersion);

/// <summary>The answer to a removal (200): the scorecard's new row version.</summary>
public sealed record EvidenceRemovalResult(byte[] ScorecardRowVersion);

/// <summary>A file to send: the stream is disposed by whoever sends it.</summary>
public sealed record EvidenceDownload(string FileName, string ContentType, Stream Content);

/// <summary>Adds an evidence file to a KPI of the caller's own scorecard (<c>POST /api/kpi/scorecard-items/{itemId}/attachments</c>).</summary>
public interface IUploadEvidenceHandler
{
    Task<Result<EvidenceUploadResult>> HandleAsync(int itemId, EvidenceUploadRequest request, CancellationToken cancellationToken);
}

/// <summary>Removes an evidence file (<c>DELETE /api/kpi/attachments/{id}</c>).</summary>
public interface IRemoveEvidenceHandler
{
    Task<Result<EvidenceRemovalResult>> HandleAsync(int id, string? rowVersion, CancellationToken cancellationToken);
}

/// <summary>Opens an evidence file of a scorecard the caller may see (<c>GET /api/kpi/attachments/{id}</c>).</summary>
public interface IDownloadEvidenceHandler
{
    Task<Result<EvidenceDownload>> HandleAsync(int id, CancellationToken cancellationToken);
}
