using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Performance.Scorecards;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Performance;
using Microsoft.Extensions.DependencyInjection;
using static Apex.Hrms.Application.Excel.ExcelModuleSupport;

namespace Apex.Hrms.Application.Performance.Insights;

// KPI data in Excel is export only: scorecards change through the planning and review workflow, never by upload. The three modules
// read the same visible scorecards as the KPI reports (ScorecardScope: own, team or all), with the same filters (KpiReportFilter, sent
// as the export's "filters"), the search on employee code or name, and the export's date range (periods that overlap it). "Selected"
// exports take scorecard ids. No module writes a comment, a manager rating or an evidence file: only the employee code and name
// identify a person. Each needs excel:Export (the route) and kpi:View (the module's own view permission).

internal static class KpiExcelRegistration
{
    public static IServiceCollection AddKpiExcel(this IServiceCollection services)
    {
        services.AddScoped<IExcelModule, KpiScorecardExcelModule>();
        services.AddScoped<IExcelModule, KpiRankingExcelModule>();
        services.AddScoped<IExcelModule, KpiTargetVsActualExcelModule>();
        return services;
    }
}

/// <summary>What the three KPI exports share: the policies, no import, and the scorecards an export request selects.</summary>
internal abstract class KpiExportModule(KpiReportSource source) : IExcelModule
{
    public abstract ExcelModuleInfo Info { get; }

    public IReadOnlyList<string> ImportPolicies { get; } = [];

    public IReadOnlyList<string> ExportPolicies { get; } = ["kpi:View"];

    public IReadOnlyList<string> SensitiveExportPolicies { get; } = [];

    protected KpiReportSource Source => source;

    public Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) => ExportOnly.NoReferences();

    public IExcelImportAdapter CreateImporter() => ExportOnly.Refuse();

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => [];

    public abstract Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// The visible scorecards of the request: Filtered applies the filters, the search and the date range; All applies the filters but
    /// not the search or the date range; Selected takes the given scorecard ids and nothing else (an id outside the caller's scope is
    /// silently left out, as in the other exports). Every scope starts from the caller's visible scorecards. Also returns the category
    /// filter (not for Selected), which the item export applies to the KPIs themselves.
    /// </summary>
    protected Result<(IQueryable<Scorecard> Scorecards, int? CategoryId)> Select(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Scope == ExportScope.Selected)
        {
            int[] ids = [.. (request.SelectedIds ?? []).Distinct()];
            return Result.Success((source.Visible().Where(scorecard => ids.Contains(scorecard.Id)), (int?)null));
        }

        var filter = ParseFilter<KpiReportFilter>(request);
        if (filter.IsFailure)
        {
            return Result.Failure<(IQueryable<Scorecard>, int?)>(filter.Error);
        }

        var filtered = request.Scope == ExportScope.Filtered;
        var scorecards = source.Filtered(
            filter.Value,
            filtered ? request.Search : null,
            filtered ? request.DateFrom : null,
            filtered ? request.DateTo : null);
        return scorecards.IsFailure
            ? Result.Failure<(IQueryable<Scorecard>, int?)>(scorecards.Error)
            : Result.Success((scorecards.Value, filter.Value.CategoryId));
    }

    protected static Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>> Failure(AppError error) =>
        Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(error);
}

/// <summary>The scorecard list: one row per scorecard, newest period first, then employee code.</summary>
internal sealed class KpiScorecardExcelModule(KpiReportSource source, ScorecardReader reader) : KpiExportModule(source)
{
    public static ExcelModuleInfo Definition { get; } = ExportOnly.Info(
        "kpi-scorecards",
        "KPI scorecards",
        "Export the KPI scorecards you are allowed to see (your own, your team's, or everyone's, as on the KPI screens), with the same filters as the KPI reports. Comments and evidence files are never exported. Import is not available.",
        [
            Out("employeeCode", "Employee Code", width: 14),
            Out("employeeName", "Employee Name", width: 26),
            Out("department", "Department", width: 24),
            Out("period", "Period", width: 20),
            Out("status", "Status", width: 14),
            Out("itemCount", "KPIs", ExcelColumnType.Integer),
            Out("totalWeight", "Total Weight", ExcelColumnType.Decimal),
            Out("overallScore", "Overall Score", ExcelColumnType.Decimal),
            Out("ratingLabel", "Rating", width: 22),
            Out("reviewer", "Reviewer", width: 26),
            Out("planFinalizedOn", "Plan Finalized On", ExcelColumnType.Date),
            Out("submittedOn", "Submitted On", ExcelColumnType.Date),
            Out("submissionDueDate", "Submission Due", ExcelColumnType.Date),
            Out("finalizedOn", "Finalized On", ExcelColumnType.Date),
        ],
        "Period dates");

    public override ExcelModuleInfo Info => Definition;

    public override async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var selected = Select(request);
        if (selected.IsFailure)
        {
            return Failure(selected.Error);
        }

        var rows = await Source.Read.ToListAsync(
            reader.Rows(selected.Value.Scorecards)
                .OrderByDescending(row => row.PeriodStartDate)
                .ThenBy(row => row.EmployeeCode)
                .ThenBy(row => row.Id)
                .Take(limit + 1),
            cancellationToken);
        var reviewers = await reader.ReviewerNamesAsync(rows, cancellationToken);

