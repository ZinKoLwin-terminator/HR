using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace Apex.Hrms.Application.Tests.Excel;

/// <summary>The Excel import and export services with a scripted module, so every rule of the engine is checked on its own.</summary>
public sealed class ExcelImportServiceTests : IDisposable
{
    private static readonly string[] Headers = ["Code", "Name"];

    private readonly FakeFileStorage storage = new();
    private readonly FakeWorkbookReader reader = new();
    private readonly FakeWorkbookWriter writer = new();
    private readonly FakeBatchStore batches = new();
    private readonly FakeTransactions transactions = new();
    private readonly ScriptedModule module = new();
    private readonly TestHost host;

    public ExcelImportServiceTests() =>
        host = new TestHost(services =>
        {
            services.AddSingleton<IFileStorage>(storage);
            services.AddSingleton<IWorkbookReader>(reader);
            services.AddSingleton<IWorkbookWriter>(writer);
            services.AddSingleton<IImportBatchStore>(batches);
            services.AddSingleton<IBatchTransactionFactory>(transactions);
            services.AddSingleton<IActivityMute>(new NoMute());
            services.AddSingleton<IExcelModule>(module);
            services.AddSingleton(new ExcelLimits { MaxFileBytes = 5000 });
        });

    private IExcelImportService Imports => host.Get<IExcelImportService>();

    private IExcelExportService Exports => host.Get<IExcelExportService>();

    public void Dispose() => host.Dispose();

    private async Task<string> InspectAsync(params string?[][] rows)
    {
        reader.Next = Result.Success(ExcelTestFiles.Sheet(Headers, rows));
        var inspected = await Imports.InspectAsync("items", "items.xlsx", ExcelTestFiles.Package(), default);
        Assert.True(inspected.IsSuccess, inspected.IsFailure ? inspected.Error.Message : null);
        return inspected.Value.UploadId;
    }

    private async Task<ImportPreviewDto> ValidateAsync(ImportStrategy strategy, params string?[][] rows)
    {
        var uploadId = await InspectAsync(rows);
        var preview = await Imports.ValidateAsync("items", new ImportValidateRequest(uploadId, strategy, null), default);
        Assert.True(preview.IsSuccess, preview.IsFailure ? preview.Error.Message : null);
        return preview.Value;
    }

    // ---- Catalog and template ----

