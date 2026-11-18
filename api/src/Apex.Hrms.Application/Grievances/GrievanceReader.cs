using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Grievances;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Security;

namespace Apex.Hrms.Application.Grievances;

/// <summary>One row of a case-team list, projected in the database (never the pseudonym).</summary>
internal sealed class GrievanceCaseRow
{
    public int Id { get; init; }

    public string? CaseNo { get; init; }

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public GrievanceStatus Status { get; init; }

    public GrievancePriority? Priority { get; init; }

    public bool IsAnonymous { get; init; }

    public int? DepartmentId { get; init; }

    public string? DepartmentName { get; init; }

    public DateTime? SubmittedAtUtc { get; init; }

    public DateOnly? TargetResolutionDate { get; init; }

    public bool IsEscalated { get; init; }

    public DateTime? ArchivedAtUtc { get; init; }

    public byte[] RowVersion { get; init; } = [];

    /// <summary>The named submitter's employee (null on an anonymous case: the row then holds only the pseudonym).</summary>
    public int? SubmitterEmployeeId { get; init; }

    public int? LeadEmployeeId { get; init; }

    public string? SubmitterName { get; set; }

    public string? LeadName { get; set; }
}

/// <summary>
/// Loads grievance records for the redaction: the submitter's own cases and details, the case-team lists and details. The scope is
/// always applied by the caller of a method (the <c>source</c> it passes); nothing here widens it.
/// </summary>
internal sealed class GrievanceReader(IReadModel read, IGrievanceSubmitterDirectory submitters, IClock clock)
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<GrievanceCaseRow, object>>> TeamSortKeys =
        new Dictionary<string, Expression<Func<GrievanceCaseRow, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["caseNo"] = row => row.CaseNo!,
            ["submittedAtUtc"] = row => row.SubmittedAtUtc!,
            ["targetResolutionDate"] = row => row.TargetResolutionDate!,
            ["priority"] = row => row.Priority == GrievancePriority.Critical ? 4
                : row.Priority == GrievancePriority.High ? 3
                : row.Priority == GrievancePriority.Medium ? 2
                : row.Priority == GrievancePriority.Low ? 1 : 0,
            ["status"] = row => row.Status,
            ["categoryName"] = row => row.CategoryName,
        };

    public DateOnly Today => clock.Today;

    public Task<byte[]?> RowVersionAsync(int id, CancellationToken cancellationToken) =>
        read.FirstOrDefaultAsync(read.Query<GrievanceCase>().Where(grievance => grievance.Id == id).Select(grievance => grievance.RowVersion), cancellationToken);

    public Task<GrievanceCase?> FirstAsync(Expression<Func<GrievanceCase, bool>> scope, int id, CancellationToken cancellationToken) =>
        read.FirstOrDefaultAsync(read.Query<GrievanceCase>().Where(scope).Where(grievance => grievance.Id == id), cancellationToken);

    // ---- Submitter view ----

    public async Task<MyGrievanceDetailDto> MyDetailAsync(GrievanceCase grievance, CancellationToken cancellationToken)
    {
        var id = grievance.Id;
        var submitter = await read.FirstOrDefaultAsync(submitters.Query().Where(row => row.CaseId == id), cancellationToken)
            ?? throw new InvalidOperationException($"Grievance case #{id} has no submitter row.");
        var bundle = new MyGrievanceBundle(
            grievance,
            await CategoryNameAsync(grievance.CategoryId, cancellationToken),
            await DepartmentNameAsync(grievance.InvolvedDepartmentId, cancellationToken),
            submitter,
            await read.ToListAsync(read.Query<GrievanceStatusHistoryEntry>().Where(entry => entry.CaseId == id && entry.VisibleToSubmitter), cancellationToken),
            await read.ToListAsync(read.Query<GrievanceCommunication>().Where(message => message.CaseId == id), cancellationToken),
            await read.ToListAsync(
                read.Query<GrievanceAttachment>().Where(attachment => attachment.CaseId == id
                    && attachment.Visibility == GrievanceAttachmentVisibility.Shared
                    && attachment.RemovedAtUtc == null),
                cancellationToken));
        return GrievanceRedaction.ToMyDetail(bundle);
    }

    /// <summary>The caller's own cases from <paramref name="own"/> (their scope) as list rows, in the given order, with the unread badge.</summary>
    public async Task<List<MyGrievanceDto>> MyRowsAsync(IQueryable<GrievanceCase> own, CancellationToken cancellationToken)
    {
        var rows = await read.ToListAsync(
            from grievance in own
            join category in read.Query<GrievanceCategory>() on grievance.CategoryId equals category.Id
            join submitter in submitters.Query() on grievance.Id equals submitter.CaseId
            select new { Case = grievance, CategoryName = category.Name, submitter.SubmitterSeenAtUtc },
            cancellationToken);
        return [.. rows.Select(row => GrievanceRedaction.ToMy(row.Case, row.CategoryName, row.SubmitterSeenAtUtc))];
    }

    // ---- Case-team view ----

    public IQueryable<GrievanceCaseRow> Rows(IQueryable<GrievanceCase> source)
    {
        var categories = read.Query<GrievanceCategory>();
        var departments = read.Query<Department>();
        var caseSubmitters = submitters.Query();
        var assignments = read.Query<GrievanceAssignment>();
        return from grievance in source
               join category in categories on grievance.CategoryId equals category.Id
               select new GrievanceCaseRow
               {
                   Id = grievance.Id,
                   CaseNo = grievance.CaseNo,
                   CategoryId = grievance.CategoryId,
                   CategoryName = category.Name,
                   Subject = grievance.Subject,
                   Status = grievance.Status,
                   Priority = grievance.Priority,
                   IsAnonymous = grievance.IsAnonymous,
                   DepartmentId = grievance.DepartmentId,
                   DepartmentName = departments.Where(department => department.Id == grievance.DepartmentId).Select(department => department.Name).FirstOrDefault(),
                   SubmittedAtUtc = grievance.SubmittedAtUtc,
                   TargetResolutionDate = grievance.TargetResolutionDate,
                   IsEscalated = grievance.IsEscalated,
                   ArchivedAtUtc = grievance.ArchivedAtUtc,
                   RowVersion = grievance.RowVersion,
                   SubmitterEmployeeId = grievance.IsAnonymous
                       ? null
                       : caseSubmitters.Where(submitter => submitter.CaseId == grievance.Id).Select(submitter => submitter.EmployeeId).FirstOrDefault(),
                   LeadEmployeeId = assignments
                       .Where(assignment => assignment.CaseId == grievance.Id && assignment.Role == GrievanceAssignmentRole.Lead && assignment.UnassignedAtUtc == null)
                       .Select(assignment => (int?)assignment.InvestigatorEmployeeId)
                       .FirstOrDefault(),
               };
    }

    /// <summary>
    /// One page of case-team rows from <paramref name="source"/> (already the caller's scope and the purpose of the list): the filters,
    /// the search on case number or subject, the sort whitelist (or the fallback) and the paging, in the database.
    /// </summary>
    public async Task<Result<PagedResult<GrievanceCaseDto>>> TeamPageAsync(
        IQueryable<GrievanceCase> source,
        PageRequest<GrievanceCaseFilter> request,
        Expression<Func<GrievanceCaseRow, object>> fallbackKey,
        bool fallbackDescending,
        CancellationToken cancellationToken)
    {
        var sortKey = fallbackKey;
        var descending = fallbackDescending;
        if (request.Sort is { Active: { Length: > 0 } active, Direction: { Length: > 0 } direction })
        {
            if (!TeamSortKeys.TryGetValue(active, out var chosen))
            {
                return Result.Failure<PagedResult<GrievanceCaseDto>>(AppError.Validation($"Cannot sort by \"{active}\".", "sort", "Choose a sortable field."));
            }

            sortKey = chosen;
            descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        }

        if (request.Search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<GrievanceCaseDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.", "search", $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var filtered = GrievanceQueries.Filter(source, request.Filters ?? new GrievanceCaseFilter(), read, clock);
        if (filtered.IsFailure)
        {
            return Result.Failure<PagedResult<GrievanceCaseDto>>(filtered.Error);
        }

        var cases = filtered.Value;
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            cases = cases.Where(Predicates.ContainsAny<GrievanceCase>(request.Search.Trim(), grievance => grievance.CaseNo, grievance => grievance.Subject));
        }

        var total = await read.CountAsync(cases, cancellationToken);
        var pageSize = Paging.ClampPageSize(request.PageSize);
        var pageIndex = Paging.ClampPageIndex(Math.Max(0, request.PageIndex), total, pageSize);
        var rows = Rows(cases);
        var ordered = (descending ? rows.OrderByDescending(sortKey) : rows.OrderBy(sortKey)).ThenBy(row => row.Id);
        var page = await read.ToListAsync(ordered.Skip(pageIndex * pageSize).Take(pageSize), cancellationToken);
        await NameAsync(page, cancellationToken);
        return Result.Success(new PagedResult<GrievanceCaseDto>([.. page.Select(row => GrievanceRedaction.ToCase(row, clock.Today))], total, pageIndex, pageSize));
    }

    private async Task NameAsync(IReadOnlyCollection<GrievanceCaseRow> rows, CancellationToken cancellationToken)
    {
        var ids = rows.SelectMany(row => new[] { row.SubmitterEmployeeId, row.LeadEmployeeId }).OfType<int>().Distinct().ToArray();
        var people = await PeopleAsync(ids, cancellationToken);
        foreach (var row in rows)
        {
            row.SubmitterName = row.IsAnonymous || row.SubmitterEmployeeId is not { } submitter ? null : people.GetValueOrDefault(submitter)?.DisplayName;
            row.LeadName = row.LeadEmployeeId is { } lead ? people.GetValueOrDefault(lead)?.DisplayName : null;
        }
    }

    /// <summary>Everything the case-team detail of <paramref name="grievance"/> is built from (the redaction trims it by tier).</summary>
    public async Task<GrievanceCaseBundle> BundleAsync(GrievanceCase grievance, CancellationToken cancellationToken)
    {
        var id = grievance.Id;
        var assignments = await read.ToListAsync(read.Query<GrievanceAssignment>().Where(assignment => assignment.CaseId == id), cancellationToken);
        var notes = await read.ToListAsync(read.Query<GrievanceInvestigationNote>().Where(note => note.CaseId == id), cancellationToken);
        var messages = await read.ToListAsync(read.Query<GrievanceCommunication>().Where(message => message.CaseId == id), cancellationToken);
        var attachments = await read.ToListAsync(read.Query<GrievanceAttachment>().Where(attachment => attachment.CaseId == id && attachment.RemovedAtUtc == null), cancellationToken);
        var actions = await read.ToListAsync(read.Query<GrievanceCorrectiveAction>().Where(action => action.CaseId == id), cancellationToken);
        var proposals = await read.ToListAsync(read.Query<GrievanceResolutionProposal>().Where(proposal => proposal.CaseId == id), cancellationToken);
        var escalations = await read.ToListAsync(read.Query<GrievanceEscalation>().Where(escalation => escalation.CaseId == id), cancellationToken);

        int? submitterEmployeeId = null;
        if (!grievance.IsAnonymous)
        {
            submitterEmployeeId = await read.FirstOrDefaultAsync(
                submitters.Query().Where(submitter => submitter.CaseId == id).Select(submitter => submitter.EmployeeId),
                cancellationToken);
        }

        var employeeIds = assignments.Select(assignment => (int?)assignment.InvestigatorEmployeeId)
            .Concat(proposals.Select(proposal => proposal.ProposedByEmployeeId))
            .Append(submitterEmployeeId)
            .Append(grievance.SubjectEmployeeId)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var people = await PeopleAsync(employeeIds, cancellationToken);
        var userIds = assignments.Select(assignment => (int?)assignment.AssignedByUserId)
            .Concat(assignments.Select(assignment => assignment.UnassignedByUserId))
            .Concat(proposals.Select(proposal => (int?)proposal.ProposedByUserId))
            .Concat(proposals.Select(proposal => proposal.DecidedByUserId))
            .Concat(escalations.Select(escalation => escalation.EscalatedByUserId))
            .OfType<int>()
            .Distinct()
            .ToArray();

        GrievanceNamedSubmitter? named = null;
        if (submitterEmployeeId is { } employee && people.TryGetValue(employee, out var person))
        {
            named = new GrievanceNamedSubmitter(
                person,
                await BranchNameAsync(grievance.BranchId, cancellationToken),
                await DepartmentNameAsync(grievance.DepartmentId, cancellationToken),
                await PositionTitleAsync(grievance.PositionId, cancellationToken));
        }

        return new GrievanceCaseBundle(
            grievance,
            await CategoryNameAsync(grievance.CategoryId, cancellationToken),
            await DepartmentNameAsync(grievance.InvolvedDepartmentId, cancellationToken),
            named,
            grievance.SubjectEmployeeId is { } subject ? people.GetValueOrDefault(subject)?.DisplayName : null,
            assignments,
            notes,
            messages,
            attachments,
            actions,
            proposals,
            escalations,
            people,
            await AccountNamesAsync(userIds, cancellationToken));
    }

    /// <summary>The named submitter of a case as a person (null for an anonymous case).</summary>
    public async Task<GrievancePerson?> NamedSubmitterAsync(GrievanceCase grievance, CancellationToken cancellationToken)
    {
        if (grievance.IsAnonymous)
        {
            return null;
        }

        var id = grievance.Id;
        var employee = await read.FirstOrDefaultAsync(
            submitters.Query().Where(submitter => submitter.CaseId == id).Select(submitter => submitter.EmployeeId),
            cancellationToken);
        return employee is { } employeeId ? (await PeopleAsync([employeeId], cancellationToken)).GetValueOrDefault(employeeId) : null;
    }

    public async Task<IReadOnlyDictionary<int, GrievancePerson>> PeopleAsync(int[] employeeIds, CancellationToken cancellationToken)
    {
        if (employeeIds.Length == 0)
        {
            return new Dictionary<int, GrievancePerson>();
        }

        var rows = await read.ToListAsync(
            read.Query<Employee>().Where(employee => employeeIds.Contains(employee.Id))
                .Select(employee => new { employee.Id, employee.EmployeeCode, employee.FirstName, employee.LastName }),
            cancellationToken);
        return rows.ToDictionary(row => row.Id, row => new GrievancePerson(row.Id, row.EmployeeCode, $"{row.FirstName} {row.LastName}".Trim()));
    }

    public async Task<IReadOnlyDictionary<int, string>> AccountNamesAsync(int[] userIds, CancellationToken cancellationToken)
    {
        if (userIds.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        var rows = await read.ToListAsync(
            read.Query<AppAccount>().Where(account => userIds.Contains(account.Id)).Select(account => new { account.Id, account.DisplayName }),
            cancellationToken);
        return rows.ToDictionary(row => row.Id, row => row.DisplayName);
    }

    public async Task<string> CategoryNameAsync(int categoryId, CancellationToken cancellationToken) =>
        await read.FirstOrDefaultAsync(read.Query<GrievanceCategory>().Where(category => category.Id == categoryId).Select(category => category.Name), cancellationToken)
            ?? string.Empty;

    private async Task<string?> DepartmentNameAsync(int? departmentId, CancellationToken cancellationToken) =>
        departmentId is { } id
            ? await read.FirstOrDefaultAsync(read.Query<Department>().Where(department => department.Id == id).Select(department => department.Name), cancellationToken)
            : null;

    private async Task<string?> BranchNameAsync(int? branchId, CancellationToken cancellationToken) =>
        branchId is { } id
            ? await read.FirstOrDefaultAsync(read.Query<Branch>().Where(branch => branch.Id == id).Select(branch => branch.Name), cancellationToken)
            : null;

    private async Task<string?> PositionTitleAsync(int? positionId, CancellationToken cancellationToken) =>
        positionId is { } id
            ? await read.FirstOrDefaultAsync(read.Query<Position>().Where(position => position.Id == id).Select(position => position.Title), cancellationToken)
            : null;
}

