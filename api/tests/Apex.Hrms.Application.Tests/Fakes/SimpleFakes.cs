using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Organization;
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
}

/// <summary>Employees of the fake database; counted by the reference checks like the not-yet-ported Employee table.</summary>
internal sealed class FakeEmployees : IRecordCounter<EmployeeReference>
{
    private readonly List<EmployeeReference> employees = [];

    public void Add(int companyId = 1, int branchId = 1, int departmentId = 1, int? sectionId = null, int positionId = 1, int jobGradeId = 1) =>
        employees.Add(new EmployeeReference(employees.Count + 1, companyId, branchId, departmentId, sectionId, positionId, jobGradeId));

    public Task<int> CountAsync(Expression<Func<EmployeeReference, bool>> predicate, CancellationToken cancellationToken) =>
        Task.FromResult(employees.Count(predicate.Compile()));
}
