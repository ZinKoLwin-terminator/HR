using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Apex.Hrms.Domain.Excel;

namespace Apex.Hrms.Application.Excel;

// Shapes mirror ui/src/app/core/models/excel.models.ts (the mock is the specification).

[JsonConverter(typeof(JsonStringEnumConverter<ExcelColumnType>))]
public enum ExcelColumnType
{
    [JsonStringEnumMemberName("text")]
    Text,
    [JsonStringEnumMemberName("integer")]
#pragma warning disable CA1720
    Integer,
    [JsonStringEnumMemberName("decimal")]
    Decimal,
#pragma warning restore CA1720
    [JsonStringEnumMemberName("date")]
    Date,
    [JsonStringEnumMemberName("time")]
    Time,
    [JsonStringEnumMemberName("email")]
    Email,
    [JsonStringEnumMemberName("phone")]
    Phone,
    [JsonStringEnumMemberName("enum")]
    Enum,
    [JsonStringEnumMemberName("boolean")]
    Boolean,
}

/// <summary>One column of a module's sheet. <see cref="Key"/> is internal; <see cref="Header"/> is what the workbook shows.</summary>
public sealed record ExcelColumn
{
    public required string Key { get; init; }

    public required string Header { get; init; }

    /// <summary>Other headers recognised for this column (case and punctuation are ignored).</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    public string Description { get; init; } = string.Empty;

    public bool Required { get; init; }

    public ExcelColumnType Type { get; init; } = ExcelColumnType.Text;

    public int? MaxLength { get; init; }

    public int? MinLength { get; init; }

    /// <summary>Regular expression (ECMAScript flavour) the text must match, with the message shown otherwise.</summary>
    public string? Pattern { get; init; }

    public string? PatternMessage { get; init; }

    public bool Uppercase { get; init; }

    public decimal? Min { get; init; }

    public decimal? Max { get; init; }

    public int? Decimals { get; init; }

    public IReadOnlyList<string> Allowed { get; init; } = [];

    public object? Example { get; init; }

    public int? Width { get; init; }

    /// <summary>Personal or financial data: left out of exports unless explicitly included, masked in previews and issues.</summary>
    public bool Sensitive { get; init; }
}

public sealed record ExcelModuleInfo(
    string Code,
    string Label,
    string SheetName,
    string Description,
    IReadOnlyList<ExcelColumn> Columns,
    IReadOnlyList<ExcelColumn>? ExportColumns,
    IReadOnlyList<string> MatchKey,
    string MatchKeyLabel,
    bool CanImport,
    bool CanExport,
    string? ExportDateLabel);

/// <summary>A module that cannot be imported or exported (yet), with the reason. Part of the catalog response.</summary>
public sealed record ExcelModuleAvailability(string Code, string Label, bool CanImport, bool CanExport, string Reason);

public sealed record ExcelCatalogDto(IReadOnlyList<ExcelModuleInfo> Modules, IReadOnlyList<ExcelModuleAvailability> Unavailable);

/// <summary>Limits of uploads and exports (configuration section <c>Excel</c>). Defaults are those of the mock.</summary>
public sealed class ExcelLimits
{
    public long MaxFileBytes { get; init; } = 5 * 1024 * 1024;

    public int MaxRows { get; init; } = 5000;

    public int MaxSheets { get; init; } = 5;

    public int MaxExportRows { get; init; } = 50_000;

    /// <summary>Largest total size of the unpacked workbook (zip bomb guard).</summary>
    public long MaxUncompressedBytes { get; init; } = 100L * 1024 * 1024;

    public int MaxEntries { get; init; } = 2000;

    /// <summary>How long an uploaded file is kept when nobody validates or executes it.</summary>
    public int UploadMinutes { get; init; } = 30;
}

public sealed record ExcelFileResult(string FileName, string ContentType, byte[] Content);

public static class ExcelContentTypes
{
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

// ---- Parsed workbook ----

/// <summary>A cell as read from a worksheet; <c>Value</c> is a string, double, bool, DateTime, TimeSpan or null. Formulas are never evaluated.</summary>
public sealed record RawCell(object? Value, bool Formula);

public sealed record ParsedSheetRow(int RowNumber, IReadOnlyList<RawCell> Cells);

public sealed record ParsedSheet(string Name, int SheetCount, IReadOnlyList<string> Headers, IReadOnlyList<ParsedSheetRow> Rows);

/// <summary>Typed values of one row by column key: string (dates as yyyy-MM-dd, times as HH:mm), decimal, bool or null.</summary>
public sealed class ParsedRow : Dictionary<string, object?>
{
    public string Text(string key) =>
        TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

