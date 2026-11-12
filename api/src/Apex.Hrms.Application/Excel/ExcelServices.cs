using System.Globalization;
using System.Text.Json;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Activity;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Excel;

namespace Apex.Hrms.Application.Excel;

public interface IExcelCatalogService
{
    Result<ExcelCatalogDto> GetModules();

    /// <summary>The module with that code, for the policies the API layer checks; null when unknown.</summary>
    IExcelModule? Find(string code);
}

public interface IExcelImportService
{
    Task<Result<ExcelFileResult>> GetTemplateAsync(string moduleCode, CancellationToken cancellationToken);

    Task<Result<FileInspectionDto>> InspectAsync(string moduleCode, string fileName, byte[] content, CancellationToken cancellationToken);

    Task<Result<ImportPreviewDto>> ValidateAsync(string moduleCode, ImportValidateRequest request, CancellationToken cancellationToken);

    Task<Result<ImportResultDto>> ExecuteAsync(string batchId, ImportMode mode, CancellationToken cancellationToken);

    Task<Result<PagedResult<ImportBatchSummaryDto>>> GetHistoryAsync(int pageIndex, int pageSize, CancellationToken cancellationToken);

    Task<Result<ImportBatchDetailDto>> GetBatchAsync(string batchId, CancellationToken cancellationToken);

    Task<Result<ExcelFileResult>> GetErrorReportAsync(string batchId, CancellationToken cancellationToken);

    /// <summary>The module code of a batch, for the policies the API layer checks.</summary>
    Task<Result<string>> ModuleOfBatchAsync(string batchId, CancellationToken cancellationToken);
}

public interface IExcelExportService
{
    Task<Result<ExcelFileResult>> ExportAsync(string moduleCode, ExportRequest request, CancellationToken cancellationToken);
}

internal sealed class ExcelCatalogService(IEnumerable<IExcelModule> modules) : IExcelCatalogService
{
    private static readonly ExcelModuleAvailability[] Unavailable =
    [
        .. new[]
        {
            ("companies", "Companies"), ("branches", "Branches"), ("departments", "Departments"), ("sections", "Sections"),
            ("job-grades", "Job grades"), ("positions", "Positions"), ("leave-types", "Leave types"),
            ("leave-policies", "Leave policies"), ("shifts", "Shifts"), ("holidays", "Holidays"),
            ("attendance-records", "Attendance records"), ("applications", "Applications"),
        }.Select(m => new ExcelModuleAvailability(
            m.Item1,
            m.Item2,
            CanImport: false,
            CanExport: false,
            "The frontend Excel module catalog defines no columns for this module (only Employees), so there is no specification to port.")),
    ];

    public Result<ExcelCatalogDto> GetModules() =>
        Result.Success(new ExcelCatalogDto([.. modules.Select(m => m.Info)], Unavailable));

    public IExcelModule? Find(string code) =>
        modules.FirstOrDefault(m => string.Equals(m.Info.Code, code, StringComparison.OrdinalIgnoreCase));
}

