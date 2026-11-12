using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Excel;

/// <summary>
/// The real Excel services and modules over the in-memory fakes: a test names a module and the cells of its rows by column key,
/// and gets back the preview, the result or the rows the export would write. The workbook reader and writer are the fakes.
/// </summary>
internal sealed class ExcelModuleHarness : IDisposable
{
    private readonly FakeFileStorage storage = new();
    private readonly FakeWorkbookReader reader = new();
    private readonly FakeBatchStore batches = new();

    public ExcelModuleHarness()
    {
        Writer = new FakeWorkbookWriter();
        Host = new TestHost(services =>
        {
            services.AddSingleton<IFileStorage>(storage);
            services.AddSingleton<IWorkbookReader>(reader);
            services.AddSingleton<IWorkbookWriter>(Writer);
            services.AddSingleton<IImportBatchStore>(batches);
            services.AddSingleton<IBatchTransactionFactory>(new FakeTransactions());
            services.AddSingleton<IActivityMute>(new NoMute());
        });
    }

    public TestHost Host { get; }

    public FakeWorkbookWriter Writer { get; }

    public IExcelImportService Imports => Host.Get<IExcelImportService>();

    public IExcelExportService Exports => Host.Get<IExcelExportService>();

    public IExcelCatalogService Catalog => Host.Get<IExcelCatalogService>();

    public ExcelModuleInfo Info(string code) => Catalog.GetModules().Value.Modules.Single(m => m.Code == code);

    public void Dispose() => Host.Dispose();

    /// <summary>A row as the cells of a sheet, in the order of the module's import columns; keys not given stay empty.</summary>
    public string?[] Row(string module, IReadOnlyDictionary<string, string?> values) =>
        [.. Info(module).Columns.Select(c => values.GetValueOrDefault(c.Key))];

    public async Task<ImportPreviewDto> ValidateAsync(string module, ImportStrategy strategy, params IReadOnlyDictionary<string, string?>[] rows)
    {
        var headers = Info(module).Columns.Select(c => c.Header).ToArray();
        reader.Next = Result.Success(ExcelTestFiles.Sheet(headers, [.. rows.Select(r => Row(module, r))]));
        var inspected = await Imports.InspectAsync(module, $"{module}.xlsx", ExcelTestFiles.Package(), default);
        Assert.True(inspected.IsSuccess, inspected.IsFailure ? inspected.Error.Message : null);
        var preview = await Imports.ValidateAsync(module, new ImportValidateRequest(inspected.Value.UploadId, strategy, null), default);
        Assert.True(preview.IsSuccess, preview.IsFailure ? preview.Error.Message : null);
        return preview.Value;
    }

    public async Task<ImportResultDto> ExecuteAsync(ImportPreviewDto preview, ImportMode mode)
    {
        var result = await Imports.ExecuteAsync(preview.BatchId, mode, default);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }

    /// <summary>The rows the export writes (by column key), failing the test when the export is refused.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExportAsync(string module, ExportRequest? request = null)
    {
        var result = await Exports.ExportAsync(module, request ?? new ExportRequest(ExportScope.All), default);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return Writer.ExportedMain!.Rows;
    }

    public static IReadOnlyDictionary<string, string?> Cells(params (string Key, string? Value)[] cells) =>
        cells.ToDictionary(c => c.Key, c => c.Value);

    public static IReadOnlyDictionary<string, string?> With(IReadOnlyDictionary<string, string?> row, params (string Key, string? Value)[] changes)
    {
        var copy = new Dictionary<string, string?>(row);
        foreach (var (key, value) in changes)
        {
            copy[key] = value;
        }

        return copy;
    }
}
