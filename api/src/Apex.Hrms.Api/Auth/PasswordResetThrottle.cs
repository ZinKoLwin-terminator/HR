using System.Collections.Concurrent;

namespace Apex.Hrms.Api.Auth;

/// <summary>
/// Allows one administrator-triggered password reset mail per target user per minute, so an administrator (or a stolen
/// administrator session) cannot flood a mailbox. Per target user, not per caller or client IP, and in memory: each API
/// instance counts for itself. It runs after authorization (an endpoint filter), so callers who are refused cannot use
/// up the allowance of a user.
/// </summary>
internal sealed class PasswordResetThrottle(TimeProvider time)
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<int, DateTimeOffset> lastSent = new();

    /// <summary>Takes the allowance for the user, or returns false with the time left to wait.</summary>
    public bool TryAcquire(int accountId, out TimeSpan retryAfter)
    {
        var now = time.GetUtcNow();
        Purge(now);

        var acquired = true;
        var wait = TimeSpan.Zero;
        lastSent.AddOrUpdate(
            accountId,
            now,
            (_, previous) =>
            {
                if (now - previous < Window)
                {
                    acquired = false;
                    wait = Window - (now - previous);
                    return previous;
                }

                acquired = true;
                return now;
            });

        retryAfter = wait;
        return acquired;
    }

    /// <summary>Gives the allowance back after a request that sent nothing (a refusal or a failure).</summary>
    public void Release(int accountId) => lastSent.TryRemove(accountId, out _);

    private void Purge(DateTimeOffset now)
    {
        if (lastSent.Count < 256)
        {
            return;
        }

        foreach (var pair in lastSent.Where(pair => now - pair.Value >= Window))
        {
            lastSent.TryRemove(pair);
        }
    }
}
