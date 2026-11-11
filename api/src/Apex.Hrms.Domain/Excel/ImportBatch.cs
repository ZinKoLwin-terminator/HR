using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Domain.Excel;

/// <summary>How rows that match an existing record (by the module's match key) are treated. The names are the database values.</summary>
public enum ImportStrategy
{
    InsertOnly,
    UpdateOnly,
    InsertOrUpdate,
}

/// <summary>Chosen when the import runs. The names are the database values.</summary>
public enum ImportMode
{
    /// <summary>Every row is saved or none is (one transaction).</summary>
    AllOrNothing,

    /// <summary>The valid rows are saved; the others are skipped.</summary>
    Partial,
}

public enum ImportBatchStatus
{
    Validated,
    Completed,
    CompletedWithErrors,
    Failed,
}

public enum ImportSeverity
{
    Error,
    Warning,
}

/// <summary>
/// One Excel import (app.ImportBatch): validated first, executed once. It records who uploaded the file and the counts;
/// the issues hold the problems found. Cell values kept in issues are truncated and masked for sensitive columns.
/// </summary>
public sealed class ImportBatch
{
    public const int ModuleCodeMaxLength = 30;
    public const int ModuleLabelMaxLength = 60;
    public const int FileNameMaxLength = 260;
    public const int UserNameMaxLength = 130;
    public const int ErrorSummaryMaxLength = 500;

    private readonly List<ImportIssue> issues = [];

    private ImportBatch()
    {
    }

    public Guid Id { get; private set; }

    public string ModuleCode { get; private set; } = string.Empty;

    public string ModuleLabel { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public ImportStrategy Strategy { get; private set; }

    public ImportMode? Mode { get; private set; }

    public ImportBatchStatus Status { get; private set; }

    public int? UploadedByUserId { get; private set; }

    public string UploadedByName { get; private set; } = string.Empty;

    public DateTime UploadedAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public int TotalRows { get; private set; }

    public int ValidRows { get; private set; }

    public int InvalidRows { get; private set; }

    public int SuccessRows { get; private set; }

    public int FailedRows { get; private set; }

    public int SkippedRows { get; private set; }

    public string? ErrorSummary { get; private set; }

    public IReadOnlyCollection<ImportIssue> Issues => issues;

    /// <summary>A batch that was validated and has not run. <paramref name="id"/> is also the key of the stored file.</summary>
    public static ImportBatch Validated(
        Guid id,
        string moduleCode,
        string moduleLabel,
        string fileName,
        ImportStrategy strategy,
        int? userId,
        string userName,
        DateTime uploadedAtUtc,
        int totalRows,
        int validRows,
        int invalidRows) => new()
        {
            Id = id,
            ModuleCode = Cut(moduleCode, ModuleCodeMaxLength),
            ModuleLabel = Cut(moduleLabel, ModuleLabelMaxLength),
            FileName = Cut(fileName, FileNameMaxLength),
            Strategy = strategy,
            Status = ImportBatchStatus.Validated,
            UploadedByUserId = userId,
            UploadedByName = Cut(userName, UserNameMaxLength),
            UploadedAtUtc = uploadedAtUtc,
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows,
        };

    public void AddIssue(ImportIssue issue) => issues.Add(issue);

    /// <summary>Records the outcome of the single execution. A batch that has run cannot run again.</summary>
    public Result Complete(
        ImportMode mode,
        ImportBatchStatus status,
        DateTime startedAtUtc,
        DateTime completedAtUtc,
        int success,
        int failed,
        int skipped,
        string? errorSummary)
    {
        if (Status != ImportBatchStatus.Validated)
        {
            return Result.Failure(AppError.Conflict(
                "This import has already been processed. Upload the file again to import it again."));
        }

        Mode = mode;
        Status = status;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = completedAtUtc < startedAtUtc ? startedAtUtc : completedAtUtc;
        SuccessRows = success;
        FailedRows = failed;
        SkippedRows = skipped;
        ErrorSummary = string.IsNullOrWhiteSpace(errorSummary) ? null : Cut(errorSummary, ErrorSummaryMaxLength);
        return Result.Success();
    }

    internal static string Cut(string text, int maxLength) =>
        text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 1), "…");
}

/// <summary>One validation or execution problem of a batch (app.ImportIssue). Never holds a full cell value: it is cut and masked.</summary>
public sealed class ImportIssue
{
    public const int FieldMaxLength = 100;
    public const int ValueMaxLength = 100;
    public const int MessageMaxLength = 500;
    public const int SuggestionMaxLength = 300;

    private ImportIssue()
    {
    }

    public long Id { get; private set; }

    public Guid ImportBatchId { get; private set; }

    /// <summary>Row number in the worksheet (1 = header).</summary>
    public int RowNumber { get; private set; }

    public string Field { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? Suggestion { get; private set; }

    public ImportSeverity Severity { get; private set; }

    public static ImportIssue Create(
        int rowNumber,
        string field,
        string value,
        string message,
        string? suggestion,
        ImportSeverity severity) => new()
        {
            RowNumber = Math.Max(1, rowNumber),
            Field = ImportBatch.Cut(field, FieldMaxLength),
            Value = ImportBatch.Cut(value, ValueMaxLength),
            Message = ImportBatch.Cut(message, MessageMaxLength),
            Suggestion = string.IsNullOrWhiteSpace(suggestion) ? null : ImportBatch.Cut(suggestion, SuggestionMaxLength),
            Severity = severity,
        };
}
