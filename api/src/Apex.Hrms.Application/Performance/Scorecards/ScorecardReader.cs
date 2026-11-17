using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Organization;
using Apex.Hrms.Domain.Performance;

namespace Apex.Hrms.Application.Performance.Scorecards;

/// <summary>
/// Reads scorecards as read models: one projection joins the scorecard to its employee, period and department and counts the
/// items, so a page of the list costs the same few queries whatever its size. <see cref="Scoped"/> is the only source the
/// scorecard read endpoints use; rows outside the scope of the caller (see <c>ScorecardScope</c>) are simply not there.
/// </summary>
internal sealed class ScorecardReader(IReadModel read, IAccessScope<Scorecard> scope, ICurrentUser user)
{
    /// <summary>The scorecards the caller may see.</summary>
    public IQueryable<Scorecard> Scoped() => read.Query<Scorecard>().Where(scope.PredicateFor(user));

    /// <summary>Every scorecard, for the HR writes that answer with the scorecard they just changed.</summary>
    public IQueryable<Scorecard> Unscoped() => read.Query<Scorecard>();

    /// <summary>The row version of a scorecard as the database holds it, or null.</summary>
    public Task<byte[]?> RowVersionAsync(int id, CancellationToken cancellationToken) =>
        read.FirstOrDefaultAsync(
            read.Query<Scorecard>().Where(scorecard => scorecard.Id == id).Select(scorecard => scorecard.RowVersion),
            cancellationToken);

    public async Task<Result<ScorecardDetailDto>> DetailAsync(IQueryable<Scorecard> source, int id, CancellationToken cancellationToken)
    {
        var row = await read.FirstOrDefaultAsync(Rows(source.Where(scorecard => scorecard.Id == id)), cancellationToken);
        if (row is null)
        {
            return Result.Failure<ScorecardDetailDto>(AppError.NotFound($"Scorecard #{id} was not found."));
        }

        var names = await ReviewerNamesAsync([row], cancellationToken);
        var items = await read.ToListAsync(
            read.Query<ScorecardItem>().Where(item => item.ScorecardId == id).OrderBy(item => item.SortOrder).ThenBy(item => item.Id),
            cancellationToken);
        var itemIds = items.Select(item => item.Id).ToArray();
        var files = itemIds.Length == 0
            ? []
            : (await read.ToListAsync(
                read.Query<KpiAttachment>().Where(attachment => itemIds.Contains(attachment.ScorecardItemId)).OrderBy(attachment => attachment.Id),
                cancellationToken))
                .GroupBy(attachment => attachment.ScorecardItemId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<AttachmentDto>)[.. group.Select(ScorecardMapping.ToDto)]);
        var categoryIds = items.Select(item => item.CategoryId).Distinct().ToArray();
        var categories = categoryIds.Length == 0
            ? []
            : (await read.ToListAsync(
                read.Query<KpiCategory>().Where(category => categoryIds.Contains(category.Id)).Select(category => new { category.Id, category.Name }),
                cancellationToken)).ToDictionary(category => category.Id, category => category.Name);
        var history = await read.ToListAsync(
            read.Query<ReviewAction>().Where(action => action.ScorecardId == id).OrderBy(action => action.OccurredAtUtc).ThenBy(action => action.Id),
            cancellationToken);

        var extras = await read.FirstOrDefaultAsync(
            from scorecard in read.Query<Scorecard>().Where(scorecard => scorecard.Id == id)
            join period in read.Query<EvaluationPeriod>() on scorecard.PeriodId equals period.Id
            select new ScorecardExtras
            {
                ReviewedOn = scorecard.ReviewedOn,
                EmployeeComment = scorecard.EmployeeComment,
                ManagerRating = scorecard.ManagerRating,
                ManagerComment = scorecard.ManagerComment,
                HrComment = scorecard.HrComment,
                PeriodStatus = period.Status,
                SubmissionDueDate = period.SubmissionDueDate,
                RequiresHrReview = period.RequiresHrReview,
            },
            cancellationToken);

