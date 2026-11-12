using System.Collections.Concurrent;

namespace Apex.Hrms.Api.Auth;

/// <summary>
/// Allows one administrator-triggered password reset mail per target user per minute, so an administrator (or a stolen
/// administrator session) cannot flood a mailbox. Per target user (one a minute), plus a cap per calling administrator (20 in 10 minutes) against flooding many mailboxes; in memory: each API
/// instance counts for itself. It runs after authorization (an endpoint filter), so callers who are refused cannot use
/// up the allowance of a user.
/// </summary>
internal sealed class PasswordResetThrottle(TimeProvider time)
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>At most this many reset mails per caller in <see cref="CallerWindow"/>, to the same or different users.</summary>
    public const int CallerLimit = 20;

    public static readonly TimeSpan CallerWindow = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<int, DateTimeOffset> lastSent = new();
    private readonly ConcurrentDictionary<int, Queue<DateTimeOffset>> byCaller = new();

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

    /// <summary>Gives the allowance back after a request that sent no mail (a refusal or a failure before sending). Never after a mail was sent.</summary>
    public void Release(int accountId) => lastSent.TryRemove(accountId, out _);

    /// <summary>
    /// Takes one of the <see cref="CallerLimit"/> sends the caller may start in <see cref="CallerWindow"/> (all users together),
    /// or returns false with the time until the oldest one expires.
    /// </summary>
    public bool TryAcquireForCaller(int callerId, out TimeSpan retryAfter)
    {
        var now = time.GetUtcNow();
        var sends = byCaller.GetOrAdd(callerId, _ => new Queue<DateTimeOffset>());
        lock (sends)
        {
            while (sends.Count > 0 && now - sends.Peek() >= CallerWindow)
            {
                sends.Dequeue();
            }

            if (sends.Count >= CallerLimit)
            {
                retryAfter = CallerWindow - (now - sends.Peek());
                return false;
            }

            sends.Enqueue(now);
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    /// <summary>Gives back the caller's latest send after a request that sent no mail.</summary>
    public void ReleaseForCaller(int callerId)
    {
        if (!byCaller.TryGetValue(callerId, out var sends))
        {
            return;
        }

        lock (sends)
        {
            if (sends.Count > 0)
            {
                var kept = sends.ToArray()[..^1];
                sends.Clear();
                foreach (var sent in kept)
                {
                    sends.Enqueue(sent);
                }
            }
        }
    }

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