/// <summary>The filters of the case-team lists, as database predicates.</summary>
internal static class GrievanceQueries
{
    /// <summary>
    /// An open case past its target date. The open statuses are written out as constants so the database gets a literal IN list (the
    /// filtered index IX_Case_TargetResolutionDate_Open names the same statuses), never a parameter list.
    /// </summary>
    public static Expression<Func<GrievanceCase, bool>> Overdue(DateOnly today) => grievance =>
        (grievance.Status == GrievanceStatus.Submitted
            || grievance.Status == GrievanceStatus.Acknowledged
            || grievance.Status == GrievanceStatus.UnderReview
            || grievance.Status == GrievanceStatus.Assigned
            || grievance.Status == GrievanceStatus.UnderInvestigation
            || grievance.Status == GrievanceStatus.AwaitingEmployeeResponse
            || grievance.Status == GrievanceStatus.ResolutionProposed
            || grievance.Status == GrievanceStatus.Reopened)
        && grievance.TargetResolutionDate != null
        && grievance.TargetResolutionDate < today;

    /// <summary>The same open statuses as a literal IN list.</summary>
    public static Expression<Func<GrievanceCase, bool>> IsOpen() => grievance =>
        grievance.Status == GrievanceStatus.Submitted
        || grievance.Status == GrievanceStatus.Acknowledged
        || grievance.Status == GrievanceStatus.UnderReview
        || grievance.Status == GrievanceStatus.Assigned
        || grievance.Status == GrievanceStatus.UnderInvestigation
        || grievance.Status == GrievanceStatus.AwaitingEmployeeResponse
        || grievance.Status == GrievanceStatus.ResolutionProposed
        || grievance.Status == GrievanceStatus.Reopened;

