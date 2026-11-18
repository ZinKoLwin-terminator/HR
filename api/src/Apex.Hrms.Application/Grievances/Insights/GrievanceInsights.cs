using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Organization;

namespace Apex.Hrms.Application.Grievances.Insights;

/// <summary>
/// The counted cases (every case that is not a Draft, archived included) as <see cref="GrievanceCaseFact"/>s. The database does the
/// filtering that maps to columns; the metrics are worked out in memory (grievance volumes are small) by <see cref="GrievanceFigures"/>.
/// Deliberately NOT the case-team scope: aggregates are the same for every viewer of a tier (see <see cref="GrievanceAccess"/>).
/// </summary>
internal sealed class GrievanceInsightSource(IReadModel read, IClock clock)
{
    public IReadModel Read => read;

    public DateOnly Today => clock.Today;

    public IQueryable<GrievanceCase> Counted() => read.Query<GrievanceCase>().Where(grievance => grievance.Status != GrievanceStatus.Draft);

    public DateOnly DateOf(DateTime utc) => GrievanceDates.DateOf(clock, utc);

    /// <summary>Applies the report filters that map to columns (dates, department, category, status, priority, investigator).</summary>
    public Result<IQueryable<GrievanceCase>> Filter(IQueryable<GrievanceCase> cases, GrievanceReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var violations = new Violations()
            .AddIf(filter.SubmittedFrom > filter.SubmittedTo, "submittedFrom", "The start date must not be after the end date.")
            .AddIf(filter.MinResolutionDays < 0, "minResolutionDays", "Enter zero or more days.")
            .AddIf(filter.MaxResolutionDays < 0, "maxResolutionDays", "Enter zero or more days.")
            .AddIf(filter.MinResolutionDays > filter.MaxResolutionDays, "minResolutionDays", "The minimum must not be above the maximum.");
        GrievanceStatus status = default;
        GrievancePriority priority = default;
        violations
            .AddIf(filter.Status is { Length: > 0 } && !GrievanceText.Status.TryParse(filter.Status, out status), "status", "Choose a valid status.")
            .AddIf(filter.Priority is { Length: > 0 } && !GrievanceText.Priority.TryParse(filter.Priority, out priority), "priority", "Choose a valid priority.");
        if (violations.Any)
        {
            return Result.Failure<IQueryable<GrievanceCase>>(violations.ToError());
        }

        cases = Submitted(cases, filter.SubmittedFrom, filter.SubmittedTo);
        if (filter.DepartmentId is { } departmentId)
        {
            cases = cases.Where(grievance => !grievance.IsAnonymous && grievance.DepartmentId == departmentId);
        }

        if (filter.CategoryId is { } categoryId)
        {
            cases = cases.Where(grievance => grievance.CategoryId == categoryId);
        }

        if (filter.Status is { Length: > 0 })
        {
            cases = cases.Where(grievance => grievance.Status == status);
        }

        if (filter.Priority is { Length: > 0 })
        {
            cases = cases.Where(grievance => grievance.Priority == priority);
        }

        if (filter.InvestigatorEmployeeId is { } investigator)
        {
            var assigned = read.Query<GrievanceAssignment>()
                .Where(assignment => assignment.InvestigatorEmployeeId == investigator && assignment.UnassignedAtUtc == null)
                .Select(assignment => assignment.CaseId);
            cases = cases.Where(grievance => assigned.Contains(grievance.Id));
        }

        return Result.Success(cases);
    }

    /// <summary>Cases submitted from <paramref name="from"/> to <paramref name="to"/> (inclusive, business dates).</summary>
    public IQueryable<GrievanceCase> Submitted(IQueryable<GrievanceCase> cases, DateOnly? from, DateOnly? to)
    {
        if (from is { } start)
        {
            var since = clock.StartOfDayUtc(start);
            cases = cases.Where(grievance => grievance.SubmittedAtUtc >= since);
        }

        if (to is { } end)
        {
            var before = clock.StartOfDayUtc(end.AddDays(1));
            cases = cases.Where(grievance => grievance.SubmittedAtUtc < before);
        }

        return cases;
    }