    public bool Has(string key) => Text(key).Length > 0;
}

// ---- Export sheets ----

public sealed record ExportSheet(string Name, IReadOnlyList<ExcelColumn> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);

// ---- Import ----

public sealed record FileInspectionDto(
    string UploadId,
    string ModuleCode,
    string FileName,
    long FileSize,
    string SheetName,
    int SheetCount,
    int RowCount,
    IReadOnlyList<string> Headers,
    IReadOnlyDictionary<string, string?> Mapping,
    IReadOnlyList<string> MissingRequired,
    IReadOnlyList<string> UnknownHeaders,
    IReadOnlyList<string> AmbiguousHeaders,
    bool NeedsMapping);

[JsonConverter(typeof(JsonStringEnumConverter<ImportRowStatus>))]
public enum ImportRowStatus
{
    Valid,
    Invalid,
    Warning,
    Duplicate,
}

[JsonConverter(typeof(JsonStringEnumConverter<ImportRowAction>))]
public enum ImportRowAction
{
    Insert,
    Update,
    None,
}

public sealed record ImportIssueDto(string Field, string Value, string Message, string? Suggestion, ImportSeverity Severity);

public sealed record ImportIssueRow(int RowNumber, string Field, string Value, string Message, string? Suggestion, ImportSeverity Severity);

public sealed record ImportPreviewRowDto(
    int RowNumber,
    IReadOnlyDictionary<string, string> Values,
    ImportRowStatus Status,
    ImportRowAction Action,
    IReadOnlyList<ImportIssueDto> Issues);

public sealed record ImportPreviewDto(
    string BatchId,
    string ModuleCode,
    string FileName,
    string SheetName,
    ImportStrategy Strategy,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int DuplicateRows,
    int WarningRows,
    IReadOnlyList<ImportPreviewRowDto> Rows);

/// <param name="UploadId">The id returned by inspect.</param>
/// <param name="Strategy">InsertOnly, UpdateOnly or InsertOrUpdate.</param>
/// <param name="Mapping">Header found in the file to column key (null = ignore). Omitted: the suggested mapping.</param>
public sealed record ImportValidateRequest(
    string UploadId,
    ImportStrategy Strategy,
    IReadOnlyDictionary<string, string?>? Mapping);

public sealed record ImportExecuteRequest(ImportMode Mode);

public sealed record ImportResultDto(
    string BatchId,
    ImportBatchStatus Status,
    ImportMode Mode,
    int Inserted,
    int Updated,
    int Skipped,
    int Failed,
    bool RolledBack,
    string Message);

public sealed record ImportBatchSummaryDto(
    string BatchId,
    string ModuleCode,
    string ModuleLabel,
    string FileName,
    ImportStrategy Strategy,
    ImportMode? Mode,
    ImportBatchStatus Status,
    string UploadedBy,
    DateTime UploadedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int SuccessRows,
    int FailedRows,
    int SkippedRows,
    string ErrorSummary);

/// <summary>The summary fields plus the issues, flattened like <c>ImportBatchDetail</c> of the frontend.</summary>
public sealed record ImportBatchDetailDto(
    string BatchId,
    string ModuleCode,
    string ModuleLabel,
    string FileName,
    ImportStrategy Strategy,
    ImportMode? Mode,
    ImportBatchStatus Status,
    string UploadedBy,
    DateTime UploadedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int SuccessRows,
    int FailedRows,
    int SkippedRows,
    string ErrorSummary,
    IReadOnlyList<ImportIssueRow> Issues);

// ---- Export ----

[JsonConverter(typeof(JsonStringEnumConverter<ExportScope>))]
public enum ExportScope
{
    Filtered,
    All,
    Selected,
}

/// <summary>
/// The same search and filters as the module's list screen. <c>Layout</c> "template" writes the import columns (business
/// codes, so the file can be imported again); the default writes the readable report.
/// </summary>
public sealed record ExportRequest(
    ExportScope Scope = ExportScope.Filtered,
    string? Search = null,
    IReadOnlyDictionary<string, JsonElement>? Filters = null,
    IReadOnlyList<int>? SelectedIds = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    IReadOnlyList<string>? ColumnKeys = null,
    bool IncludeSensitive = false,
    string? Layout = null);
