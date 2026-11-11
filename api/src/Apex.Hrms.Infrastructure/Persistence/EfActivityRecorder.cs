using Apex.Hrms.Application.Abstractions;
using Apex.Hrms.Domain.Activity;

namespace Apex.Hrms.Infrastructure.Persistence;

/// <summary>
/// Adds activity rows to the same <see cref="HrmsDbContext"/> as the change they describe, so the one save of
/// <see cref="EfUnitOfWork"/> writes both in one transaction. A message that needs a database-assigned id is kept as a
/// delegate and written by the unit of work once the change has been inserted (still inside the transaction).
/// The actor is the signed-in user; the system identity writes with no user id and the name "System".
/// </summary>
internal sealed class EfActivityRecorder(HrmsDbContext context, IClock clock, ICurrentUser user) : IActivityRecorder, IActivityMute
{
    private readonly List<(string Category, Func<string> Message)> deferred = [];

    public bool HasDeferred => deferred.Count > 0;

    private int muted;

    public IDisposable Mute()
    {
        muted++;
        return new Unmute(this);
    }

    public void Record(string category, string message)
    {
        if (muted == 0)
        {
            context.Add(Build(category, message));
        }
    }

    public void Record(string category, Func<string> message)
    {
        if (muted == 0)
        {
            deferred.Add((category, message));
        }
    }

    /// <summary>Adds the deferred entries to the context (the caller saves). Call after the change was inserted.</summary>
    public void WriteDeferred()
    {
        foreach (var (category, message) in deferred)
        {
            context.Add(Build(category, message()));
        }

        deferred.Clear();
    }

    private ActivityLogEntry Build(string category, string message)
    {
        var entry = ActivityLogEntry.Record(
            category,
            message,
            user.IsSystem ? null : user.UserId,
            user.IsSystem ? "System" : user.DisplayName,
            clock.UtcNow.UtcDateTime);
        return entry.IsSuccess
            ? entry.Value
            : throw new InvalidOperationException($"Invalid activity entry: {entry.Error.Message}");
    }

    private sealed class Unmute(EfActivityRecorder owner) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                owner.muted--;
            }
        }
    }
}
