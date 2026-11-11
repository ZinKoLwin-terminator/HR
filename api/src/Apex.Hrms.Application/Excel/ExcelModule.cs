using System.IO.Compression;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Excel;

/// <summary>
/// A module that takes part in Excel import and export: its columns plus small adapters that connect rows to the
/// module's own handlers. No spreadsheet code lives in a module.
/// </summary>
public interface IExcelModule
{
    ExcelModuleInfo Info { get; }

    /// <summary>Policies the caller needs to import rows (the rows are created or changed through the module's own use cases).</summary>
    IReadOnlyList<string> ImportPolicies { get; }

    /// <summary>Policies the caller needs to export: the module's own View.</summary>
    IReadOnlyList<string> ExportPolicies { get; }

    /// <summary>Extra policies needed when the export includes sensitive columns.</summary>
    IReadOnlyList<string> SensitiveExportPolicies { get; }

    /// <summary>Lookup sheets added to the import template (for example the valid organization codes).</summary>
    Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken);

    /// <summary>A fresh adapter for one validation or import run.</summary>
    IExcelImportAdapter CreateImporter();

    /// <summary>
    /// The records to export, filtered like the module's list, keyed by column key. At most <paramref name="limit"/> + 1
    /// rows, so the caller can detect an export over the limit. <paramref name="importLayout"/> asks for the import columns.
    /// </summary>
    Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken);

    IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows);
}

/// <summary>Applies the rows of one run. Every call that saves does so through the module's normal handlers.</summary>
public interface IExcelImportAdapter
{
    /// <summary>Loads the lookups (organization codes ...) once, before the first row.</summary>
    Task PrepareAsync(CancellationToken cancellationToken);

    /// <summary>True when a record already has the row's match key.</summary>
    Task<bool> ExistsAsync(ParsedRow row, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates the record with the rules of the normal API. Problems come back as issues named after the
    /// columns the user has to correct; an Error issue means nothing of the row may stay saved.
    /// </summary>
    Task<IReadOnlyList<ImportIssueDto>> ApplyAsync(ParsedRow row, bool exists, CancellationToken cancellationToken);
}

/// <summary>The checks on an uploaded file before it is parsed: name, size, content type and what the package contains.</summary>
public static class ExcelFileChecks
{
    public static Result Check(string fileName, byte[] content, ExcelLimits limits)
    {
        if (!fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Invalid("Only Excel workbooks (.xlsx) can be imported. Other formats, including .xls, .xlsm and .csv, are not accepted.");
        }

        if (content.Length == 0)
        {
            return Invalid("The file is empty.");
        }

        if (content.Length > limits.MaxFileBytes)
        {
            var mb = (limits.MaxFileBytes / (1024.0 * 1024.0)).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            return Invalid($"The file is too large. The maximum size is {mb} MB.");
        }

        // An .xlsx file is a ZIP archive ("PK\x03\x04"); a renamed file of another type is refused.
        if (content.Length < 4 || content[0] != 0x50 || content[1] != 0x4b || content[2] != 0x03 || content[3] != 0x04)
        {
            return Invalid("The file is not a valid Excel workbook (its content does not match the .xlsx format).");
        }

        try
        {
            using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
            if (archive.Entries.Count > limits.MaxEntries || archive.Entries.Sum(e => e.Length) > limits.MaxUncompressedBytes)
            {
                return Invalid("The workbook is too large when unpacked.");
            }

            var names = archive.Entries.Select(e => e.FullName).ToList();
            if (names.Any(n => n.Contains("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
            {
                return Invalid("The workbook contains macros, which are not allowed. Save a copy without macros as .xlsx.");
            }

            if (names.Any(n => n.Contains("externalLinks/", StringComparison.OrdinalIgnoreCase)))
            {
                return Invalid("The workbook contains links to other workbooks, which are not allowed. Break the links and retry.");
            }

            return names.Any(n => n.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase))
                ? Result.Success()
                : Invalid("The file is not a valid Excel workbook (its content does not match the .xlsx format).");
        }
        catch (InvalidDataException)
        {
            return Invalid("The file is not a valid Excel workbook (its content does not match the .xlsx format).");
        }
    }

    private static Result Invalid(string message) => Result.Failure(AppError.Validation(message, "file", message));
}