    /// <summary>The report filters worked out in memory: the resolution days range and overdue.</summary>
    public List<GrievanceCaseFact> Refine(IEnumerable<GrievanceCaseFact> facts, GrievanceReportFilter filter)
    {
        var today = clock.Today;
        return
        [
            .. facts.Where(fact =>
                (filter.MinResolutionDays is null || fact.ResolutionDays >= filter.MinResolutionDays)
                && (filter.MaxResolutionDays is null || fact.ResolutionDays <= filter.MaxResolutionDays)
                && (filter.Overdue is not { } overdue || fact.IsOverdue(today) == overdue)),
        ];
    }

    public async Task<List<GrievanceCaseFact>> FactsAsync(IQueryable<GrievanceCase> cases, CancellationToken cancellationToken)
    {
        var assignments = read.Query<GrievanceAssignment>();
        var rows = await read.ToListAsync(
            from grievance in cases
            join category in read.Query<GrievanceCategory>() on grievance.CategoryId equals category.Id
            select new
            {
                grievance.Id,
                grievance.CaseNo,
                grievance.CategoryId,
                CategoryName = category.Name,
                grievance.Status,
                grievance.Priority,
                grievance.IsAnonymous,
                grievance.DepartmentId,
                grievance.SubmittedAtUtc,
                grievance.TargetResolutionDate,
                grievance.ResolvedAtUtc,
                grievance.IsEscalated,
                Lead = assignments
                    .Where(assignment => assignment.CaseId == grievance.Id && assignment.Role == GrievanceAssignmentRole.Lead && assignment.UnassignedAtUtc == null)
                    .Select(assignment => (int?)assignment.InvestigatorEmployeeId)
                    .FirstOrDefault(),
            },
            cancellationToken);
        return
        [
            .. rows.Select(row => new GrievanceCaseFact(
                row.Id,
                row.CaseNo ?? string.Empty,
                row.CategoryId,
                row.CategoryName,
                row.Status,
                row.Priority,
                row.IsAnonymous,
                row.IsAnonymous ? null : row.DepartmentId,
                DateOf(row.SubmittedAtUtc ?? default),
                row.TargetResolutionDate,
                row.Status is GrievanceStatus.Resolved or GrievanceStatus.Closed && row.ResolvedAtUtc is { } resolved ? DateOf(resolved) : null,
                row.IsEscalated,
                row.Lead)),
        ];
    }