internal sealed class ExcelImportService(
    IEnumerable<IExcelModule> modules,
    ExcelLimits limits,
    IWorkbookReader reader,
    IWorkbookWriter writer,
    IFileStorage storage,
    IImportBatchStore batches,
    IRepository<Apex.Hrms.Domain.Security.AppAccount> accounts,
    IRepository<Apex.Hrms.Domain.Security.AppRole> roles,
    IBatchTransactionFactory transactions,
    IActivityMute mute,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    IClock clock) : IExcelImportService
{
    private const int MaxStoredIssues = 2000;
    private const string Savepoint = "excel_row";

    private sealed record Planned(int RowNumber, ParsedRow Values, Dictionary<string, string> Texts, List<ImportIssueDto> Issues, ImportRowStatus? Blocked);

    private sealed record Outcome(Planned Row, ImportRowStatus Status, ImportRowAction Action, List<ImportIssueDto> Issues);

    public async Task<Result<ExcelFileResult>> GetTemplateAsync(string moduleCode, CancellationToken cancellationToken)
    {
        var module = Module(moduleCode);
        if (module.IsFailure)
        {
            return Result.Failure<ExcelFileResult>(module.Error);
        }

        var reference = await module.Value.ReferenceSheetsAsync(cancellationToken);
        var content = writer.BuildTemplate(module.Value.Info, reference, limits);
        return Result.Success(new ExcelFileResult($"{module.Value.Info.Code}-import-template.xlsx", ExcelContentTypes.Xlsx, content));
    }

    public async Task<Result<FileInspectionDto>> InspectAsync(string moduleCode, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var module = Module(moduleCode);
        if (module.IsFailure)
        {
            return Result.Failure<FileInspectionDto>(module.Error);
        }

        var checkedFile = ExcelFileChecks.Check(fileName, content, limits);
        if (checkedFile.IsFailure)
        {
            return Result.Failure<FileInspectionDto>(checkedFile.Error);
        }

        var info = module.Value.Info;
        var read = reader.Read(content, info.SheetName, limits);
        if (read.IsFailure)
        {
            return Result.Failure<FileInspectionDto>(read.Error);
        }

        var sheet = read.Value;
        if (sheet.Rows.Count == 0)
        {
            var message = $"The worksheet \"{sheet.Name}\" has a header row but no data rows.";
            return Result.Failure<FileInspectionDto>(AppError.Validation(message, "file", message));
        }

        var headers = sheet.Headers.Where(h => h.Length > 0).ToList();
        var match = ExcelCoercion.MatchHeaders(headers, info.Columns);
        await storage.PurgeExpiredAsync(cancellationToken);
        var uploadId = Guid.NewGuid().ToString("N");
        await storage.SaveAsync(
            uploadId,
            content,
            new Dictionary<string, string>
            {
                ["module"] = info.Code,
                ["fileName"] = fileName,
                ["userId"] = user.UserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            },
            TimeSpan.FromMinutes(limits.UploadMinutes),
            cancellationToken);

        return Result.Success(new FileInspectionDto(
            uploadId,
            info.Code,
            fileName,
            content.Length,
            sheet.Name,
            sheet.SheetCount,
            sheet.Rows.Count,
            headers,
            match.Mapping,
            match.MissingRequired,
            match.UnknownHeaders,
            match.AmbiguousHeaders,
            match.MissingRequired.Count > 0 || match.AmbiguousHeaders.Count > 0));
    }

    public async Task<Result<ImportPreviewDto>> ValidateAsync(string moduleCode, ImportValidateRequest request, CancellationToken cancellationToken)
    {
        var module = Module(moduleCode);
        if (module.IsFailure)
        {
            return Result.Failure<ImportPreviewDto>(module.Error);
        }

        var info = module.Value.Info;
        var stored = Guid.TryParseExact(request.UploadId, "N", out _) ? await storage.ReadAsync(request.UploadId, cancellationToken) : null;
        if (stored is null || !OwnedByCaller(stored) || stored.Metadata.GetValueOrDefault("module") != info.Code)
        {
            return Result.Failure<ImportPreviewDto>(AppError.NotFound("The uploaded file is no longer available. Please upload it again."));
        }

        var read = reader.Read(stored.Content, info.SheetName, limits);
        if (read.IsFailure)
        {
            return Result.Failure<ImportPreviewDto>(read.Error);
        }

        var sheet = read.Value;
        var mapping = request.Mapping ?? ExcelCoercion.MatchHeaders([.. sheet.Headers.Where(h => h.Length > 0)], info.Columns).Mapping;
        var problems = ExcelCoercion.MappingProblems(mapping, info.Columns);
        if (problems.Count > 0)
        {
            var message = string.Join(' ', problems);
            return Result.Failure<ImportPreviewDto>(AppError.Validation(message, "mapping", message));
        }

        var plan = Plan(info, sheet, mapping);
        var importer = module.Value.CreateImporter();
        List<Outcome> outcomes;
        await using (var transaction = await transactions.BeginAsync(cancellationToken))
        {
            using var silence = mute.Mute();
            await importer.PrepareAsync(cancellationToken);

            // The rows are applied for real so the rules are exactly those of the API, then everything is rolled back.
            outcomes = await RunAsync(info, plan, request.Strategy, importer, transaction, rollbackFailed: true, cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
        }

        var rows = outcomes.Select(o => new ImportPreviewRowDto(o.Row.RowNumber, o.Row.Texts, o.Status, o.Action, o.Issues)).ToList();
        int Count(ImportRowStatus s) => rows.Count(r => r.Status == s);
        var valid = Count(ImportRowStatus.Valid) + Count(ImportRowStatus.Warning);
        var invalid = Count(ImportRowStatus.Invalid) + Count(ImportRowStatus.Duplicate);

        var batchId = Guid.CreateVersion7();
        var batch = ImportBatch.Validated(
            batchId,
            info.Code,
            info.Label,
            stored.Metadata.GetValueOrDefault("fileName") ?? "upload.xlsx",
            request.Strategy,
            user.UserId,
            user.DisplayName,
            clock.UtcNow.UtcDateTime,
            rows.Count,
            valid,
            invalid);
        foreach (var outcome in outcomes)
        {
            foreach (var issue in outcome.Issues.Take(Math.Max(0, MaxStoredIssues - batch.Issues.Count)))
            {
                batch.AddIssue(ImportIssue.Create(outcome.Row.RowNumber, issue.Field, issue.Value, issue.Message, issue.Suggestion, issue.Severity));
            }
        }

        batches.Add(batch);
        var metadata = new Dictionary<string, string>(stored.Metadata)
        {
            ["strategy"] = request.Strategy.ToString(),
            ["mapping"] = JsonSerializer.Serialize(mapping),
        };
        await storage.SaveAsync(batchId.ToString("N"), stored.Content, metadata, TimeSpan.FromMinutes(limits.UploadMinutes), cancellationToken);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            await storage.DeleteAsync(batchId.ToString("N"), cancellationToken);
            return Result.Failure<ImportPreviewDto>(saved.Error);
        }

        return Result.Success(new ImportPreviewDto(
            batchId.ToString(),
            info.Code,
            batch.FileName,
            sheet.Name,
            request.Strategy,
            rows.Count,
            valid,
            Count(ImportRowStatus.Invalid),
            Count(ImportRowStatus.Duplicate),
            Count(ImportRowStatus.Warning),
            rows));
    }

    public async Task<Result<ImportResultDto>> ExecuteAsync(string batchId, ImportMode mode, CancellationToken cancellationToken)
    {
        var found = await FindBatchAsync(batchId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ImportResultDto>(found.Error);
        }

        var batch = found.Value;
        var module = Module(batch.ModuleCode);
        if (module.IsFailure)
        {
            return Result.Failure<ImportResultDto>(module.Error);
        }

        if (batch.Status != ImportBatchStatus.Validated)
        {
            return Result.Failure<ImportResultDto>(AppError.Conflict("This import has already been processed. Upload the file again to import it again."));
        }

        var blocked = batch.InvalidRows;
        if (mode == ImportMode.AllOrNothing && blocked > 0)
        {
            return Result.Failure<ImportResultDto>(AppError.Conflict(
                $"{blocked} row(s) have problems, so an all-or-nothing import cannot run. Fix the file, or import only the valid rows."));
        }

        if (batch.ValidRows == 0)
        {
            return Result.Failure<ImportResultDto>(AppError.Conflict("There are no valid rows to import."));
        }

        var key = batch.Id.ToString("N");
        var stored = await storage.ReadAsync(key, cancellationToken);
        if (stored is null || !OwnedByCaller(stored))
        {
            return Result.Failure<ImportResultDto>(AppError.NotFound("The uploaded file is no longer available. Please upload it again."));
        }

        var info = module.Value.Info;
        var read = reader.Read(stored.Content, info.SheetName, limits);
        if (read.IsFailure)
        {
            return Result.Failure<ImportResultDto>(read.Error);
        }

        var mapping = JsonSerializer.Deserialize<Dictionary<string, string?>>(stored.Metadata.GetValueOrDefault("mapping") ?? "{}") ?? [];
        var strategy = batch.Strategy;
        var fileName = batch.FileName;
        var label = batch.ModuleLabel;
        var plan = Plan(info, read.Value, mapping);
        var importer = module.Value.CreateImporter();
        var started = clock.UtcNow.UtcDateTime;
        List<Outcome> outcomes;
        await using (var transaction = await transactions.BeginAsync(cancellationToken))
        {
            using var silence = mute.Mute();
            await importer.PrepareAsync(cancellationToken);
            outcomes = await RunAsync(info, plan, strategy, importer, transaction, rollbackFailed: true, cancellationToken);
            var failedRows = outcomes.Count(o => o.Status == ImportRowStatus.Invalid || o.Status == ImportRowStatus.Duplicate);
            if (mode == ImportMode.AllOrNothing && failedRows > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            else
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        var failed = outcomes.Count(o => o.Status is ImportRowStatus.Invalid or ImportRowStatus.Duplicate);
        var rolledBack = mode == ImportMode.AllOrNothing && failed > 0;
        var inserted = rolledBack ? 0 : outcomes.Count(o => o.Status is ImportRowStatus.Valid or ImportRowStatus.Warning && o.Action == ImportRowAction.Insert);
        var updated = rolledBack ? 0 : outcomes.Count(o => o.Status is ImportRowStatus.Valid or ImportRowStatus.Warning && o.Action == ImportRowAction.Update);
        var skipped = mode == ImportMode.Partial ? Math.Min(blocked, failed) : 0;
        var failedAtRun = rolledBack ? failed : Math.Max(0, failed - skipped);
        var status = rolledBack ? ImportBatchStatus.Failed : failedAtRun > 0 || skipped > 0 ? ImportBatchStatus.CompletedWithErrors : ImportBatchStatus.Completed;
        var message = rolledBack
            ? $"Nothing was imported: {failedAtRun} row(s) failed, so the whole import was rolled back."
            : $"{inserted} inserted, {updated} updated{(skipped > 0 ? $", {skipped} skipped" : string.Empty)}{(failedAtRun > 0 ? $", {failedAtRun} failed" : string.Empty)}.";

        // The transaction has ended (committed or rolled back): record the outcome on its own, so a rolled back
        // import still leaves its batch row and its activity entry.
        var tracked = await batches.FindAsync(batch.Id, cancellationToken);
        if (tracked is null)
        {
            return Result.Failure<ImportResultDto>(AppError.NotFound("This import was not found."));
        }

        var existingRows = tracked.Issues.Select(i => i.RowNumber).ToHashSet();
        foreach (var outcome in outcomes.Where(o => o.Status is ImportRowStatus.Invalid or ImportRowStatus.Duplicate && !existingRows.Contains(o.Row.RowNumber)))
        {
            foreach (var issue in outcome.Issues.Take(Math.Max(0, MaxStoredIssues - tracked.Issues.Count)))
            {
                tracked.AddIssue(ImportIssue.Create(outcome.Row.RowNumber, issue.Field, issue.Value, issue.Message, issue.Suggestion, issue.Severity));
            }
        }

        var completed = tracked.Complete(
            mode,
            status,
            started,
            clock.UtcNow.UtcDateTime,
            inserted + updated,
            failedAtRun,
            skipped,
            failedAtRun > 0 || skipped > 0 ? message : null);
        if (completed.IsFailure)
        {
            return Result.Failure<ImportResultDto>(completed.Error);
        }

        activity.Record(ActivityCategories.System, $"Excel import of {label} ({fileName}): {message}");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<ImportResultDto>(saved.Error);
        }

        await storage.DeleteAsync(key, cancellationToken);
        return Result.Success(new ImportResultDto(batch.Id.ToString(), status, mode, inserted, updated, skipped, failedAtRun, rolledBack, message));
    }

    public async Task<Result<PagedResult<ImportBatchSummaryDto>>> GetHistoryAsync(int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        // Only an HR Administrator sees the batches of other users.
        int? onlyOwn = await IsAdministratorAsync(cancellationToken) ? null : user.UserId ?? -1;
        var page = await batches.QueryAsync(Math.Max(0, pageIndex), Paging.ClampPageSize(pageSize), onlyOwn, cancellationToken);
        return Result.Success(page.Map(Summary));
    }

    public async Task<Result<ImportBatchDetailDto>> GetBatchAsync(string batchId, CancellationToken cancellationToken)
    {
        var found = await FindBatchAsync(batchId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ImportBatchDetailDto>(found.Error);
        }

        var s = Summary(found.Value);
        return Result.Success(new ImportBatchDetailDto(
            s.BatchId, s.ModuleCode, s.ModuleLabel, s.FileName, s.Strategy, s.Mode, s.Status, s.UploadedBy, s.UploadedAt,
            s.StartedAt, s.CompletedAt, s.TotalRows, s.ValidRows, s.InvalidRows, s.SuccessRows, s.FailedRows, s.SkippedRows,
            s.ErrorSummary, IssueRows(found.Value)));
    }

    public async Task<Result<ExcelFileResult>> GetErrorReportAsync(string batchId, CancellationToken cancellationToken)
    {
        var found = await FindBatchAsync(batchId, cancellationToken);
        if (found.IsFailure)
        {
            return Result.Failure<ExcelFileResult>(found.Error);
        }

        var batch = found.Value;
        var content = writer.BuildErrorReport(batch.ModuleLabel, IssueRows(batch));
        return Result.Success(new ExcelFileResult($"{batch.ModuleCode}-import-report-{batch.Id}.xlsx", ExcelContentTypes.Xlsx, content));
    }

    public async Task<Result<string>> ModuleOfBatchAsync(string batchId, CancellationToken cancellationToken)
    {
        var found = await FindBatchAsync(batchId, cancellationToken);
        return found.IsFailure ? Result.Failure<string>(found.Error) : Result.Success(found.Value.ModuleCode);
    }

    // ---- Planning and running ----

    private static List<Planned> Plan(ExcelModuleInfo info, ParsedSheet sheet, IReadOnlyDictionary<string, string?> mapping)
    {
        var columnIndex = new Dictionary<string, int>();
        for (var i = 0; i < sheet.Headers.Count; i++)
        {
            if (mapping.TryGetValue(sheet.Headers[i], out var key) && key is not null)
            {
                columnIndex[key] = i;
            }
        }

        var planned = new List<Planned>();
        var firstRowOfKey = new Dictionary<string, int>();
        foreach (var sheetRow in sheet.Rows)
        {
            var values = new ParsedRow();
            var texts = new Dictionary<string, string>();
            var issues = new List<ImportIssueDto>();
            foreach (var column in info.Columns)
            {
                var cell = columnIndex.TryGetValue(column.Key, out var index) && index < sheetRow.Cells.Count
                    ? sheetRow.Cells[index]
                    : new RawCell(null, false);
                var coerced = ExcelCoercion.Coerce(column, cell);
                values[column.Key] = coerced.Value;
                var shown = coerced.Error is not null || coerced.Value is null ? coerced.Text : Convert.ToString(coerced.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                texts[column.Key] = ExcelCoercion.Shown(column, shown);
                if (coerced.Error is not null)
                {
                    issues.Add(new ImportIssueDto(column.Header, Shorten(texts[column.Key]), coerced.Error, coerced.Suggestion, ImportSeverity.Error));
                }
            }

            ImportRowStatus? blocked = null;
            var key = ExcelCoercion.MatchKeyOf(values, info.MatchKey);
            int? earlier = key is not null && firstRowOfKey.TryGetValue(key, out var first) ? first : null;
            if (key is not null && earlier is null)
            {
                firstRowOfKey[key] = sheetRow.RowNumber;
            }

            if (earlier is { } earlierRow)
            {
                blocked = ImportRowStatus.Duplicate;
                issues.Add(new ImportIssueDto(
                    info.MatchKeyLabel,
                    Shorten(string.Join(" / ", info.MatchKey.Select(k => texts[k]))),
                    $"Duplicate of row {earlierRow} in this file (same {info.MatchKeyLabel}).",
                    "Remove one of the duplicate rows.",
                    ImportSeverity.Error));
            }
            else if (issues.Count > 0)
            {
                blocked = ImportRowStatus.Invalid;
            }

            planned.Add(new Planned(sheetRow.RowNumber, values, texts, issues, blocked));
        }

        return planned;
    }

    private static async Task<List<Outcome>> RunAsync(
        ExcelModuleInfo info,
        List<Planned> plan,
        ImportStrategy strategy,
        IExcelImportAdapter importer,
        IBatchTransaction transaction,
        bool rollbackFailed,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<Outcome>();
        foreach (var row in plan)
        {
            var issues = new List<ImportIssueDto>(row.Issues);
            if (row.Blocked is { } status)
            {
                outcomes.Add(new Outcome(row, status, ImportRowAction.None, issues));
                continue;
            }

            var exists = await importer.ExistsAsync(row.Values, cancellationToken);
            var keyText = Shorten(string.Join(" / ", info.MatchKey.Select(k => row.Texts[k])));
            if (strategy == ImportStrategy.InsertOnly && exists)
            {
                issues.Add(new ImportIssueDto(
                    info.MatchKeyLabel,
                    keyText,
                    $"A record with this {info.MatchKeyLabel} already exists.",
                    "Use \"Insert or update\" to update existing records.",
                    ImportSeverity.Error));
                outcomes.Add(new Outcome(row, ImportRowStatus.Duplicate, ImportRowAction.None, issues));
                continue;
            }

            if (strategy == ImportStrategy.UpdateOnly && !exists)
            {
                issues.Add(new ImportIssueDto(
                    info.MatchKeyLabel,
                    keyText,
                    $"No existing record has this {info.MatchKeyLabel}, so it cannot be updated.",
                    "Use \"Insert or update\" to create new records.",
                    ImportSeverity.Error));
                outcomes.Add(new Outcome(row, ImportRowStatus.Invalid, ImportRowAction.None, issues));
                continue;
            }

            await transaction.SavepointAsync(Savepoint, cancellationToken);
            var applied = await importer.ApplyAsync(row.Values, exists, cancellationToken);
            issues.AddRange(applied);
            var rejected = applied.Any(i => i.Severity == ImportSeverity.Error);
            if (rejected && rollbackFailed)
            {
                await transaction.RollbackToSavepointAsync(Savepoint, cancellationToken);
            }

            transaction.ClearTracked();
            var rowStatus = rejected ? ImportRowStatus.Invalid : applied.Any(i => i.Severity == ImportSeverity.Warning) ? ImportRowStatus.Warning : ImportRowStatus.Valid;
            outcomes.Add(new Outcome(row, rowStatus, rejected ? ImportRowAction.None : exists ? ImportRowAction.Update : ImportRowAction.Insert, issues));
        }

        return outcomes;
    }

    // ---- Helpers ----

    private Result<IExcelModule> Module(string code)
    {
        var module = modules.FirstOrDefault(m => string.Equals(m.Info.Code, code, StringComparison.OrdinalIgnoreCase));
        return module is { Info.CanImport: true }
            ? Result.Success(module)
            : Result.Failure<IExcelModule>(AppError.NotFound($"Import is not available for \"{code}\"."));
    }

    private bool OwnedByCaller(StoredFile file) =>
        file.Metadata.GetValueOrDefault("userId", string.Empty) == (user.UserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

    private async Task<bool> CanSeeAsync(ImportBatch batch, CancellationToken cancellationToken) =>
        (user.UserId is { } own && batch.UploadedByUserId == own) || await IsAdministratorAsync(cancellationToken);

    private Task<bool> IsAdministratorAsync(CancellationToken cancellationToken) =>
        Apex.Hrms.Application.Settings.UserRules.IsAdministratorAsync(accounts, roles, user, cancellationToken);

    private async Task<Result<ImportBatch>> FindBatchAsync(string batchId, CancellationToken cancellationToken)
    {
        var batch = Guid.TryParse(batchId, out var id) ? await batches.FindAsync(id, cancellationToken) : null;
        if (batch is not null && !await CanSeeAsync(batch, cancellationToken))
        {
            // Another user's batch is not visible: it looks like a batch that does not exist.
            batch = null;
        }

        return batch is null
            ? Result.Failure<ImportBatch>(AppError.NotFound("This import was not found."))
            : Result.Success(batch);
    }

    private static string Shorten(string value) => value.Length > 60 ? value[..60] + "…" : value;

    private static List<ImportIssueRow> IssueRows(ImportBatch batch) =>
        [.. batch.Issues.OrderBy(i => i.RowNumber).ThenBy(i => i.Id)
            .Select(i => new ImportIssueRow(i.RowNumber, i.Field, i.Value, i.Message, i.Suggestion, i.Severity))];

    private static ImportBatchSummaryDto Summary(ImportBatch b) => new(
        b.Id.ToString(),
        b.ModuleCode,
        b.ModuleLabel,
        b.FileName,
        b.Strategy,
        b.Mode,
        b.Status,
        b.UploadedByName,
        DateTime.SpecifyKind(b.UploadedAtUtc, DateTimeKind.Utc),
        b.StartedAtUtc is { } s ? DateTime.SpecifyKind(s, DateTimeKind.Utc) : null,
        b.CompletedAtUtc is { } c ? DateTime.SpecifyKind(c, DateTimeKind.Utc) : null,
        b.TotalRows,
        b.ValidRows,
        b.InvalidRows,
        b.SuccessRows,
        b.FailedRows,
        b.SkippedRows,
        b.ErrorSummary ?? string.Empty);
}

internal sealed class ExcelExportService(
    IEnumerable<IExcelModule> modules,
    ExcelLimits limits,
    IWorkbookWriter writer,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork,
    IClock clock) : IExcelExportService
{
    public async Task<Result<ExcelFileResult>> ExportAsync(string moduleCode, ExportRequest request, CancellationToken cancellationToken)
    {
        var module = modules.FirstOrDefault(m => string.Equals(m.Info.Code, moduleCode, StringComparison.OrdinalIgnoreCase));
        if (module is not { Info.CanExport: true })
        {
            return Result.Failure<ExcelFileResult>(AppError.NotFound($"Export is not available for \"{moduleCode}\"."));
        }

        var info = module.Info;
        if (request.Scope == ExportScope.Selected && request.SelectedIds is not { Count: > 0 })
        {
            return Invalid("Select at least one record to export.", "selectedIds");
        }

        if (request.DateFrom is { } from && request.DateTo is { } to && from > to)
        {
            return Invalid("The start date must not be after the end date.", "dateFrom");
        }

        var importLayout = string.Equals(request.Layout, "template", StringComparison.OrdinalIgnoreCase);
        var wanted = request.ColumnKeys is { Count: > 0 } ? request.ColumnKeys.ToHashSet() : null;
        var columns = (importLayout ? info.Columns : info.ExportColumns ?? info.Columns)
            .Where(c => (wanted is null || wanted.Contains(c.Key)) && (!c.Sensitive || request.IncludeSensitive))
            .ToList();
        if (columns.Count == 0)
        {
            return Invalid("Choose at least one column to export.", "columnKeys");
        }

        var rows = await module.ExportRowsAsync(request, importLayout, limits.MaxExportRows, cancellationToken);
        if (rows.IsFailure)
        {
            return Result.Failure<ExcelFileResult>(rows.Error);
        }

        if (rows.Value.Count > limits.MaxExportRows)
        {
            return Invalid(
                $"The export would contain more than {limits.MaxExportRows.ToString("N0", CultureInfo.InvariantCulture)} rows. Narrow the filters or the date range.",
                "scope");
        }

        var extra = importLayout ? [] : module.ExtraSheets(rows.Value);
        var content = writer.BuildExport($"{info.Label} export", new ExportSheet(info.SheetName, columns, rows.Value), extra);

        activity.Record(ActivityCategories.System, $"Exported {rows.Value.Count} {info.Label} record(s) to Excel");
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return Result.Failure<ExcelFileResult>(saved.Error);
        }

        var now = clock.UtcNow;
        return Result.Success(new ExcelFileResult(
            $"{info.Code}-export-{now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.xlsx",
            ExcelContentTypes.Xlsx,
            content));
    }

    private static Result<ExcelFileResult> Invalid(string message, string field) =>
        Result.Failure<ExcelFileResult>(AppError.Validation(message, field, message));
}