    public static Result<IQueryable<GrievanceCase>> Filter(IQueryable<GrievanceCase> source, GrievanceCaseFilter filter, IReadModel read, IClock clock)
    {
        var cases = source;
        if (filter.Status is { Length: > 0 } statusText)
        {
            if (!GrievanceText.Status.TryParse(statusText, out var status))
            {
                return Invalid("status", "Choose a valid status.");
            }

            cases = cases.Where(grievance => grievance.Status == status);
        }

        if (filter.Priority is { Length: > 0 } priorityText)
        {
            if (!GrievanceText.Priority.TryParse(priorityText, out var priority))
            {
                return Invalid("priority", "Choose a valid priority.");
            }

            cases = cases.Where(grievance => grievance.Priority == priority);
        }

        if (filter.CategoryId is { } categoryId)
        {
            cases = cases.Where(grievance => grievance.CategoryId == categoryId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            // An anonymous case has no department, so it never matches.
            cases = cases.Where(grievance => !grievance.IsAnonymous && grievance.DepartmentId == departmentId);
        }

        if (filter.IsAnonymous is { } anonymous)
        {
            cases = cases.Where(grievance => grievance.IsAnonymous == anonymous);
        }

        if (filter.IsEscalated is { } escalated)
        {
            cases = cases.Where(grievance => grievance.IsEscalated == escalated);
        }

        if (filter.Overdue is { } overdue)
        {
            var isOverdue = Overdue(clock.Today);
            cases = overdue ? cases.Where(isOverdue) : cases.Where(Not(isOverdue));
        }

        cases = filter.Archived == true
            ? cases.Where(grievance => grievance.ArchivedAtUtc != null)
            : cases.Where(grievance => grievance.ArchivedAtUtc == null);

        if (filter.InvestigatorEmployeeId is { } investigator)
        {
            var assigned = read.Query<GrievanceAssignment>()
                .Where(assignment => assignment.InvestigatorEmployeeId == investigator && assignment.UnassignedAtUtc == null)
                .Select(assignment => assignment.CaseId);
            cases = cases.Where(grievance => assigned.Contains(grievance.Id));
        }

        if (filter.SubmittedFrom is { } from)
        {
            var start = clock.StartOfDayUtc(from);
            cases = cases.Where(grievance => grievance.SubmittedAtUtc >= start);
        }

        if (filter.SubmittedTo is { } to)
        {
            var end = clock.StartOfDayUtc(to.AddDays(1));
            cases = cases.Where(grievance => grievance.SubmittedAtUtc < end);
        }

        return Result.Success(cases);
    }

    private static Expression<Func<T, bool>> Not<T>(Expression<Func<T, bool>> predicate) =>
        Expression.Lambda<Func<T, bool>>(Expression.Not(predicate.Body), predicate.Parameters);

    private static Result<IQueryable<GrievanceCase>> Invalid(string field, string message) =>
        Result.Failure<IQueryable<GrievanceCase>>(AppError.Validation(message, field, message));
}
