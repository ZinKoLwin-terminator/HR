using System.Linq.Expressions;
using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Application.Employees;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

internal sealed class FakeClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;

    /// <summary>The time of day now; tests set it to punch at a chosen time.</summary>
    public TimeOnly TimeNow { get; set; } = new(9, 0);

    public DateTimeOffset UtcNow => new(Today.ToDateTime(TimeNow), TimeSpan.Zero);
}

internal sealed class FakeUnitOfWork(FakeDatabase database, FakeActivityLog? activityLog = null) : IUnitOfWork
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
            activityLog?.Commit();
        }
        else
        {
            activityLog?.Discard();
        }

        return Task.FromResult(failure is null ? Result.Success() : Result.Failure(failure));
    }

    /// <summary>The exclusive lock name of every transaction a handler began (null when it asked for none).</summary>
    public List<string?> TransactionLocks { get; } = [];

    /// <summary>The exclusive lock of every transaction a handler began, with its busy message (null when it asked for none).</summary>
    public List<ApplicationLock?> Locks { get; } = [];

    /// <summary>
    /// Lock names that "another request" holds for longer than the wait: a transaction asking for one fails with a Conflict carrying the
    /// lock's busy message, as the real unit of work does after 15 seconds. Any other lock is granted at once.
    /// </summary>
    public HashSet<string> HeldElsewhere { get; } = [];

    public int Commits { get; private set; }

    /// <summary>
    /// Runs when a handler begins a transaction, before the lock is granted: a test uses it to commit "another request" that
    /// held the lock just before, so it can see which reads the handler makes after it got the lock.
    /// </summary>
    public Action<ApplicationLock?>? OnBeginTransaction { get; set; }

    /// <summary>Transactions that were disposed without a commit.</summary>
    public int RollBacks { get; private set; }

    /// <summary>
    /// When true, a transaction disposed without a commit puts the records and the activity log back as they were when it began,
    /// like a database rollback. Off by default: tests that read the in-memory state after a refused change keep their meaning.
    /// </summary>
    public bool RollBackUncommittedTransactions { get; set; }

    public Task<Result<IUnitOfWorkTransaction>> BeginTransactionAsync(ApplicationLock? exclusiveLock, CancellationToken cancellationToken)
    {
        TransactionLocks.Add(exclusiveLock?.Name);
        Locks.Add(exclusiveLock);
        OnBeginTransaction?.Invoke(exclusiveLock);
        if (exclusiveLock is not null && HeldElsewhere.Contains(exclusiveLock.Name))
        {
            return Task.FromResult(Result.Failure<IUnitOfWorkTransaction>(AppError.Conflict(exclusiveLock.BusyMessage)));
        }

        Action? restore = null;
        if (RollBackUncommittedTransactions)
        {
            var records = database.Snapshot();
            var log = activityLog?.Snapshot();
            restore = () =>
            {
                records();
                log?.Invoke();
            };
        }

        return Task.FromResult(Result.Success<IUnitOfWorkTransaction>(new FakeTransaction(this, restore)));
    }

    private sealed class FakeTransaction(FakeUnitOfWork owner, Action? restore) : IUnitOfWorkTransaction
    {
        private bool committed;

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            committed = true;
            owner.Commits++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!committed)
            {
                owner.RollBacks++;
                restore?.Invoke();
            }

            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    /// <summary>The signed-in account; set to null for an anonymous caller.</summary>
    public int? UserId { get; set; } = 1;

    /// <summary>The employee linked to the account; null is the built-in administrator.</summary>
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

/// <summary>A sequence that counts up from 1, like lve.LeaveApplicationNoSeq.</summary>
internal sealed class FakeLeaveNumberSequence : ILeaveApplicationNumberSequence
{
    private int last;

    public Task<int> NextAsync(CancellationToken cancellationToken) => Task.FromResult(++last);

    /// <summary>Burns numbers, as a rolled-back transaction or another session would.</summary>
    public void Skip(int count) => last += count;

    /// <summary>Starts counting from 1 again, so the next numbers are ones already in use.</summary>
    public void Reset() => last = 0;
}

internal sealed class FakeBusinessWeek : IBusinessWeek
{
    public HashSet<DayOfWeek> Days { get; } = [DayOfWeek.Saturday, DayOfWeek.Sunday];

    public IReadOnlySet<DayOfWeek> WeekendDays => Days;
}

/// <summary>A sequence that counts up from 1, like hr.EmployeeCodeSeq.</summary>
internal sealed class FakeEmployeeCodeSequence : IEmployeeCodeSequence
{
    private int last;

    public Task<int> NextAsync(CancellationToken cancellationToken) => Task.FromResult(++last);
}

/// <summary>A sequence that counts up from 1, like rec.JobOpeningCodeSeq.</summary>
internal sealed class FakeJobCodeSequence : IJobOpeningCodeSequence
{
    private int last;

    public Task<int> NextAsync(CancellationToken cancellationToken) => Task.FromResult(++last);

    /// <summary>Starts counting from 1 again, so the next codes are ones already in use.</summary>
    public void Reset() => last = 0;
}

/// <summary>The caller's grants: everything (an approver who sees every row) until a test calls <see cref="Only"/>.</summary>
internal sealed class FakeCallerPermissions : Apex.Hrms.Application.Security.ICallerPermissions
{
    private HashSet<string>? only;

    public bool Has(string moduleKey, string action) => only is null || only.Contains($"{moduleKey}:{action}");

    /// <summary>Grants only these "module:Action" keys, like an ordinary employee.</summary>
    public void Only(params string[] keys) => only = [.. keys];
}