    public async Task<IReadOnlyDictionary<int, string>> DepartmentNamesAsync(IEnumerable<GrievanceCaseFact> facts, CancellationToken cancellationToken)
    {
        int[] ids = [.. facts.Select(fact => fact.DepartmentId).OfType<int>().Distinct()];
        if (ids.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        var rows = await read.ToListAsync(read.Query<Department>().Where(department => ids.Contains(department.Id)).Select(department => new { department.Id, department.Name }), cancellationToken);
        return rows.ToDictionary(row => row.Id, row => row.Name);
    }

    public async Task<IReadOnlyDictionary<int, string>> LeadNamesAsync(IEnumerable<GrievanceCaseFact> facts, CancellationToken cancellationToken)
    {
        int[] ids = [.. facts.Select(fact => fact.LeadEmployeeId).OfType<int>().Distinct()];
        if (ids.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        var rows = await read.ToListAsync(
            read.Query<Employee>().Where(employee => ids.Contains(employee.Id)).Select(employee => new { employee.Id, employee.FirstName, employee.LastName }),
            cancellationToken);
        return rows.ToDictionary(row => row.Id, row => $"{row.FirstName} {row.LastName}".Trim());
    }

    /// <summary>The report row of a fact: an anonymous case shows "Not disclosed" for its department and no department id.</summary>
    public GrievanceReportRowDto ToRow(GrievanceCaseFact fact, IReadOnlyDictionary<int, string> departments, IReadOnlyDictionary<int, string> leads)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var today = clock.Today;
        return new GrievanceReportRowDto(
            fact.Id,
            fact.CaseNo,
            fact.CategoryName,
            GrievanceText.Status.ToText(fact.Status),
            fact.Priority is { } priority ? GrievanceText.Priority.ToText(priority) : null,
            fact.IsAnonymous ? null : fact.DepartmentId,
            fact.IsAnonymous ? GrievanceFigures.NotDisclosedLabel : fact.DepartmentId is { } department ? departments.GetValueOrDefault(department) : null,
            fact.SubmittedOn,
            fact.TargetResolutionDate,
            fact.ResolvedOn,
            fact.ResolutionDays,
            fact.AgeDays(today),
            fact.IsOverdue(today),
            fact.LeadEmployeeId is { } lead ? leads.GetValueOrDefault(lead) : null,
            fact.IsEscalated);
    }
}

/// <summary>The grievance dashboards and reports.</summary>
internal sealed class GrievanceInsights(GrievanceInsightSource source, ICurrentUser user) : IGrievanceInsights
{
    private static readonly Dictionary<string, Func<GrievanceReportRowDto, object?>> SortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["caseNo"] = row => row.CaseNo,
        ["categoryName"] = row => row.CategoryName,
        ["status"] = row => Array.IndexOf(Enum.GetNames<GrievanceStatus>(), row.Status),
        ["priority"] = row => row.Priority is null ? 0 : Array.IndexOf(Enum.GetNames<GrievancePriority>(), row.Priority) + 1,
        ["departmentName"] = row => row.DepartmentName,
        ["submittedOn"] = row => row.SubmittedOn,
        ["targetResolutionDate"] = row => row.TargetResolutionDate,
        ["resolvedOn"] = row => row.ResolvedOn,
        ["resolutionDays"] = row => row.ResolutionDays,
        ["ageDays"] = row => row.AgeDays,
    };

    public async Task<Result<GrievanceHrDashboardDto>> HrDashboardAsync(CancellationToken cancellationToken)
    {
        var facts = await source.FactsAsync(source.Counted(), cancellationToken);
        var today = source.Today;
        var departments = await source.DepartmentNamesAsync(facts, cancellationToken);
        var read = source.Read;
        // Recent activity is case-level detail (case numbers and steps), so like the case lists it leaves out the cases the caller is
        // the subject of. The counts above stay the same for every viewer.
        var recent = await read.ToListAsync(
            (from entry in read.Query<GrievanceStatusHistoryEntry>()
             join grievance in ReportCases() on entry.CaseId equals grievance.Id
             orderby entry.OccurredAtUtc descending, entry.Id descending
             select new { entry.CaseId, grievance.CaseNo, entry.Action, entry.ToStatus, entry.OccurredAtUtc })
            .Take(GrievanceFigures.RecentSteps),
            cancellationToken);
        int Count(params GrievanceStatus[] statuses) => facts.Count(fact => statuses.Contains(fact.Status));

        return Result.Success(new GrievanceHrDashboardDto(
            today,
            facts.Count,
            Count(GrievanceStatus.Submitted),
            Count(GrievanceStatus.Acknowledged, GrievanceStatus.UnderReview, GrievanceStatus.Reopened),
            Count(GrievanceStatus.Assigned, GrievanceStatus.UnderInvestigation),
            Count(GrievanceStatus.AwaitingEmployeeResponse),
            Count(GrievanceStatus.ResolutionProposed),
            Count(GrievanceStatus.Resolved),
            Count(GrievanceStatus.Closed),
            Count(GrievanceStatus.Rejected),
            facts.Count(fact => fact.IsOverdue(today)),
            facts.Count(fact => fact.IsOpen && fact.IsEscalated),
            GrievanceFigures.ResolutionRatePct(facts),
            GrievanceFigures.AverageResolutionDays(facts),
            GrievanceFigures.ByStatus(facts),
            GrievanceFigures.ByPriority(facts),
            GrievanceFigures.ByCategory(facts),
            GrievanceFigures.ByDepartment(facts, departments),
            GrievanceFigures.MonthlyTrend(facts, today),
            [.. recent.Select(step => new GrievanceRecentActivityDto(
                step.CaseId,
                step.CaseNo ?? string.Empty,
                GrievanceText.HistoryAction.ToText(step.Action),
                GrievanceText.Status.ToText(step.ToStatus),
                step.OccurredAtUtc))]));
    }

