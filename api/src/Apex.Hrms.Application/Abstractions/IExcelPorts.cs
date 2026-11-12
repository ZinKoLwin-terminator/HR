using Apex.Hrms.Application.Excel;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;

namespace Apex.Hrms.Application.Abstractions;

/// <summary>
/// Temporary storage of uploaded workbooks. A file is kept only until it is used or its lifetime ends; after that it is
/// gone and reading it returns null. Keys are 32 hexadecimal characters (a GUID without dashes), nothing else.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(
        string key,
        byte[] content,
        IReadOnlyDictionary<string, string> metadata,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    /// <summary>The file, or null when it does not exist or has expired.</summary>
    Task<StoredFile?> ReadAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>Removes every expired file.</summary>
    Task PurgeExpiredAsync(CancellationToken cancellationToken);
}

public sealed record StoredFile(byte[] Content, IReadOnlyDictionary<string, string> Metadata);

/// <summary>Reads and writes .xlsx workbooks. The only port that touches a spreadsheet library.</summary>
public interface IWorkbookReader
{
    /// <summary>Reads the data worksheet. Formulas are flagged, never calculated. Problems with the content are Validation failures.</summary>
    Result<ParsedSheet> Read(byte[] content, string preferredSheet, ExcelLimits limits);
}

public interface IWorkbookWriter
{
    byte[] BuildTemplate(ExcelModuleInfo info, IReadOnlyList<ExportSheet> referenceSheets, ExcelLimits limits);

    byte[] BuildExport(string title, ExportSheet main, IReadOnlyList<ExportSheet> extraSheets);

    byte[] BuildErrorReport(string moduleLabel, IReadOnlyList<ImportIssueRow> issues);
}

/// <summary>Persistence of import batches and their issues. Saved by <see cref="IUnitOfWork"/>.</summary>
public interface IImportBatchStore
{
    void Add(ImportBatch batch);

    /// <summary>The batch with its issues (tracked, so a change to it is saved by the unit of work), or null.</summary>
    Task<ImportBatch?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first, without issues; only the batches of <paramref name="uploadedByUserId"/> when it is given.</summary>
    Task<PagedResult<ImportBatch>> QueryAsync(int pageIndex, int pageSize, int? uploadedByUserId, CancellationToken cancellationToken);
}

/// <summary>Starts the one database transaction a validation or an import runs in.</summary>
public interface IBatchTransactionFactory
{
    Task<IBatchTransaction> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>
/// An open transaction. Disposing it without <see cref="CommitAsync"/> rolls it back. Handlers save inside it (each
/// save is part of the transaction), so a savepoint per row can undo one failed row without losing the others.
/// </summary>
public interface IBatchTransaction : IAsyncDisposable
{
    Task SavepointAsync(string name, CancellationToken cancellationToken);

    Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken);

    /// <summary>Forgets what the unit of work tracks, so the next row starts clean (also after a failed row).</summary>
    void ClearTracked();

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Silences <see cref="IActivityRecorder"/> while a batch applies its rows: an import writes one activity entry for the
/// whole batch, not one per row.
/// </summary>
public interface IActivityMute
{
    IDisposable Mute();
}
