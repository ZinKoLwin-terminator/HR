using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.UseCases;
using Apex.Hrms.Domain.Common;
using Apex.Hrms.Domain.Employees;
using Apex.Hrms.Domain.Recruitment;

namespace Apex.Hrms.Application.Employees.Blacklist;

/// <summary>
/// Tells whether a candidate matches an employee on the active do-not-rehire list, and nothing else: callers learn a
/// boolean, never which employee or why. A candidate matches when their email equals the work or personal email of a
/// listed employee, their phone (digits only) equals the phone or alternate phone, or their national id (when given)
/// equals the employee's. Lifted entries do not count. The list is read on every call (once per page or command), so a lift shows at once.
/// </summary>
public interface IBlacklistMatcher
{
    Task<bool> MatchesAsync(Candidate candidate, CancellationToken cancellationToken);

    /// <summary>The ids among <paramref name="candidateIds"/> that match.</summary>
    Task<IReadOnlySet<int>> MatchingCandidateIdsAsync(IReadOnlyCollection<int> candidateIds, CancellationToken cancellationToken);
}

internal sealed class BlacklistMatcher(
    IRepository<EmployeeBlacklistEntry> entries,
    IRepository<Employee> employees,
    IRepository<Candidate> candidates) : IBlacklistMatcher
{
    public async Task<bool> MatchesAsync(Candidate candidate, CancellationToken cancellationToken) =>
        (await LoadAsync(cancellationToken)).Matches(candidate);

    public async Task<IReadOnlySet<int>> MatchingCandidateIdsAsync(IReadOnlyCollection<int> candidateIds, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(cancellationToken);
        if (loaded.IsEmpty || candidateIds.Count == 0)
        {
            return new HashSet<int>();
        }

        var ids = candidateIds.Distinct().ToArray();
        var rows = await candidates.ListAsync(candidate => ids.Contains(candidate.Id), cancellationToken);
        return rows.Where(loaded.Matches).Select(candidate => candidate.Id).ToHashSet();
    }

    private async Task<Index> LoadAsync(CancellationToken cancellationToken)
    {
        // Read on every call: a request needs the list once, and a lift must show at once.
        var active = await entries.ListAsync(entry => entry.LiftedOn == null, cancellationToken);
        var employeeIds = active.Select(entry => entry.EmployeeId).Distinct().ToArray();
        var listed = employeeIds.Length == 0
            ? []
            : await employees.ListAsync(employee => employeeIds.Contains(employee.Id), cancellationToken);
        return Index.Of(listed);
    }

    private sealed class Index
    {
        private readonly HashSet<string> emails = [];
        private readonly HashSet<string> phones = [];
        private readonly HashSet<string> nationalIds = [];

        public bool IsEmpty => emails.Count == 0 && phones.Count == 0 && nationalIds.Count == 0;

        public static Index Of(IEnumerable<Employee> listed)
        {
            var index = new Index();
            foreach (var employee in listed)
            {
                Add(index.emails, BlacklistMatching.NormalizeEmail(employee.WorkEmail));
                Add(index.emails, BlacklistMatching.NormalizeEmail(employee.PersonalEmail));
                Add(index.phones, BlacklistMatching.NormalizePhone(employee.Phone));
                Add(index.phones, BlacklistMatching.NormalizePhone(employee.AlternatePhone));
                Add(index.nationalIds, BlacklistMatching.NormalizeNationalId(employee.NationalId));
            }

            return index;
        }

        public bool Matches(Candidate candidate) =>
            Contains(emails, BlacklistMatching.NormalizeEmail(candidate.Email))
            || Contains(phones, BlacklistMatching.NormalizePhone(candidate.Phone))
            || Contains(nationalIds, BlacklistMatching.NormalizeNationalId(candidate.NationalId));

        private static void Add(HashSet<string> set, string? value)
        {
            if (value is not null)
            {
                set.Add(value);
            }
        }

        private static bool Contains(HashSet<string> set, string? value) => value is not null && set.Contains(value);
    }
}

/// <summary>Sets <c>blacklistMatch</c> on candidate and application responses (see <see cref="IBlacklistMatcher"/>).</summary>
internal abstract class BlacklistFlagging<TDto>(IBlacklistMatcher matcher)
{
    protected abstract int CandidateIdOf(TDto dto);

    protected abstract TDto With(TDto dto, bool match);