        return Result.Success(ScorecardMapping.ToDetail(
            ScorecardMapping.ToDto(row, names),
            [.. items.Select(item => ScorecardMapping.ToItemDto(item, categories.GetValueOrDefault(item.CategoryId, string.Empty), files.GetValueOrDefault(item.Id)))],
            [.. history.Select(ScorecardMapping.ToDto)],
            extras ?? new ScorecardExtras()));
    }

    /// <summary>
    /// One page of scorecards from <paramref name="source"/> (already limited to what the caller may see and to the purpose of the
    /// list): the filters of the request, the search on the employee, the sort whitelist (<paramref name="fallbackKey"/> and
    /// <paramref name="fallbackDescending"/> when the request names none; the id always breaks ties) and the paging, all in the database.
    /// </summary>
    public async Task<Result<PagedResult<ScorecardDto>>> PageAsync(
        IQueryable<Scorecard> source,
        PageRequest<ScorecardFilter> request,
        Expression<Func<ScorecardRow, object>>? fallbackKey,
        bool fallbackDescending,
        CancellationToken cancellationToken)
    {
        var sortKey = fallbackKey;
        var descending = fallbackDescending;
        if (request.Sort is { Active: { Length: > 0 } active, Direction: { Length: > 0 } direction })
        {
            if (!ScorecardQuerySpecification.SortKeys.TryGetValue(active, out sortKey))
            {
                return Result.Failure<PagedResult<ScorecardDto>>(
                    AppError.Validation($"Cannot sort by \"{active}\".", "sort", "Choose a sortable field."));
            }

            descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        }

        if (request.Search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<ScorecardDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.",
                "search",
                $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var scorecards = source.Where(ScorecardQuerySpecification.Filter(request.Filters));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var matching = read.Query<Employee>().Where(ScorecardQuerySpecification.Search(request.Search.Trim())).Select(employee => employee.Id);
            scorecards = scorecards.Where(scorecard => matching.Contains(scorecard.EmployeeId));
        }

        var total = await read.CountAsync(scorecards, cancellationToken);
        var pageSize = Paging.ClampPageSize(request.PageSize);
        var pageIndex = Paging.ClampPageIndex(Math.Max(0, request.PageIndex), total, pageSize);

        var rows = Rows(scorecards);
        var ordered = sortKey is null
            ? rows.OrderBy(row => row.Id)
            : (descending ? rows.OrderByDescending(sortKey) : rows.OrderBy(sortKey)).ThenBy(row => row.Id);
        var page = await read.ToListAsync(ordered.Skip(pageIndex * pageSize).Take(pageSize), cancellationToken);
        var reviewers = await ReviewerNamesAsync(page, cancellationToken);

        return Result.Success(new PagedResult<ScorecardDto>(
            [.. page.Select(row => ScorecardMapping.ToDto(row, reviewers))],
            total,
            pageIndex,
            pageSize));
    }

    public IQueryable<ScorecardRow> Rows(IQueryable<Scorecard> source)
    {
        var employees = read.Query<Employee>();
        var periods = read.Query<EvaluationPeriod>();
        var departments = read.Query<Department>();
        var items = read.Query<ScorecardItem>();
        return from scorecard in source
               join employee in employees on scorecard.EmployeeId equals employee.Id
               join period in periods on scorecard.PeriodId equals period.Id
               join department in departments on scorecard.DepartmentId equals department.Id
               select new ScorecardRow
               {
                   Id = scorecard.Id,
                   EmployeeId = scorecard.EmployeeId,
                   EmployeeCode = employee.EmployeeCode,
                   FirstName = employee.FirstName,
                   LastName = employee.LastName,
                   PeriodId = scorecard.PeriodId,
                   PeriodName = period.Name,
                   PeriodStartDate = period.StartDate,
                   DepartmentId = scorecard.DepartmentId,
                   DepartmentName = department.Name,
                   Status = scorecard.Status,
                   ItemCount = items.Count(item => item.ScorecardId == scorecard.Id),
                   TotalWeight = items.Where(item => item.ScorecardId == scorecard.Id).Sum(item => (decimal?)item.Weight) ?? 0m,
                   OverallScore = scorecard.OverallScore,
                   RatingLabel = scorecard.RatingLabel,
                   ReviewerEmployeeId = scorecard.ReviewerEmployeeId,
                   PlanFinalizedOn = scorecard.PlanFinalizedOn,
                   SubmittedOn = scorecard.SubmittedOn,
                   FinalizedOn = scorecard.FinalizedOn,
                   ModifiedAtUtc = scorecard.ModifiedAtUtc,
                   RowVersion = scorecard.RowVersion,
               };
    }

    /// <summary>The names of the reviewers of a page of rows, in one query.</summary>
    public async Task<IReadOnlyDictionary<int, string>> ReviewerNamesAsync(IReadOnlyCollection<ScorecardRow> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Where(row => row.ReviewerEmployeeId is not null).Select(row => row.ReviewerEmployeeId!.Value).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        var people = await read.ToListAsync(
            read.Query<Employee>().Where(employee => ids.Contains(employee.Id)).Select(employee => new { employee.Id, employee.FirstName, employee.LastName }),
            cancellationToken);
        return people.ToDictionary(person => person.Id, person => ScorecardMapping.FullName(person.FirstName, person.LastName));
    }
}

