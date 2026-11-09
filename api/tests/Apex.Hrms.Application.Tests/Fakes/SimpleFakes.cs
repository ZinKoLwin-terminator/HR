using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Common;

namespace Apex.Hrms.Application.Tests.Fakes;

internal sealed class FakeClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;

    public DateTimeOffset UtcNow => new(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    /// <summary>When set, the next save fails with this error (e.g. a concurrency conflict).</summary>
    public AppError? FailNextSaveWith { get; set; }

    public Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        var failure = FailNextSaveWith;
        FailNextSaveWith = null;
        return Task.FromResult(failure is null ? Result.Success() : Result.Failure(failure));
    }
}

internal sealed class FakeReferenceUsageReader : IReferenceUsageReader
{
    private readonly Dictionary<(ReferenceQuery Query, int OwnerId), int> counts = [];

    public void Set(ReferenceQuery query, int ownerId, int count) => counts[(query, ownerId)] = count;

    public Task<int> CountAsync(ReferenceQuery query, int ownerId, CancellationToken cancellationToken) =>
        Task.FromResult(counts.GetValueOrDefault((query, ownerId)));
}