    public async Task<Result<GrievanceReviewDashboardDto>> ReviewDashboardAsync(CancellationToken cancellationToken)
    {
        var read = source.Read;
        var today = source.Today;
        var pendingProposals = read.Query<GrievanceResolutionProposal>().Where(proposal => proposal.Decision == ResolutionDecision.Pending);
        var pendingCases = pendingProposals.Select(proposal => proposal.CaseId);
        var queue = source.Counted()
            .Where(GrievanceQueries.IsOpen())
            .Where(grievance => grievance.IsEscalated || pendingCases.Contains(grievance.Id));
        var facts = await source.FactsAsync(queue, cancellationToken);
        var factIds = facts.Select(fact => fact.Id).ToHashSet();

        var immediate = (await read.ToListAsync(
                read.Query<GrievanceEscalation>().Where(escalation => escalation.IsImmediate && escalation.ClearedAtUtc == null).Select(escalation => escalation.CaseId),
                cancellationToken))
            .ToHashSet();
        var pending = (await read.ToListAsync(pendingProposals.Select(proposal => new { proposal.CaseId, proposal.ProposedAtUtc }), cancellationToken))
            .Where(proposal => factIds.Contains(proposal.CaseId))
            .ToList();
        var userId = user.UserId ?? 0;
        var decisions = await read.ToListAsync(
            read.Query<GrievanceResolutionProposal>().Where(proposal => proposal.DecidedByUserId == userId).Select(proposal => proposal.Decision),
            cancellationToken);

        return Result.Success(new GrievanceReviewDashboardDto(
            today,
            facts.Count(fact => fact.Status == GrievanceStatus.ResolutionProposed && pending.Any(proposal => proposal.CaseId == fact.Id)),
            facts.Count(fact => fact.IsEscalated),
            facts.Count(fact => fact.IsEscalated && immediate.Contains(fact.Id)),
            facts.Count(fact => fact.IsOverdue(today)),
            pending.Count == 0 ? null : today.DayNumber - pending.Min(proposal => source.DateOf(proposal.ProposedAtUtc)).DayNumber,
            new GrievanceDecisionCountDto(
                decisions.Count(decision => decision == ResolutionDecision.Approved),
                decisions.Count(decision => decision == ResolutionDecision.Rejected)),
            GrievanceFigures.ByPriority(facts),
            GrievanceFigures.ByCategory(facts)));
    }

