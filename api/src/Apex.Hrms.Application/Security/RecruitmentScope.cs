using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Recruitment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apex.Hrms.Application.Security;

/// <summary>
/// The recruitment records a hiring manager may see: the openings they are hiring manager of, the applications and
/// interviews of those openings, and the candidates who applied to them. Filled for the current request by the host's
/// permission check (only for callers who hold recruitment:View but not recruitment:Edit); until then it is empty,
/// which is the safe side (nothing).
/// </summary>
public interface ICallerRecruitment
{
    IReadOnlyList<int> OpeningIds { get; }

    IReadOnlyList<int> ApplicationIds { get; }

    IReadOnlyList<int> CandidateIds { get; }
}

/// <summary>The per-request holder behind <see cref="ICallerRecruitment"/>.</summary>
public sealed class CallerRecruitment : ICallerRecruitment
{
    public bool IsLoaded { get; private set; }

    public IReadOnlyList<int> OpeningIds { get; private set; } = [];

    public IReadOnlyList<int> ApplicationIds { get; private set; } = [];

    public IReadOnlyList<int> CandidateIds { get; private set; } = [];

    public void Set(ManagedRecruitment managed)
    {
        OpeningIds = managed.OpeningIds;
        ApplicationIds = managed.ApplicationIds;
        CandidateIds = managed.CandidateIds;
        IsLoaded = true;
    }
}

/// <summary>Ids of the recruitment records of the openings one employee is hiring manager of.</summary>
public sealed record ManagedRecruitment(IReadOnlyList<int> OpeningIds, IReadOnlyList<int> ApplicationIds, IReadOnlyList<int> CandidateIds);

/// <summary>Reads what a hiring manager manages. Implemented by the persistence layer.</summary>
public interface IRecruitmentDirectory
{
    Task<ManagedRecruitment> ManagedByAsync(int hiringManagerEmployeeId, CancellationToken cancellationToken);
}

/// <summary>
/// "HR all, managers own": callers holding <c>recruitment:Edit</c> (Recruiter, HR Officer, HR Administrator in the seed) see
/// every opening, candidate, application and interview. Anyone else with <c>recruitment:View</c> (Department Manager) sees only
/// what belongs to openings they are hiring manager of; a caller with no linked employee sees nothing.
/// </summary>
internal sealed class RecruitmentAccess(ICurrentUser user, ICallerPermissions permissions, ICallerRecruitment managed)
{
    public bool SeesEverything => permissions.Has("recruitment", "Edit");

    public int? OwnEmployeeId => user.EmployeeId;

    public IReadOnlyList<int> OpeningIds => managed.OpeningIds;

    public IReadOnlyList<int> ApplicationIds => managed.ApplicationIds;

    public IReadOnlyList<int> CandidateIds => managed.CandidateIds;

    public Expression<Func<T, bool>> Rows<T>(Func<int[], Expression<Func<T, bool>>> inSet, IReadOnlyList<int> ids)
    {
        if (SeesEverything)
        {
            return Predicates.Always<T>();
        }

        return user.EmployeeId is null || ids.Count == 0 ? Predicates.Never<T>() : inSet([.. ids]);
    }
}

internal sealed class JobOpeningScope(RecruitmentAccess access) : IAccessScope<JobOpening>
{
    public Expression<Func<JobOpening, bool>> PredicateFor(ICurrentUser user)
    {
        if (access.SeesEverything)
        {
            return Predicates.Always<JobOpening>();
        }

        return access.OwnEmployeeId is { } own
            ? opening => opening.HiringManagerEmployeeId == own
            : Predicates.Never<JobOpening>();
    }
}

internal sealed class CandidateScope(RecruitmentAccess access) : IAccessScope<Candidate>
{
    public Expression<Func<Candidate, bool>> PredicateFor(ICurrentUser user) =>
        access.Rows<Candidate>(ids => candidate => ids.Contains(candidate.Id), access.CandidateIds);
}

internal sealed class JobApplicationScope(RecruitmentAccess access) : IAccessScope<JobApplication>
{
    public Expression<Func<JobApplication, bool>> PredicateFor(ICurrentUser user) =>
        access.Rows<JobApplication>(ids => application => ids.Contains(application.JobOpeningId), access.OpeningIds);
}

internal sealed class InterviewScope(RecruitmentAccess access) : IAccessScope<Interview>
{
    public Expression<Func<Interview, bool>> PredicateFor(ICurrentUser user) =>
        access.Rows<Interview>(ids => interview => ids.Contains(interview.ApplicationId), access.ApplicationIds);
}

internal static class RecruitmentScopeExtensions
{
    public static IServiceCollection AddRecruitmentScopes(this IServiceCollection services)
    {
        services.TryAddScoped<CallerRecruitment>();
        services.TryAddScoped<ICallerRecruitment>(provider => provider.GetRequiredService<CallerRecruitment>());
        services.TryAddScoped<RecruitmentAccess>();
        return services
            .AddScoped<IAccessScope<JobOpening>, JobOpeningScope>()
            .AddScoped<IAccessScope<Candidate>, CandidateScope>()
            .AddScoped<IAccessScope<JobApplication>, JobApplicationScope>()
            .AddScoped<IAccessScope<Interview>, InterviewScope>();
    }
}
