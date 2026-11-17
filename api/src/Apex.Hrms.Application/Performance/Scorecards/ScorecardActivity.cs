using Apex.Hrms.Domain.Activity;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// The wording of scorecard changes in the activity feed (category <c>performance</c>). An employee is named by code and a
/// period by name; comments and results never appear.
/// </summary>
internal static class ScorecardActivity
{
    public const string Category = ActivityCategories.Performance;

    public static string Assigned(int count, string employeeCode, string periodName) =>
        $"{count} KPI(s) assigned to {employeeCode} for \"{periodName}\"";

    public static string BulkAssigned(string periodName, int assigned, int skipped) =>
        $"KPIs assigned in bulk for \"{periodName}\": {assigned} employee(s) assigned, {skipped} skipped";

    public static string ItemUpdated(string kpiCode, string employeeCode, string periodName) =>
        $"KPI \"{kpiCode}\" of {employeeCode} for \"{periodName}\" updated";

    public static string ItemRemoved(string kpiCode, string employeeCode, string periodName) =>
        $"KPI \"{kpiCode}\" removed from {employeeCode} for \"{periodName}\"";

    public static string ScorecardDeleted(string employeeCode, string periodName) =>
        $"Scorecard of {employeeCode} for \"{periodName}\" deleted";

    public static string PlanFinalized(string employeeCode, string periodName) =>
        $"KPI plan of {employeeCode} for \"{periodName}\" finalized";

    public static string ResultsSaved(string employeeCode, string periodName) =>
        $"Results saved on the scorecard of {employeeCode} for \"{periodName}\"";

    public static string Submitted(string employeeCode, string periodName, bool again) =>
        $"Scorecard of {employeeCode} for \"{periodName}\" {(again ? "resubmitted" : "submitted")}";

    /// <summary>Never the file name: it can carry personal data.</summary>
    public static string EvidenceAdded(string employeeCode, string periodName) =>
        $"Evidence added to the scorecard of {employeeCode} ({periodName})";

    public static string EvidenceRemoved(string employeeCode, string periodName) =>
        $"Evidence removed from the scorecard of {employeeCode} ({periodName})";

    public static string ReviewStarted(string employeeCode, string periodName) =>
        $"Review of the scorecard of {employeeCode} for \"{periodName}\" started";

    public static string ReviewSaved(string employeeCode, string periodName) =>
        $"Review notes saved on the scorecard of {employeeCode} for \"{periodName}\"";

    public static string Approved(string employeeCode, string periodName, bool finalized) =>
        $"Scorecard of {employeeCode} for \"{periodName}\" approved{(finalized ? " and finalized" : ", waiting for the HR review")}";

    public static string Returned(string employeeCode, string periodName) =>
        $"Scorecard of {employeeCode} for \"{periodName}\" returned to the employee";

    public static string Rejected(string employeeCode, string periodName) =>
        $"Scorecard of {employeeCode} for \"{periodName}\" rejected";

    public static string Finalized(string employeeCode, string periodName) =>
        $"Scorecard of {employeeCode} for \"{periodName}\" finalized by HR";
}