/// <summary>What the scorecard list can filter and sort by.</summary>
internal static class ScorecardQuerySpecification
{
    public static IReadOnlyDictionary<string, Expression<Func<ScorecardRow, object>>> SortKeys { get; } =
        new Dictionary<string, Expression<Func<ScorecardRow, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = row => row.Id,
            ["employeeCode"] = row => row.EmployeeCode,
            ["employeeName"] = row => row.FirstName + " " + row.LastName,
            ["periodName"] = row => row.PeriodName,
            ["periodStart"] = row => row.PeriodStartDate,
            ["submittedOn"] = row => row.SubmittedOn ?? DateOnly.MinValue,
            ["departmentName"] = row => row.DepartmentName,
            ["status"] = row => row.Status,
            ["overallScore"] = row => row.OverallScore ?? 0m,
            ["planFinalizedOn"] = row => row.PlanFinalizedOn ?? DateOnly.MinValue,
            ["modifiedAt"] = row => row.ModifiedAtUtc,
        };

    public static Expression<Func<Scorecard, bool>> Filter(ScorecardFilter filter)
    {
        var predicate = Predicates.Always<Scorecard>();
        if (filter.PeriodId is { } periodId)
        {
            predicate = predicate.And(scorecard => scorecard.PeriodId == periodId);
        }

        if (filter.EmployeeId is { } employeeId)
        {
            predicate = predicate.And(scorecard => scorecard.EmployeeId == employeeId);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            predicate = predicate.And(scorecard => scorecard.DepartmentId == departmentId);
        }

        if (filter.CompanyId is { } companyId)
        {
            predicate = predicate.And(scorecard => scorecard.CompanyId == companyId);
        }

        if (FilterPredicates.IsSet(filter.Status))
        {
            predicate = ScorecardStatusText.TryParse(filter.Status?.Trim(), out var status)
                ? predicate.And(scorecard => scorecard.Status == status)
                : Predicates.Never<Scorecard>();
        }

        return predicate;
    }

    /// <summary>The employees whose code or name contains the term.</summary>
    public static Expression<Func<Employee, bool>> Search(string term) =>
        Predicates.ContainsAny<Employee>(term, employee => employee.EmployeeCode, employee => employee.FirstName + " " + employee.LastName);
}