        return Result.Success(ExcelRows.Of(rows.Select(row => new Dictionary<string, object?>
        {
            ["employeeCode"] = row.EmployeeCode,
            ["employeeName"] = ScorecardMapping.FullName(row.FirstName, row.LastName),
            ["department"] = row.DepartmentName,
            ["period"] = row.PeriodName,
            ["status"] = ScorecardStatusText.ToText(row.Status),
            ["itemCount"] = row.ItemCount,
            ["totalWeight"] = row.TotalWeight,
            ["overallScore"] = row.OverallScore,
            ["ratingLabel"] = row.RatingLabel,
            ["reviewer"] = row.ReviewerEmployeeId is { } reviewer ? reviewers.GetValueOrDefault(reviewer) : null,
            ["planFinalizedOn"] = row.PlanFinalizedOn,
            ["submittedOn"] = row.SubmittedOn,
            ["submissionDueDate"] = row.SubmissionDueDate,
            ["finalizedOn"] = row.FinalizedOn,
        })));
    }
}

/// <summary>
/// The ranking: Finalized scorecards by overall score, equal scores sharing a rank (1, 2, 2, 4), then by employee code. The ranks are
/// over the exported rows (the filtered set, or the selected scorecards).
/// </summary>
internal sealed class KpiRankingExcelModule(KpiReportSource source) : KpiExportModule(source)
{
    public static ExcelModuleInfo Definition { get; } = ExportOnly.Info(
        "kpi-ranking",
        "KPI ranking",
        "Export the ranking of the finalized KPI scorecards you are allowed to see, highest overall score first; equal scores share a rank. Use the period filter for a ranking within one period. Import is not available.",
        [
            Out("rank", "Rank", ExcelColumnType.Integer),
            Out("employeeCode", "Employee Code", width: 14),
            Out("employeeName", "Employee Name", width: 26),
            Out("department", "Department", width: 24),
            Out("period", "Period", width: 20),
            Out("overallScore", "Overall Score", ExcelColumnType.Decimal),
            Out("ratingLabel", "Rating", width: 22),
        ],
        "Period dates");

    public override ExcelModuleInfo Info => Definition;

    public override async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var selected = Select(request);
        if (selected.IsFailure)
        {
            return Failure(selected.Error);
        }

        var rows = await Source.Read.ToListAsync(
            KpiRankingReportHandler.Order(Source.Rows(KpiRankingReportHandler.Ranked(selected.Value.Scorecards))).Take(limit + 1),
            cancellationToken);
        return Result.Success(ExcelRows.Of(KpiRankingReportHandler.ToDtos(rows, 1, 1).Select(row => new Dictionary<string, object?>
        {
            ["rank"] = row.Rank,
            ["employeeCode"] = row.EmployeeCode,
            ["employeeName"] = row.EmployeeName,
            ["department"] = row.DepartmentName,
            ["period"] = row.PeriodName,
            ["overallScore"] = row.OverallScore,
            ["ratingLabel"] = row.RatingLabel,
        })));
    }
}

/// <summary>Target versus actual: one row per KPI of the selected scorecards (of the filtered category only), every status.</summary>
internal sealed class KpiTargetVsActualExcelModule(KpiReportSource source) : KpiExportModule(source)
{
    public static ExcelModuleInfo Definition { get; } = ExportOnly.Info(
        "kpi-target-vs-actual",
        "KPI target vs actual",
        "Export each KPI of the scorecards you are allowed to see with its target, actual result, achievement, weight and weighted score. Comments and evidence files are never exported. Import is not available.",
        [
            Out("employeeCode", "Employee Code", width: 14),
            Out("employeeName", "Employee Name", width: 26),
            Out("department", "Department", width: 24),
            Out("period", "Period", width: 20),
            Out("status", "Status", width: 14),
            Out("kpiCode", "KPI Code", width: 14),
            Out("kpiName", "KPI", width: 30),
            Out("category", "Category", width: 20),
            Out("unit", "Unit", width: 10),
            Out("target", "Target", ExcelColumnType.Decimal),
            Out("actual", "Actual", ExcelColumnType.Decimal),
            Out("achievementPct", "Achievement %", ExcelColumnType.Decimal),
            Out("weight", "Weight", ExcelColumnType.Decimal),
            Out("weightedScore", "Weighted Score", ExcelColumnType.Decimal),
        ],
        "Period dates");

    public override ExcelModuleInfo Info => Definition;

    public override async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        var selected = Select(request);
        if (selected.IsFailure)
        {
            return Failure(selected.Error);
        }

        var rows = await Source.Read.ToListAsync(
            KpiTargetVsActualReportHandler.DefaultOrder(Source.ItemRows(selected.Value.Scorecards, selected.Value.CategoryId)).Take(limit + 1),
            cancellationToken);
        return Result.Success(ExcelRows.Of(rows.Select(row => new Dictionary<string, object?>
        {
            ["employeeCode"] = row.EmployeeCode,
            ["employeeName"] = ScorecardMapping.FullName(row.FirstName, row.LastName),
            ["department"] = row.DepartmentName,
            ["period"] = row.PeriodName,
            ["status"] = ScorecardStatusText.ToText(row.Status),
            ["kpiCode"] = row.KpiCode,
            ["kpiName"] = row.KpiName,
            ["category"] = row.CategoryName,
            ["unit"] = row.Unit,
            ["target"] = row.Target,
            ["actual"] = row.ActualValue,
            ["achievementPct"] = row.AchievementPct,
            ["weight"] = row.Weight,
            ["weightedScore"] = row.WeightedScore,
        })));
    }
}