    public async Task<TDto> FlagAsync(TDto dto, CancellationToken cancellationToken) =>
        (await FlagAsync([dto], cancellationToken))[0];

    public async Task<IReadOnlyList<TDto>> FlagAsync(IReadOnlyList<TDto> dtos, CancellationToken cancellationToken)
    {
        if (dtos.Count == 0)
        {
            return dtos;
        }

        var matching = await matcher.MatchingCandidateIdsAsync([.. dtos.Select(CandidateIdOf)], cancellationToken);
        return matching.Count == 0 ? dtos : [.. dtos.Select(dto => With(dto, matching.Contains(CandidateIdOf(dto))))];
    }
}

internal sealed class CandidateFlagging(IBlacklistMatcher matcher) : BlacklistFlagging<Recruitment.Candidates.CandidateDto>(matcher)
{
    protected override int CandidateIdOf(Recruitment.Candidates.CandidateDto dto) => dto.Id;

    protected override Recruitment.Candidates.CandidateDto With(Recruitment.Candidates.CandidateDto dto, bool match) =>
        dto with { BlacklistMatch = match };
}

internal sealed class ApplicationFlagging(IBlacklistMatcher matcher) : BlacklistFlagging<Recruitment.Applications.JobApplicationDto>(matcher)
{
    protected override int CandidateIdOf(Recruitment.Applications.JobApplicationDto dto) => dto.CandidateId;

    protected override Recruitment.Applications.JobApplicationDto With(Recruitment.Applications.JobApplicationDto dto, bool match) =>
        dto with { BlacklistMatch = match };
}

// ---- Decorators: the same handler interfaces, with the flag set on what they return. ----

internal sealed class FlaggedGetAllHandler<TDto>(IGetAllHandler<TDto> inner, BlacklistFlagging<TDto> flagging) : IGetAllHandler<TDto>
{
    public async Task<Result<IReadOnlyList<TDto>>> HandleAsync(CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(cancellationToken);
        return result.IsFailure ? result : Result.Success(await flagging.FlagAsync(result.Value, cancellationToken));
    }
}

internal sealed class FlaggedGetByIdHandler<TDto>(IGetByIdHandler<TDto> inner, BlacklistFlagging<TDto> flagging) : IGetByIdHandler<TDto>
{
    public async Task<Result<TDto>> HandleAsync(int id, CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(id, cancellationToken);
        return result.IsFailure ? result : Result.Success(await flagging.FlagAsync(result.Value, cancellationToken));
    }
}

internal sealed class FlaggedQueryHandler<TDto, TFilter>(IQueryHandler<TDto, TFilter> inner, BlacklistFlagging<TDto> flagging) : IQueryHandler<TDto, TFilter>
{
    public async Task<Result<PagedResult<TDto>>> HandleAsync(PageRequest<TFilter> request, CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return result;
        }

        var page = result.Value;
        return Result.Success(page with { Items = await flagging.FlagAsync(page.Items, cancellationToken) });
    }
}

internal sealed class FlaggedCreateHandler<TRequest, TDto>(ICreateHandler<TRequest, TDto> inner, BlacklistFlagging<TDto> flagging) : ICreateHandler<TRequest, TDto>
{
    public async Task<Result<TDto>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(request, cancellationToken);
        return result.IsFailure ? result : Result.Success(await flagging.FlagAsync(result.Value, cancellationToken));
    }
}

internal sealed class FlaggedUpdateHandler<TRequest, TDto>(IUpdateHandler<TRequest, TDto> inner, BlacklistFlagging<TDto> flagging) : IUpdateHandler<TRequest, TDto>
{
    public async Task<Result<TDto>> HandleAsync(int id, TRequest request, CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(id, request, cancellationToken);
        return result.IsFailure ? result : Result.Success(await flagging.FlagAsync(result.Value, cancellationToken));
    }
}

internal sealed class FlaggedCommandHandler<TCommand, TDto>(ICommandHandler<TCommand, TDto> inner, BlacklistFlagging<TDto> flagging) : ICommandHandler<TCommand, TDto>
    where TCommand : IEntityCommand
{
    public async Task<Result<TDto>> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var result = await inner.HandleAsync(command, cancellationToken);
        return result.IsFailure ? result : Result.Success(await flagging.FlagAsync(result.Value, cancellationToken));
    }
}
