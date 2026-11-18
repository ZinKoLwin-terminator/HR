using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Excel;
using Apex.Hrms.Application.Grievances;
using Apex.Hrms.Application.Grievances.Insights;
using Apex.Hrms.Application.Tests.Fakes;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Grievances;

namespace Apex.Hrms.Application.Tests.Grievances;

/// <summary>The dashboards, reports and the export: the metric definitions, the suppression, and that no aggregate names anybody.</summary>
public sealed class GrievanceInsightTests : GrievanceTestBase
{
    private IGrievanceInsights Insights => Host.Get<IGrievanceInsights>();

    private static PageRequest<GrievanceReportFilter> Report(GrievanceReportFilter? filter = null, SortState? sort = null) =>
        new(0, 50, sort, null, filter ?? new GrievanceReportFilter());

    /// <summary>
    /// Five counted cases and a draft: A resolved three days after submission, B closed and archived, C rejected, D acknowledged (open),
    /// E anonymous and submitted. The draft is never counted.
    /// </summary>
    private async Task<(int A, int B, int C, int D, int E)> SeedCasesAsync()
    {
        await SeedAsync();
        var a = await ProposedAsync();
        Host.Clock.Today = Host.Clock.Today.AddDays(3);
        AsRita();
        await Ok(new ApproveGrievanceResolutionCommand(a.Id, V(Version(a.Id)), true, null));
        Host.Clock.Today = Host.Clock.Today.AddDays(-3);

        var b = await ClosedAsync();
        AsHana();
        await Ok(new ArchiveGrievanceCommand(b.Id, V(Version(b.Id)), null));
        var c = await SubmittedAsync();
        AsHana();
        await Ok(new RejectGrievanceCommand(c.Id, V(Version(c.Id)), "TST outside policy"));
        var d = await AcknowledgedAsync();
        var e = await SubmittedAsync(anonymous: true);
        await DraftAsync();
        return (a.Id, b.Id, c.Id, d.Id, e.Id);
    }

