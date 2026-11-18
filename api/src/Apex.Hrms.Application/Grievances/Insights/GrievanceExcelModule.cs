using System.Globalization;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;
using static Apex.Hrms.Application.Excel.ExcelModuleSupport;

namespace Apex.Hrms.Application.Grievances.Insights;

/// <summary>
/// The case report in Excel (<c>grievance-cases</c>), export only, for <c>grievance-management:View</c> (and <c>excel:Export</c> at the
/// route). The same rows as <c>GET /api/grievances/reports/cases</c>: the report filters (Filtered scope), the case-number search and the
/// export's date range on the submission date; Selected takes case ids. Never the subject, description, notes or submitter; an
/// anonymous case's department is "Not disclosed". Every export is written to the grievance access log (Export, the row count only) and
/// leaves no line in the company-wide activity feed.
/// </summary>
internal sealed class GrievanceCasesExcelModule(GrievanceInsightSource source, GrievanceInsights insights, GrievanceAuditTrail audit) : IExcelModule
{
    public static ExcelModuleInfo Definition { get; } = ExportOnly.Info(
        "grievance-cases",
        "Grievance cases",
        "Export the grievance case report: case number, category, status, priority, department (Not disclosed for anonymous cases), dates, resolution days, overdue, lead investigator and escalation. Subjects, descriptions, notes and submitters are never exported. Every export is recorded in the grievance access log. Import is not available.",
        [
            Out("caseNo", "Case No", width: 16),
            Out("category", "Category", width: 26),
            Out("status", "Status", width: 22),
            Out("priority", "Priority", width: 10),
            Out("department", "Department", width: 24),
            Out("submittedOn", "Submitted", ExcelColumnType.Date),
            Out("targetResolutionDate", "Target", ExcelColumnType.Date),
            Out("resolvedOn", "Resolved", ExcelColumnType.Date),
            Out("resolutionDays", "Days", ExcelColumnType.Integer),
            Out("isOverdue", "Overdue", ExcelColumnType.Boolean),
            Out("leadInvestigator", "Lead Investigator", width: 26),
            Out("isEscalated", "Escalated", ExcelColumnType.Boolean),
        ],
        "Submission dates");

    public ExcelModuleInfo Info => Definition;

    public IReadOnlyList<string> ImportPolicies { get; } = [];

    public IReadOnlyList<string> ExportPolicies { get; } = ["grievance-management:View"];

    public IReadOnlyList<string> SensitiveExportPolicies { get; } = [];

    /// <summary>The access log records the export instead; the company-wide activity feed never mentions grievances.</summary>
    public bool RecordsActivity => false;

    public Task<IReadOnlyList<ExportSheet>> ReferenceSheetsAsync(CancellationToken cancellationToken) => ExportOnly.NoReferences();

    public IExcelImportAdapter CreateImporter() => ExportOnly.Refuse();

    public IReadOnlyList<ExportSheet> ExtraSheets(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) => [];

    public async Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> ExportRowsAsync(
        ExportRequest request,
        bool importLayout,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Result<List<GrievanceReportRowDto>> rows;
        if (request.Scope == ExportScope.Selected)
        {
            int[] ids = [.. (request.SelectedIds ?? []).Distinct()];
            rows = await insights.ReportRowsAsync(insights.ReportCases().Where(grievance => ids.Contains(grievance.Id)), new GrievanceReportFilter(), null, cancellationToken);
        }
        else
        {
            var filter = ParseFilter<GrievanceReportFilter>(request);
            if (filter.IsFailure)
            {
                return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(filter.Error);
            }

            var filtered = request.Scope == ExportScope.Filtered;
            var cases = filtered ? source.Submitted(insights.ReportCases(), request.DateFrom, request.DateTo) : insights.ReportCases();
            rows = await insights.ReportRowsAsync(cases, filter.Value, filtered ? request.Search : null, cancellationToken);
        }

        if (rows.IsFailure)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(rows.Error);
        }

        var exported = rows.Value.OrderByDescending(row => row.SubmittedOn).ThenBy(row => row.CaseNo, StringComparer.Ordinal).Take(limit + 1).ToList();

        // Staged now and saved by the export's own save, so the log has the export exactly when the file was produced.
        audit.Team(null, GrievanceAuditAction.Export, GrievanceActorRole.Management, string.Create(CultureInfo.InvariantCulture, $"module:grievance-cases;rows:{Math.Min(exported.Count, limit)}"));
        return Result.Success(ExcelRows.Of(exported.Select(row => new Dictionary<string, object?>
        {
            ["caseNo"] = row.CaseNo,
            ["category"] = row.CategoryName,
            ["status"] = row.Status,
            ["priority"] = row.Priority,
            ["department"] = row.DepartmentName,
            ["submittedOn"] = row.SubmittedOn,
            ["targetResolutionDate"] = row.TargetResolutionDate,
            ["resolvedOn"] = row.ResolvedOn,
            ["resolutionDays"] = row.ResolutionDays,
            ["isOverdue"] = row.IsOverdue,
            ["leadInvestigator"] = row.LeadInvestigatorName,
            ["isEscalated"] = row.IsEscalated,
        })));
    }
}
