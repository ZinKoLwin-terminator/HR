using System.IO.Compression;
using Apex.Hrms.Application;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;

namespace Apex.Hrms.Application.Tests.Excel;

internal sealed class FakeFileStorage : IFileStorage
{
    public Dictionary<string, StoredFile> Files { get; } = [];

    public Task SaveAsync(string key, byte[] content, IReadOnlyDictionary<string, string> metadata, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        Files[key] = new StoredFile(content, new Dictionary<string, string>(metadata));
        return Task.CompletedTask;
    }

    public Task<StoredFile?> ReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Files.GetValueOrDefault(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }

    public Task PurgeExpiredAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeWorkbookReader : IWorkbookReader
{
    public Result<ParsedSheet>? Next { get; set; }

    public Result<ParsedSheet> Read(byte[] content, string preferredSheet, ExcelLimits limits) =>
        Next ?? Result.Failure<ParsedSheet>(AppError.Validation("no sheet", "file", "no sheet"));
}

internal sealed class FakeWorkbookWriter : IWorkbookWriter
{
    public IReadOnlyList<ImportIssueRow>? ReportedIssues { get; private set; }

    public ExportSheet? ExportedMain { get; private set; }

    public byte[] BuildTemplate(ExcelModuleInfo info, IReadOnlyList<ExportSheet> referenceSheets, ExcelLimits limits) => [1];

    public byte[] BuildExport(string title, ExportSheet main, IReadOnlyList<ExportSheet> extraSheets)
    {
        ExportedMain = main;
        return [2];
    }

    public byte[] BuildErrorReport(string moduleLabel, IReadOnlyList<ImportIssueRow> issues)
    {
        ReportedIssues = issues;
        return [3];
    }
}

internal sealed class FakeBatchStore : IImportBatchStore
{
    public List<ImportBatch> Batches { get; } = [];

    public void Add(ImportBatch batch) => Batches.Add(batch);

    public Task<ImportBatch?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Batches.FirstOrDefault(b => b.Id == id));

    public Task<PagedResult<ImportBatch>> QueryAsync(int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var all = Batches.OrderByDescending(b => b.UploadedAtUtc).ToList();
        return Task.FromResult(new PagedResult<ImportBatch>([.. all.Skip(pageIndex * pageSize).Take(pageSize)], all.Count, pageIndex, pageSize));
    }
}

internal sealed class FakeTransactions : IBatchTransactionFactory
{
    public List<FakeTransaction> Started { get; } = [];

    public Task<IBatchTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = new FakeTransaction();
        Started.Add(transaction);
        return Task.FromResult<IBatchTransaction>(transaction);
    }
}

internal sealed class FakeTransaction : IBatchTransaction
{
    public bool Committed { get; private set; }

    public bool RolledBack { get; private set; }

    public int RolledBackToSavepoint { get; private set; }

    public Task SavepointAsync(string name, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken)
    {
        RolledBackToSavepoint++;
        return Task.CompletedTask;
    }

    public void ClearTracked()
    {
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        Committed = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        RolledBack = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class NoMute : IActivityMute
{
    public IDisposable Mute() => new Nothing();

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>A module with two columns whose adapter is scripted by the row values: name BAD is rejected, WARN warns, FAILRUN fails only while importing.</summary>
internal sealed class ScriptedModule : IExcelModule
{
    public static readonly ExcelColumn CodeColumn = new() { Key = "code", Header = "Code", Required = true, Uppercase = true, MaxLength = 8, Description = "Unique code." };

    public static readonly ExcelColumn NameColumn = new() { Key = "name", Header = "Name", Required = true, MaxLength = 20, Description = "Name." };

    public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Applied { get; } = [];

    /// <summary>When true, a row named FAILRUN is rejected (it passed validation, so the failure happens during the import).</summary>
    public bool FailDuringImport { get; set; }

    public ExcelModuleInfo Info { get; } = new(
        "items",
        "Items",
        "Items",
        "Test items.",
        [CodeColumn, NameColumn],
        null,
        ["code"],
        "Code",
        CanImport: true,
        CanExport: true,
        null);

    public IReadOnlyList<string> ImportPolicies => ["items:Create"];

    public IReadOnlyList<string> ExportPolicies => ["items:View"];

    public IReadOnlyList<string> SensitiveExportPolicies => [];

    public List<IReadOnlyDictionary<string, object?>> ExportData { get; } = [];

    public Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExportSheet>>([]);

    public IExcelImportAdapter CreateImporter() => new Importer(this);

    public Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(ExportRequest request, bool importLayout, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success<IReadOnlyList<IReadOnlyDictionary<string, object?>>>([.. ExportData.Take(limit + 1)]));

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => [];

    private sealed class Importer(ScriptedModule module) : IExcelImportAdapter
    {
        private readonly HashSet<string> created = new(StringComparer.OrdinalIgnoreCase);

        public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> ExistsAsync(ParsedRow row, CancellationToken cancellationToken) =>
            Task.FromResult(module.Existing.Contains(row.Text("code")) || created.Contains(row.Text("code")));

        public Task<IReadOnlyList<ImportIssueDto>> ApplyAsync(ParsedRow row, bool exists, CancellationToken cancellationToken)
        {
            var name = row.Text("name");
            if (name == "BAD" || (name == "FAILRUN" && module.FailDuringImport))
            {
                return Task.FromResult<IReadOnlyList<ImportIssueDto>>(
                    [new ImportIssueDto("Name", name, "The name is not accepted.", null, ImportSeverity.Error)]);
            }

            module.Applied.Add(row.Text("code"));
            created.Add(row.Text("code"));
            return Task.FromResult<IReadOnlyList<ImportIssueDto>>(name == "WARN"
                ? [new ImportIssueDto("Name", name, "Looks odd.", null, ImportSeverity.Warning)]
                : []);
        }
    }
}

internal static class ExcelTestFiles
{
    /// <summary>A ZIP package with the given entries, enough to pass the file checks (the reader is a fake).</summary>
    public static byte[] Package(params string[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in entries.Length == 0 ? ["xl/workbook.xml"] : entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write("<x/>");
            }
        }

        return stream.ToArray();
    }

    public static ParsedSheet Sheet(string[] headers, params string?[][] rows) => new(
        "Items",
        1,
        headers,
        [.. rows.Select((row, i) => new ParsedSheetRow(i + 2, [.. row.Select(v => new RawCell(v, false))]))]);
}
