using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Activity;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;

namespace Apex.Hrms.Application.Employees.Blacklist;

/// <summary>
/// The do-not-rehire list. Reads and writes need <c>employees:ViewBlacklist</c> / <c>EditBlacklist</c> (checked by the host;
/// only HR Administrator holds them in the seed), so entries are not scoped by employee. Only a former employee (one with an
/// exit date) can be listed, and one employee has at most one active entry. Activity entries name the employee by code and
/// never carry the reason.
/// </summary>
internal sealed class BlacklistHandler(
    IRepository<EmployeeBlacklistEntry> entries,
    IRepository<Employee> employees,
    EntityLoader<EmployeeBlacklistEntry> loader,
    ICurrentUser user,
    IClock clock,
    IActivityRecorder activity,
    IUnitOfWork unitOfWork) : IBlacklistHandler
{
    public const string FormerOnlyMessage = "Only a former employee can be put on the do-not-rehire list.";
    public const string AlreadyListedMessage = "This employee is already on the do-not-rehire list.";

    public async Task<Result<PagedResult<BlacklistEntryDto>>> QueryAsync(PageRequest<BlacklistFilter> request, CancellationToken cancellationToken)
    {
        var sort = ResolveSort(request.Sort);
        if (sort.IsFailure)
        {
            return Result.Failure<PagedResult<BlacklistEntryDto>>(sort.Error);
        }

        if (request.Search is { Length: > Paging.MaxSearchLength })
        {
            return Result.Failure<PagedResult<BlacklistEntryDto>>(AppError.Validation(
                $"Search must be at most {Paging.MaxSearchLength} characters.",
                "search",
                $"Search must be at most {Paging.MaxSearchLength} characters."));
        }

        var predicate = BlacklistSorting.Filter(request.Filters ?? new BlacklistFilter());
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var matching = await employees.ListAsync(
                Predicates.ContainsAny<Employee>(term, e => e.EmployeeCode, e => e.FirstName, e => e.LastName),
                cancellationToken);
            var ids = matching.Select(e => e.Id).ToArray();
            predicate = predicate.And(entry => ids.Contains(entry.EmployeeId));
        }

        var page = await entries.QueryAsync(
            new EntityQuery<EmployeeBlacklistEntry>(predicate, sort.Value, Math.Max(0, request.PageIndex), Paging.ClampPageSize(request.PageSize)),
            cancellationToken);
        var items = await ToDtosAsync(page.Items, cancellationToken);
        return Result.Success(new PagedResult<BlacklistEntryDto>(items, page.Total, page.PageIndex, page.PageSize));
    }

    public async Task<Result<BlacklistEntryDto>> GetAsync(int id, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(id, cancellationToken);
        return loaded.IsFailure
            ? Result.Failure<BlacklistEntryDto>(loaded.Error)
            : Result.Success((await ToDtosAsync([loaded.Value], cancellationToken))[0]);
    }

    public async Task<Result<BlacklistEntryDto>> GetActiveOfEmployeeAsync(int employeeId, CancellationToken cancellationToken)
    {
        var employee = await employees.FindAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return Result.Failure<BlacklistEntryDto>(NotFoundError.For("Employee", employeeId));
        }

        var active = await entries.ListAsync(e => e.EmployeeId == employeeId && e.LiftedOn == null, cancellationToken);
        return active.Count == 0
            ? Result.Failure<BlacklistEntryDto>(AppError.NotFound($"Employee {employee.EmployeeCode} is not on the do-not-rehire list."))
            : Result.Success((await ToDtosAsync([active[0]], cancellationToken))[0]);
    }

    public async Task<Result<BlacklistEntryDto>> AddAsync(BlacklistAddRequest request, CancellationToken cancellationToken)
    {
        var reason = EmployeeBlacklistEntry.ValidateReason(request.Reason);
        if (reason.IsFailure)
        {
            return Result.Failure<BlacklistEntryDto>(reason.Error);
        }

        if (user.UserId is not { } userId)
        {
            return Result.Failure<BlacklistEntryDto>(AppError.Unauthorized("Sign in to change the do-not-rehire list."));
        }

        var employee = request.EmployeeId > 0 ? await employees.FindAsync(request.EmployeeId, cancellationToken) : null;
        if (employee is null)
        {
            return Result.Failure<BlacklistEntryDto>(AppError.Validation("Choose an employee.", "employeeId", "Choose an employee."));
        }

        if (employee.ExitDate is null)
        {
            return Result.Failure<BlacklistEntryDto>(AppError.Conflict(FormerOnlyMessage));
        }

        var employeeId = employee.Id;
        if (await entries.AnyAsync(e => e.EmployeeId == employeeId && e.LiftedOn == null, cancellationToken))
        {
            return Result.Failure<BlacklistEntryDto>(AppError.Conflict(AlreadyListedMessage));
        }

        var created = EmployeeBlacklistEntry.Create(employeeId, request.Reason, clock.Today, userId, user.DisplayName);
        if (created.IsFailure)
        {
            return Result.Failure<BlacklistEntryDto>(created.Error);
        }

        entries.Add(created.Value);
        activity.Record(Domain.Activity.ActivityCategories.Employee, ActivityMessages.AddedToDoNotRehire(employee.EmployeeCode));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<BlacklistEntryDto>(saved.Error)
            : Result.Success(ToDto(created.Value, employee));
    }

    public async Task<Result<BlacklistEntryDto>> LiftAsync(int id, BlacklistLiftRequest request, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadForChangeAsync(id, request.RowVersion, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<BlacklistEntryDto>(loaded.Error);
        }

        if (user.UserId is not { } userId)
        {
            return Result.Failure<BlacklistEntryDto>(AppError.Unauthorized("Sign in to change the do-not-rehire list."));
        }

        var entry = loaded.Value;
        var lifted = entry.Lift(request.Reason, clock.Today, userId, user.DisplayName);
        if (lifted.IsFailure)
        {
            return Result.Failure<BlacklistEntryDto>(lifted.Error);
        }

        var employee = await employees.FindAsync(entry.EmployeeId, cancellationToken);
        activity.Record(
            Domain.Activity.ActivityCategories.Employee,
            ActivityMessages.RemovedFromDoNotRehire(employee?.EmployeeCode ?? $"#{entry.EmployeeId}"));
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsFailure
            ? Result.Failure<BlacklistEntryDto>(saved.Error)
            : Result.Success(ToDto(entry, employee));
    }

    private static Result<SortCriterion<EmployeeBlacklistEntry>?> ResolveSort(SortState? sort)
    {
        if (sort is null || string.IsNullOrWhiteSpace(sort.Active) || string.IsNullOrEmpty(sort.Direction))
        {
            return Result.Success<SortCriterion<EmployeeBlacklistEntry>?>(new SortCriterion<EmployeeBlacklistEntry>(e => e.BlacklistedOn, true));
        }

        return BlacklistSorting.Keys.TryGetValue(sort.Active, out var key)
            ? Result.Success<SortCriterion<EmployeeBlacklistEntry>?>(
                new SortCriterion<EmployeeBlacklistEntry>(key, string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase)))
            : Result.Failure<SortCriterion<EmployeeBlacklistEntry>?>(
                AppError.Validation($"Cannot sort by \"{sort.Active}\".", "sort", "Choose a sortable field."));
    }

    private async Task<IReadOnlyList<BlacklistEntryDto>> ToDtosAsync(IReadOnlyList<EmployeeBlacklistEntry> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(e => e.EmployeeId).Distinct().ToArray();
        var people = ids.Length == 0
            ? new Dictionary<int, Employee>()
            : (await employees.ListAsync(e => ids.Contains(e.Id), cancellationToken)).ToDictionary(e => e.Id);
        return [.. items.Select(entry => ToDto(entry, people.GetValueOrDefault(entry.EmployeeId)))];
    }

    private static BlacklistEntryDto ToDto(EmployeeBlacklistEntry entry, Employee? employee) => new(
        entry.Id,
        entry.EmployeeId,
        employee?.EmployeeCode ?? $"#{entry.EmployeeId}",
        employee is null ? $"Employee #{entry.EmployeeId}" : $"{employee.FirstName} {employee.LastName}",
        entry.Reason,
        entry.BlacklistedOn,
        entry.BlacklistedByName,
        entry.LiftedOn,
        entry.LiftedByName,
        entry.LiftReason,
        entry.RowVersion);
}
