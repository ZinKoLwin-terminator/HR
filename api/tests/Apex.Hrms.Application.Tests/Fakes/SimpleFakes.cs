using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

internal sealed class FakeClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;

    public DateTimeOffset UtcNow => new(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
}

internal sealed class FakeUnitOfWork(FakeDatabase database) : IUnitOfWork
{
    public int SaveCount { get; private set; }

    /// <summary>When set, the next save fails with this error (e.g. a concurrency conflict).</summary>
    public AppError? FailNextSaveWith { get; set; }

    public Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        var failure = FailNextSaveWith;
        FailNextSaveWith = null;
        if (failure is null)
        {
            database.Commit();
        }

        return Task.FromResult(failure is null ? Result.Success() : Result.Failure(failure));
    }
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public int? UserId { get; set; }

    public int? EmployeeId { get; set; }

    public bool IsAuthenticated { get; set; } = true;

    public bool IsSystem { get; set; }

    public string DisplayName { get; set; } = "Test User";
}

/// <summary>Placements of employees as the organization reference checks see them (the hr.EmployeeOrgLink view).</summary>
internal sealed class FakeEmployees : IRecordCounter<EmployeeOrgLink>
{
    private readonly List<EmployeeOrgLink> employees = [];

    public void Add(int companyId = 1, int branchId = 1, int departmentId = 1, int? sectionId = null, int positionId = 1, int jobGradeId = 1) =>
        employees.Add(new EmployeeOrgLink(employees.Count + 1, companyId, branchId, departmentId, sectionId, positionId, jobGradeId));

    public Task<int> CountAsync(Expression<Func<EmployeeOrgLink, bool>> predicate, CancellationToken cancellationToken) =>
        Task.FromResult(employees.Count(predicate.Compile()));
}

/// <summary>A sequence that counts up from 1, like hr.EmployeeCodeSeq.</summary>
internal sealed class FakeEmployeeCodeSequence : IEmployeeCodeSequence
{
    private int last;

    public Task<int> NextAsync(CancellationToken cancellationToken) => Task.FromResult(++last);
}