    [Fact]
    public void The_catalog_lists_the_modules_that_exist_and_says_why_the_others_do_not()
    {
        var catalog = host.Get<IExcelCatalogService>().GetModules().Value;

        Assert.Contains(catalog.Modules, m => m.Code == "employees" && m.CanImport && m.CanExport);
        Assert.Contains(catalog.Unavailable, m => m.Code == "leave-types" && m.Reason.Contains("no columns", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_template_is_only_available_for_a_known_module()
    {
        Assert.Equal("items-import-template.xlsx", (await Imports.GetTemplateAsync("items", default)).Value.FileName);
        Assert.Equal(ErrorType.NotFound, (await Imports.GetTemplateAsync("nope", default)).Error.Type);
    }

    // ---- Inspect ----

    [Fact]
    public async Task Inspect_refuses_files_that_are_not_xlsx_too_large_or_macro_enabled()
    {
        reader.Next = Result.Success(ExcelTestFiles.Sheet(Headers, ["A", "B"]));

        var csv = await Imports.InspectAsync("items", "items.csv", ExcelTestFiles.Package(), default);
        var big = await Imports.InspectAsync("items", "items.xlsx", new byte[5001], default);
        var macro = await Imports.InspectAsync("items", "items.xlsx", ExcelTestFiles.Package("xl/workbook.xml", "xl/vbaProject.bin"), default);

        Assert.Equal(ErrorType.Validation, csv.Error.Type);
        Assert.Contains(".csv", csv.Error.Message);
        Assert.Contains("too large", big.Error.Message);
        Assert.Contains("macros", macro.Error.Message);
        Assert.Empty(storage.Files);
    }

    [Fact]
    public async Task Inspect_keeps_the_file_and_reports_the_header_check()
    {
        reader.Next = Result.Success(ExcelTestFiles.Sheet(["Code", "Nickname"], ["A", "B"]));

        var inspected = (await Imports.InspectAsync("items", "items.xlsx", ExcelTestFiles.Package(), default)).Value;

        Assert.Single(storage.Files);
        Assert.Equal(["name"], inspected.MissingRequired);
        Assert.Equal(["Nickname"], inspected.UnknownHeaders);
        Assert.True(inspected.NeedsMapping);
        Assert.Equal("code", inspected.Mapping["Code"]);
        Assert.Equal(1, inspected.RowCount);
    }

    [Fact]
    public async Task Inspect_refuses_a_sheet_without_data_rows_and_passes_on_reader_errors()
    {
        reader.Next = Result.Success(ExcelTestFiles.Sheet(Headers));
        var empty = await Imports.InspectAsync("items", "items.xlsx", ExcelTestFiles.Package(), default);
        Assert.Equal("The worksheet \"Items\" has a header row but no data rows.", empty.Error.Message);

        reader.Next = Result.Failure<ParsedSheet>(AppError.Validation("The workbook is empty: it contains no worksheets.", "file", "x"));
        var failed = await Imports.InspectAsync("items", "items.xlsx", ExcelTestFiles.Package(), default);
        Assert.Equal("The workbook is empty: it contains no worksheets.", failed.Error.Message);
    }

    // ---- Validate ----

    [Fact]
    public async Task Validate_reports_each_rule_with_the_row_number_and_saves_nothing()
    {
        module.Existing.Add("EXIST");

        var preview = await ValidateAsync(
            ImportStrategy.InsertOnly,
            ["OK1", "Fine"],
            [null, "No code"],
            ["OK1", "Again"],
            ["EXIST", "Old"],
            ["BADROW", "BAD"],
            ["WARN1", "WARN"],
            ["LONGLONGCODE", "Fine"]);

        var rows = preview.Rows.ToDictionary(r => r.RowNumber);
        Assert.Equal(ImportRowStatus.Valid, rows[2].Status);
        Assert.Equal(ImportRowAction.Insert, rows[2].Action);
        Assert.Equal(ImportRowStatus.Invalid, rows[3].Status);
        Assert.Equal("Code is required.", rows[3].Issues[0].Message);
        Assert.Equal(ImportRowStatus.Duplicate, rows[4].Status);
        Assert.Equal("Duplicate of row 2 in this file (same Code).", rows[4].Issues[0].Message);
        Assert.Equal(ImportRowStatus.Duplicate, rows[5].Status);
        Assert.Equal("A record with this Code already exists.", rows[5].Issues[0].Message);
        Assert.Equal(ImportRowStatus.Invalid, rows[6].Status);
        Assert.Equal("The name is not accepted.", rows[6].Issues[0].Message);
        Assert.Equal(ImportRowStatus.Warning, rows[7].Status);
        Assert.Equal(ImportRowStatus.Invalid, rows[8].Status);
        Assert.Equal(7, preview.TotalRows);
        Assert.Equal(2, preview.ValidRows);
        Assert.Equal(1, preview.WarningRows);
        Assert.Equal(2, preview.DuplicateRows);
        Assert.Equal(3, preview.InvalidRows);

        // The preview is a dry run: the transaction was rolled back, and the batch is recorded with its issues.
        Assert.True(transactions.Started[0].RolledBack);
        Assert.False(transactions.Started[0].Committed);
        var batch = Assert.Single(batches.Batches);
        Assert.Equal(ImportBatchStatus.Validated, batch.Status);
        Assert.Equal(host.User.DisplayName, batch.UploadedByName);
        Assert.Equal(host.User.UserId, batch.UploadedByUserId);
        Assert.Contains(batch.Issues, i => i.RowNumber == 4 && i.Message.StartsWith("Duplicate of row 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Update_only_marks_unknown_codes_as_invalid_and_updates_known_ones()
    {
        module.Existing.Add("KNOWN");

        var preview = await ValidateAsync(ImportStrategy.UpdateOnly, ["KNOWN", "New name"], ["UNKNOWN", "Whatever"]);

        Assert.Equal(ImportRowAction.Update, preview.Rows[0].Action);
        Assert.Equal(ImportRowStatus.Invalid, preview.Rows[1].Status);
        Assert.Equal("No existing record has this Code, so it cannot be updated.", preview.Rows[1].Issues[0].Message);
    }

    [Fact]
    public async Task Insert_or_update_treats_known_codes_as_updates_and_new_ones_as_inserts()
    {
        module.Existing.Add("KNOWN");

        var preview = await ValidateAsync(ImportStrategy.InsertOrUpdate, ["KNOWN", "A"], ["NEW", "B"]);

        Assert.Equal([ImportRowAction.Update, ImportRowAction.Insert], preview.Rows.Select(r => r.Action));
    }

    [Fact]
    public async Task Validate_refuses_a_bad_mapping_an_unknown_upload_another_users_upload_and_an_expired_one()
    {
        var uploadId = await InspectAsync(["A", "B"]);

        var badMapping = await Imports.ValidateAsync(
            "items",
            new ImportValidateRequest(uploadId, ImportStrategy.InsertOnly, new Dictionary<string, string?> { ["Code"] = "code" }),
            default);
        Assert.Contains("The required column \"Name\" is not mapped.", badMapping.Error.Message);

        var unknown = await Imports.ValidateAsync("items", new ImportValidateRequest(Guid.NewGuid().ToString("N"), ImportStrategy.InsertOnly, null), default);
        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);

        host.User.UserId = 99;
        var other = await Imports.ValidateAsync("items", new ImportValidateRequest(uploadId, ImportStrategy.InsertOnly, null), default);
        Assert.Equal(ErrorType.NotFound, other.Error.Type);
        host.User.UserId = 1;

        storage.Files.Clear();
        var expired = await Imports.ValidateAsync("items", new ImportValidateRequest(uploadId, ImportStrategy.InsertOnly, null), default);
        Assert.Equal("The uploaded file is no longer available. Please upload it again.", expired.Error.Message);
    }

    // ---- Execute ----

    [Fact]
    public async Task Execute_all_or_nothing_refuses_a_batch_with_problems()
    {
        var preview = await ValidateAsync(ImportStrategy.InsertOnly, ["A1", "Fine"], ["B1", "BAD"]);

        module.Applied.Clear();
        var result = await Imports.ExecuteAsync(preview.BatchId, ImportMode.AllOrNothing, default);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("1 row(s) have problems, so an all-or-nothing import cannot run. Fix the file, or import only the valid rows.", result.Error.Message);
        Assert.Empty(module.Applied);
    }

    [Fact]
    public async Task Execute_partial_saves_the_valid_rows_in_one_transaction_and_counts_the_skipped_ones()
    {
        var preview = await ValidateAsync(ImportStrategy.InsertOrUpdate, ["A1", "Fine"], ["B1", "BAD"], ["C1", "Fine"]);
        module.Existing.Clear();
        module.Applied.Clear();

        var result = (await Imports.ExecuteAsync(preview.BatchId, ImportMode.Partial, default)).Value;

        Assert.Equal(ImportBatchStatus.CompletedWithErrors, result.Status);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(1, result.Skipped);
        Assert.False(result.RolledBack);
        Assert.Equal("2 inserted, 0 updated, 1 skipped.", result.Message);
        Assert.Equal(["A1", "C1"], module.Applied);
        var run = transactions.Started[^1];
        Assert.True(run.Committed);
        Assert.Equal(1, run.RolledBackToSavepoint);
    }

    [Fact]
    public async Task Execute_all_or_nothing_rolls_the_whole_batch_back_when_one_row_fails_during_the_import()
    {
        var preview = await ValidateAsync(ImportStrategy.InsertOrUpdate, ["A1", "Fine"], ["B1", "FAILRUN"], ["C1", "Fine"]);
        Assert.Equal(3, preview.ValidRows);
        module.FailDuringImport = true;

        var result = (await Imports.ExecuteAsync(preview.BatchId, ImportMode.AllOrNothing, default)).Value;

        Assert.True(result.RolledBack);
        Assert.Equal(ImportBatchStatus.Failed, result.Status);
        Assert.Equal(0, result.Inserted);
        Assert.Equal("Nothing was imported: 1 row(s) failed, so the whole import was rolled back.", result.Message);
        var run = transactions.Started[^1];
        Assert.True(run.RolledBack);
        Assert.False(run.Committed);
        var batch = batches.Batches.Single();
        Assert.Equal(ImportBatchStatus.Failed, batch.Status);
        Assert.Equal(ImportMode.AllOrNothing, batch.Mode);
        Assert.Contains(batch.Issues, i => i.RowNumber == 3 && i.Message == "The name is not accepted.");
    }

    [Fact]
    public async Task Execute_records_one_activity_entry_deletes_the_file_and_runs_only_once()
    {
        var preview = await ValidateAsync(ImportStrategy.InsertOnly, ["A1", "Fine"], ["B1", "Fine"]);

        var result = (await Imports.ExecuteAsync(preview.BatchId, ImportMode.AllOrNothing, default)).Value;

        Assert.Equal(ImportBatchStatus.Completed, result.Status);
        Assert.Equal(["Excel import of Items (items.xlsx): 2 inserted, 0 updated."], host.ActivityLog.Messages);
        Assert.DoesNotContain(batches.Batches.Single().Id.ToString("N"), storage.Files.Keys);
        var again = await Imports.ExecuteAsync(preview.BatchId, ImportMode.AllOrNothing, default);
        Assert.Equal(ErrorType.Conflict, again.Error.Type);
        Assert.Single(host.ActivityLog.Messages);
        var batch = batches.Batches.Single();
        Assert.Equal(2, batch.SuccessRows);
        Assert.NotNull(batch.CompletedAtUtc);
    }

    [Fact]
    public async Task Execute_needs_a_valid_row_and_a_known_batch()
    {
        var preview = await ValidateAsync(ImportStrategy.InsertOnly, ["B1", "BAD"]);

        Assert.Equal("There are no valid rows to import.", (await Imports.ExecuteAsync(preview.BatchId, ImportMode.Partial, default)).Error.Message);
        Assert.Equal(ErrorType.NotFound, (await Imports.ExecuteAsync("not-a-guid", ImportMode.Partial, default)).Error.Type);
        Assert.Equal(ErrorType.NotFound, (await Imports.ExecuteAsync(Guid.NewGuid().ToString(), ImportMode.Partial, default)).Error.Type);
    }

    // ---- History and error report ----

    [Fact]
    public async Task History_is_paged_newest_first_and_a_batch_shows_its_issues()
    {
        var first = await ValidateAsync(ImportStrategy.InsertOnly, ["A1", "Fine"]);
        host.Clock.Today = host.Clock.Today.AddDays(1);
        var second = await ValidateAsync(ImportStrategy.InsertOnly, ["B1", "BAD"]);

        var page = (await Imports.GetHistoryAsync(0, 1, default)).Value;
        var detail = (await Imports.GetBatchAsync(second.BatchId, default)).Value;

        Assert.Equal(2, page.Total);
        Assert.Equal(second.BatchId, page.Items[0].BatchId);
        Assert.Equal("Validated", page.Items[0].Status.ToString());
        Assert.Equal(host.User.DisplayName, page.Items[0].UploadedBy);
        Assert.Equal("The name is not accepted.", detail.Issues.Single().Message);
        Assert.NotEqual(first.BatchId, second.BatchId);
        Assert.Equal(ErrorType.NotFound, (await Imports.GetBatchAsync(Guid.NewGuid().ToString(), default)).Error.Type);
    }

    [Fact]
    public async Task The_error_report_lists_every_issue_with_row_field_value_and_message()
    {
        var preview = await ValidateAsync(ImportStrategy.InsertOnly, ["A1", "Fine"], [null, "x"], ["C1", "BAD"]);

        var report = (await Imports.GetErrorReportAsync(preview.BatchId, default)).Value;

        Assert.EndsWith(".xlsx", report.FileName, StringComparison.Ordinal);
        Assert.Equal(ExcelContentTypes.Xlsx, report.ContentType);
        var issues = writer.ReportedIssues!;
        Assert.Equal([3, 4], issues.Select(i => i.RowNumber));
        Assert.Equal("Code", issues[0].Field);
        Assert.Equal("Code is required.", issues[0].Message);
        Assert.Equal("BAD", issues[1].Value);
        Assert.All(issues, i => Assert.Equal(ImportSeverity.Error, i.Severity));
    }

    // ---- Export ----

    [Fact]
    public async Task Export_checks_the_request_before_it_reads_anything()
    {
        Assert.Equal(ErrorType.NotFound, (await Exports.ExportAsync("nope", new ExportRequest(), default)).Error.Type);
        Assert.Equal(
            "Select at least one record to export.",
            (await Exports.ExportAsync("items", new ExportRequest(ExportScope.Selected), default)).Error.Message);
        Assert.Equal(
            "The start date must not be after the end date.",
            (await Exports.ExportAsync("items", new ExportRequest(DateFrom: new DateOnly(2026, 2, 1), DateTo: new DateOnly(2026, 1, 1)), default)).Error.Message);
        Assert.Equal(
            "Choose at least one column to export.",
            (await Exports.ExportAsync("items", new ExportRequest(ColumnKeys: ["nope"]), default)).Error.Message);
    }

    [Fact]
    public async Task Export_returns_the_file_logs_the_export_and_refuses_more_rows_than_the_limit()
    {
        module.ExportData.Add(new Dictionary<string, object?> { ["code"] = "A1", ["name"] = "One" });

        var file = (await Exports.ExportAsync("items", new ExportRequest(), default)).Value;

        Assert.StartsWith("items-export-", file.FileName, StringComparison.Ordinal);
        Assert.Equal(["Code", "Name"], writer.ExportedMain!.Columns.Select(c => c.Header));
        Assert.Equal(["Exported 1 Items record(s) to Excel"], host.ActivityLog.Messages);

        using var small = new TestHost(services =>
        {
            services.AddSingleton<IWorkbookWriter>(writer);
            services.AddSingleton<IExcelModule>(module);
            services.AddSingleton(new ExcelLimits { MaxExportRows = 1 });
        });
        module.ExportData.Add(new Dictionary<string, object?> { ["code"] = "A2", ["name"] = "Two" });
        var tooMany = await small.Get<IExcelExportService>().ExportAsync("items", new ExportRequest(), default);
        Assert.Equal("The export would contain more than 1 rows. Narrow the filters or the date range.", tooMany.Error.Message);
    }
}