    public async Task<Result<PagedResult<GrievanceReportRowDto>>> CasesReportAsync(PageRequest<GrievanceReportFilter> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Func<GrievanceReportRowDto, object?> sortKey = row => row.SubmittedOn;
        var descending = true;
        if (request.Sort is { Active: { Length: > 0 } active, Direction: { Length: > 0 } direction })
        {
            if (!SortKeys.TryGetValue(active, out var chosen))
            {
                return Result.Failure<PagedResult<GrievanceReportRowDto>>(AppError.Validation($"Cannot sort by \"{active}\".", "sort", "Choose a sortable field."));
            }

            sortKey = chosen;
            descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        }

        if (request.Search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<GrievanceReportRowDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.", "search", $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var rows = await ReportRowsAsync(ReportCases(), request.Filters ?? new GrievanceReportFilter(), request.Search, cancellationToken);
        if (rows.IsFailure)
        {
            return Result.Failure<PagedResult<GrievanceReportRowDto>>(rows.Error);
        }

        var ordered = (descending ? rows.Value.OrderByDescending(sortKey) : rows.Value.OrderBy(sortKey)).ThenBy(row => row.CaseId).ToList();
        var pageSize = Paging.ClampPageSize(request.PageSize);
        var pageIndex = Paging.ClampPageIndex(Math.Max(0, request.PageIndex), ordered.Count, pageSize);
        return Result.Success(new PagedResult<GrievanceReportRowDto>([.. ordered.Skip(pageIndex * pageSize).Take(pageSize)], ordered.Count, pageIndex, pageSize));
    }

    /// <summary>
    /// The cases the report rows (and the export rows) come from: every counted case except those whose subject employee is the caller.
    /// Rows are case-level detail, so they follow the case-list rule for the subject. Cases the caller submitted are NOT left out: rows
    /// never show a submitter, and leaving them out would let the rows be compared with the aggregates (which leave nothing out).
    /// </summary>
    public IQueryable<GrievanceCase> ReportCases()
    {
        var cases = source.Counted();
        return user.EmployeeId is { } me
            ? cases.Where(grievance => grievance.SubjectEmployeeId == null || grievance.SubjectEmployeeId != me)
            : cases;
    }

    /// <summary>The report rows of <paramref name="cases"/> with the filters and the case-number search (never the subject). Shared with the export.</summary>
    public async Task<Result<List<GrievanceReportRowDto>>> ReportRowsAsync(IQueryable<GrievanceCase> cases, GrievanceReportFilter filter, string? search, CancellationToken cancellationToken)
    {
        var filtered = source.Filter(cases, filter);
        if (filtered.IsFailure)
        {
            return Result.Failure<List<GrievanceReportRowDto>>(filtered.Error);
        }

        var query = filtered.Value;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(Predicates.ContainsAny<GrievanceCase>(search.Trim(), grievance => grievance.CaseNo));
        }

        var facts = source.Refine(await source.FactsAsync(query, cancellationToken), filter);
        var departments = await source.DepartmentNamesAsync(facts, cancellationToken);
        var leads = await source.LeadNamesAsync(facts, cancellationToken);
        return Result.Success(facts.Select(fact => source.ToRow(fact, departments, leads)).ToList());
    }

    public async Task<Result<GrievanceSummaryDto>> SummaryAsync(GrievanceSummaryFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var filtered = source.Filter(source.Counted(), new GrievanceReportFilter(filter.SubmittedFrom, filter.SubmittedTo, CategoryId: filter.CategoryId, Priority: filter.Priority));
        if (filtered.IsFailure)
        {
            return Result.Failure<GrievanceSummaryDto>(filtered.Error);
        }

        var facts = await source.FactsAsync(filtered.Value, cancellationToken);
        var today = source.Today;
        return Result.Success(new GrievanceSummaryDto(
            today,
            facts.Count,
            facts.Count(fact => fact.IsOpen),
            facts.Count(fact => fact.Status == GrievanceStatus.Resolved),
            facts.Count(fact => fact.Status == GrievanceStatus.Closed),
            facts.Count(fact => fact.Status == GrievanceStatus.Rejected),
            facts.Count(fact => fact.IsOverdue(today)),
            facts.Count(fact => fact.IsOpen && fact.IsEscalated),
            GrievanceFigures.ResolutionRatePct(facts),
            GrievanceFigures.AverageResolutionDays(facts),
            GrievanceFigures.ByStatus(facts),
            GrievanceFigures.ByPriority(facts),
            GrievanceFigures.ByCategory(facts),
            GrievanceFigures.ByDepartment(facts, await source.DepartmentNamesAsync(facts, cancellationToken))));
    }
}