    [Fact]
    public async Task The_hr_dashboard_counts_every_submitted_case_with_the_defined_metrics()
    {
        await SeedCasesAsync();
        AsHana();

        var dashboard = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default));

        Assert.Equal(5, dashboard.Total);
        Assert.Equal(1, dashboard.New);
        Assert.Equal(1, dashboard.UnderReview);
        Assert.Equal(1, dashboard.Resolved);
        Assert.Equal(1, dashboard.Closed);
        Assert.Equal(1, dashboard.Rejected);
        Assert.Equal(50.00m, dashboard.ResolutionRatePct);
        Assert.Equal(1.5m, dashboard.AverageResolutionDays);
        Assert.Equal(11, dashboard.ByStatus.Count);
        Assert.DoesNotContain(dashboard.ByStatus, count => count.Status == "Draft");
        Assert.Equal(["Low", "Medium", "High", "Critical", null], dashboard.ByPriority.Select(count => count.Priority));
        Assert.Equal(2, dashboard.ByPriority.Single(count => count.Priority is null).Count);
        Assert.Equal(12, dashboard.MonthlyTrend.Count);
        Assert.Equal("2026-11", dashboard.MonthlyTrend[^1].Month);
        Assert.Equal("2025-12", dashboard.MonthlyTrend[0].Month);
        Assert.Equal(5, dashboard.MonthlyTrend[^1].Submitted);
        Assert.Equal(2, dashboard.MonthlyTrend[^1].Resolved);
        Assert.Equal(10, dashboard.RecentActivity.Count);
        Assert.All(dashboard.RecentActivity, step => Assert.StartsWith("GRV-2026-", step.CaseNo, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Departments_put_anonymous_cases_under_not_disclosed_and_merge_small_groups()
    {
        await SeedCasesAsync();
        AsHana();

        var departments = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default)).ByDepartment;

        Assert.Equal(
            [("Department", (int?)W.Org.Department.Id, 4), ("NotDisclosed", null, 1)],
            departments.Select(group => (group.Bucket, group.DepartmentId, group.Count)));
        Assert.Equal("Not disclosed", departments[1].DepartmentName);
    }

    [Fact]
    public async Task A_department_with_fewer_than_three_cases_is_suppressed()
    {
        await SeedAsync();
        await SubmittedAsync();
        await SubmittedAsync();
        AsHana();

        var departments = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default)).ByDepartment;

        var only = Assert.Single(departments);
        Assert.Equal(("Suppressed", (int?)null, "Other / suppressed", 2), (only.Bucket, only.DepartmentId, only.DepartmentName, only.Count));
    }

    [Fact]
    public async Task Without_resolved_cases_the_average_is_null_and_the_rate_zero()
    {
        await SeedAsync();
        var submitted = await SubmittedAsync();
        AsHana();
        await Ok(new RejectGrievanceCommand(submitted.Id, V(Version(submitted.Id)), "TST no"));

        var dashboard = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default));

        Assert.Null(dashboard.AverageResolutionDays);
        Assert.Equal(0m, dashboard.ResolutionRatePct);
    }

    [Fact]
    public async Task Aggregates_are_the_same_for_a_manager_who_submitted_a_case_and_one_who_did_not()
    {
        // Leaving out the cases a viewer submitted would let two managers find the anonymous submitter by comparing totals.
        await SeedAsync();
        As(W.Sam, SamUser, "Sam Submitterson", [.. SelfGrants, .. ManagementGrants]);
        var draft = ResultAssert.Succeeded(await My.CreateDraftAsync(DraftRequest(W.Category.Id, anonymous: true), default));
        Later();
        ResultAssert.Succeeded(await My.SubmitAsync(draft.Id, new SubmitGrievanceRequest(V(draft.RowVersion), true), default));

        var samSees = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default));
        var samSummary = ResultAssert.Succeeded(await Insights.SummaryAsync(new GrievanceSummaryFilter(), default));
        AsHana();
        var hanaSees = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default));

        Assert.Equal(1, samSees.Total);
        Assert.Equal(Json(hanaSees), Json(samSees));
        Assert.Equal(1, samSummary.Total);
    }

    [Fact]
    public async Task The_overdue_count_uses_open_cases_past_their_target_only()
    {
        var (_, _, _, d, _) = await SeedCasesAsync();
        Host.Clock.Today = Host.Clock.Today.AddDays(30);
        AsHana();

        var dashboard = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default));
        var report = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(new GrievanceReportFilter(Overdue: true)), default));

        Assert.Equal(1, dashboard.Overdue);
        Assert.Equal([d], report.Items.Select(row => row.CaseId));
        Assert.True(report.Items[0].IsOverdue);
        Assert.Equal(30, report.Items[0].AgeDays);
    }

    [Fact]
    public async Task The_case_report_never_names_the_submitter_or_carries_the_subject()
    {
        var (a, b, _, _, e) = await SeedCasesAsync();
        AsHana();

        var report = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(), default));
        var json = Json(report);

        Assert.Equal(5, report.Total);
        Assert.Contains(report.Items, row => row.CaseId == b);
        var anonymous = report.Items.Single(row => row.CaseId == e);
        Assert.Null(anonymous.DepartmentId);
        Assert.Equal("Not disclosed", anonymous.DepartmentName);
        var resolved = report.Items.Single(row => row.CaseId == a);
        Assert.Equal(3, resolved.ResolutionDays);
        Assert.Equal("Ivan Investigator", resolved.LeadInvestigatorName);
        Assert.DoesNotContain("Sam", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TST Repeated", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Floor", json, StringComparison.Ordinal);
    }

    /// <summary>Sam submits a named and an anonymous case and one more case about Sid. Sid (management grants) is its subject.</summary>
    private async Task<(int About, int Named, int Anonymous)> SubjectAndSubmitterCasesAsync()
    {
        await SeedAsync();
        var about = await AcknowledgedAsync();
        AsHana();
        await Ok(new TriageGrievanceCommand(about.Id, V(Version(about.Id)), W.Category.Id, "Medium", about.TargetResolutionDate, W.Sid.Id, null, false));

        // Sid also submits two cases of his own: report rows keep them (rows never show a submitter).
        As(W.Sid, SidUser, "Sid Subject", [.. SelfGrants, .. ManagementGrants]);
        var named = ResultAssert.Succeeded(await My.CreateDraftAsync(DraftRequest(W.Category.Id), default));
        Later();
        ResultAssert.Succeeded(await My.SubmitAsync(named.Id, new SubmitGrievanceRequest(V(named.RowVersion), true), default));
        var anonymous = ResultAssert.Succeeded(await My.CreateDraftAsync(DraftRequest(W.Category.Id, anonymous: true), default));
        Later();
        ResultAssert.Succeeded(await My.SubmitAsync(anonymous.Id, new SubmitGrievanceRequest(V(anonymous.RowVersion), true), default));
        return (about.Id, named.Id, anonymous.Id);
    }

    [Fact]
    public async Task Report_rows_leave_out_the_cases_the_caller_is_the_subject_of_but_keep_the_cases_they_submitted()
    {
        var (about, named, anonymous) = await SubjectAndSubmitterCasesAsync();

        AsSid();
        var sidRows = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(), default));
        AsHana();
        var hanaRows = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(), default));

        Assert.Equal(new[] { named, anonymous }.Order(), sidRows.Items.Select(row => row.CaseId).Order());
        Assert.Equal(new[] { about, named, anonymous }.Order(), hanaRows.Items.Select(row => row.CaseId).Order());
    }

    [Fact]
    public async Task Export_rows_leave_out_the_cases_the_caller_is_the_subject_of_also_when_selected_by_id()
    {
        var (about, named, anonymous) = await SubjectAndSubmitterCasesAsync();
        AsSid();
        var module = Host.Get<IEnumerable<IExcelModule>>().Single(candidate => candidate.Info.Code == "grievance-cases");
        string?[] caseNos = [Stored(about).CaseNo, Stored(named).CaseNo, Stored(anonymous).CaseNo];

        var filtered = ResultAssert.Succeeded(await module.ExportRowsAsync(new ExportRequest(), importLayout: false, limit: 100, default));
        var selected = ResultAssert.Succeeded(await module.ExportRowsAsync(new ExportRequest(ExportScope.Selected, SelectedIds: [about, named]), importLayout: false, limit: 100, default));

        Assert.Equal(new[] { caseNos[1], caseNos[2] }.Order(), filtered.Select(row => (string?)row["caseNo"]).Order());
        Assert.Equal([caseNos[1]], selected.Select(row => (string?)row["caseNo"]));
    }

    [Fact]
    public async Task Dashboard_counts_and_the_summary_stay_identical_for_the_subject_of_a_case_but_recent_activity_leaves_their_case_out()
    {
        var (about, _, _) = await SubjectAndSubmitterCasesAsync();

        AsSid();
        var sidDashboard = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default));
        var sidSummary = ResultAssert.Succeeded(await Insights.SummaryAsync(new GrievanceSummaryFilter(), default));
        AsHana();
        var hanaDashboard = ResultAssert.Succeeded(await Insights.HrDashboardAsync(default));
        var hanaSummary = ResultAssert.Succeeded(await Insights.SummaryAsync(new GrievanceSummaryFilter(), default));

        Assert.Equal(3, sidDashboard.Total);
        Assert.Equal(Json(hanaDashboard with { RecentActivity = [] }), Json(sidDashboard with { RecentActivity = [] }));
        Assert.Contains(hanaDashboard.RecentActivity, step => step.CaseId == about);
        Assert.DoesNotContain(sidDashboard.RecentActivity, step => step.CaseId == about);
        Assert.Equal(Json(hanaSummary), Json(sidSummary));
    }

    [Fact]
    public async Task The_case_report_filters_and_sorts_and_refuses_bad_input()
    {
        var (a, _, _, _, _) = await SeedCasesAsync();
        AsHana();

        var byDepartment = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(new GrievanceReportFilter(DepartmentId: W.Org.Department.Id)), default));
        var byDays = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(new GrievanceReportFilter(MinResolutionDays: 1, MaxResolutionDays: 5)), default));
        var byInvestigator = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(new GrievanceReportFilter(InvestigatorEmployeeId: W.Ivan.Id)), default));
        var sorted = ResultAssert.Succeeded(await Insights.CasesReportAsync(Report(sort: new SortState("resolutionDays", "desc")), default));

        Assert.Equal(4, byDepartment.Total);
        Assert.Equal([a], byDays.Items.Select(row => row.CaseId));
        Assert.Equal(2, byInvestigator.Total);
        Assert.Equal(a, sorted.Items[0].CaseId);
        ResultAssert.FailedOnField(await Insights.CasesReportAsync(Report(sort: new SortState("subject", "asc")), default), "sort");
        ResultAssert.FailedOnField(await Insights.CasesReportAsync(Report(new GrievanceReportFilter(MinResolutionDays: 5, MaxResolutionDays: 1)), default), "minResolutionDays");
        ResultAssert.FailedOnField(await Insights.CasesReportAsync(Report(new GrievanceReportFilter(Status: "Open")), default), "status");
    }

    [Fact]
    public async Task The_summary_applies_its_filters_and_has_no_department_filter()
    {
        await SeedCasesAsync();
        AsRita();

        var all = ResultAssert.Succeeded(await Insights.SummaryAsync(new GrievanceSummaryFilter(), default));
        var medium = ResultAssert.Succeeded(await Insights.SummaryAsync(new GrievanceSummaryFilter(Priority: "Medium"), default));
        var later = ResultAssert.Succeeded(await Insights.SummaryAsync(new GrievanceSummaryFilter(SubmittedFrom: Host.Clock.Today.AddDays(1)), default));

        Assert.Equal(5, all.Total);
        Assert.Equal(2, all.Open);
        Assert.Equal(50.00m, all.ResolutionRatePct);
        Assert.Equal(3, medium.Total);
        Assert.Equal(0, later.Total);
        ResultAssert.FailedOnField(await Insights.SummaryAsync(new GrievanceSummaryFilter(Priority: "Urgent"), default), "priority");
    }

    [Fact]
    public async Task The_review_dashboard_counts_the_queue_and_the_callers_own_decisions()
    {
        await SeedAsync();
        await ResolvedAsync();
        var pending = await ProposedAsync();
        await AcknowledgedAsync(priority: "Critical");
        Host.Clock.Today = Host.Clock.Today.AddDays(2);
        AsRita();

        var dashboard = ResultAssert.Succeeded(await Insights.ReviewDashboardAsync(default));

        Assert.Equal(1, dashboard.PendingProposals);
        Assert.Equal(1, dashboard.Escalated);
        Assert.Equal(1, dashboard.ImmediateEscalations);
        Assert.Equal(2, dashboard.OldestPendingDays);
        Assert.Equal((1, 0), (dashboard.DecidedByMe.Approved, dashboard.DecidedByMe.Rejected));
        Assert.Equal(2, dashboard.ByPriority.Single(count => count.Priority == "Medium").Count + dashboard.ByPriority.Single(count => count.Priority == "Critical").Count);
        Assert.NotEqual(0, pending.Id);
    }

    [Fact]
    public async Task The_excel_export_has_the_report_columns_never_the_subject_and_is_logged_not_in_the_activity_feed()
    {
        var (_, _, _, _, e) = await SeedCasesAsync();
        AsHana();
        var module = Host.Get<IEnumerable<IExcelModule>>().Single(candidate => candidate.Info.Code == "grievance-cases");
        var activityBefore = Host.ActivityLog.Messages.Count;

        var rows = ResultAssert.Succeeded(await module.ExportRowsAsync(new ExportRequest(), importLayout: false, limit: 100, default));
        await Host.Get<IUnitOfWork>().SaveChangesAsync(default);

        Assert.Equal(5, rows.Count);
        Assert.All(rows, row => Assert.Equal(
            ["caseNo", "category", "status", "priority", "department", "submittedOn", "targetResolutionDate", "resolvedOn", "resolutionDays", "isOverdue", "leadInvestigator", "isEscalated"],
            row.Keys));
        var caseNo = Stored(e).CaseNo;
        Assert.Equal("Not disclosed", rows.Single(row => (string?)row["caseNo"] == caseNo)["department"]);
        Assert.DoesNotContain(rows.SelectMany(row => row.Values).OfType<string>(), value => value.Contains("TST Repeated", StringComparison.Ordinal) || value.Contains("Sam", StringComparison.Ordinal));
        var logged = Audit.Entries.Last();
        Assert.Equal(GrievanceAuditAction.Export, logged.Action);
        Assert.Null(logged.CaseId);
        Assert.Equal("module:grievance-cases;rows:5", logged.Detail);
        Assert.False(module.RecordsActivity);
        Assert.Equal(activityBefore, Host.ActivityLog.Messages.Count);
    }

    [Fact]
    public async Task The_my_dashboard_counts_the_callers_own_cases_named_and_anonymous()
    {
        await SeedAsync();
        await DraftAsync();
        await SubmittedAsync(anonymous: true);
        await AwaitingAsync();
        AsSam();

        var mine = ResultAssert.Succeeded(await My.DashboardAsync(default));

        Assert.Equal((1, 2, 1), (mine.Drafts, mine.Open, mine.AwaitingMyResponse));
        Assert.Equal(3, mine.Recent.Count);
    }

    [Fact]
    public async Task The_employee_picker_lists_active_employees_only_at_most_twenty()
    {
        await SeedAsync();
        AsHana();

        var all = ResultAssert.Succeeded(await Cases.EmployeeOptionsAsync(null, default));
        var sid = ResultAssert.Succeeded(await Cases.EmployeeOptionsAsync("Sid", default));

        Assert.Equal(6, all.Count);
        Assert.True(all.Count <= 20);
        Assert.Equal(W.Sid.Id, Assert.Single(sid).EmployeeId);
        Assert.Equal(W.Org.Department.Name, sid[0].DepartmentName);
        ResultAssert.FailedOnField(await Cases.EmployeeOptionsAsync(new string('x', 101), default), "search");
    }
}
