namespace Otp.Domain.Entities;

/// <summary>
/// The timestamps of one user's recent wrong codes. It implements a sliding-window counter:
/// only failures that happened inside the window count towards a lockout. All members are
/// thread-safe because the same instance is shared by concurrent requests.
/// </summary>
public sealed class FailedAttemptHistory
{
    private readonly object _gate = new();
    private readonly Queue<DateTimeOffset> _failures = new();

    /// <summary>Creates an empty history.</summary>
    /// <param name="userId">The user the history belongs to. Not blank.</param>
    public FailedAttemptHistory(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
    }

    /// <summary>The user the history belongs to.</summary>
    public string UserId { get; }

    /// <summary>
    /// Records a wrong code. Failures at or before <c>now - window</c> are dropped first.
    /// </summary>
    /// <param name="now">The instant of the failure.</param>
    /// <param name="window">The length of the sliding window.</param>
    /// <returns>The number of failures that are still inside the window, including this one.</returns>
    public int RegisterFailure(DateTimeOffset now, TimeSpan window)
    {
        lock (_gate)
        {
            Prune(now - window);
            _failures.Enqueue(now);
            return _failures.Count;
        }
    }

    /// <summary>Works out whether the user is locked out and, if so, until when.</summary>
    /// <param name="now">The instant to evaluate.</param>
    /// <param name="window">The length of the sliding window.</param>
    /// <param name="maxFailures">The number of failures inside the window that triggers a lockout.</param>
    /// <returns>
    /// <see langword="null"/> when fewer than <paramref name="maxFailures"/> failures remain in the
    /// window. Otherwise the instant at which the count drops below the maximum.
    /// </returns>
    public DateTimeOffset? GetLockedUntil(DateTimeOffset now, TimeSpan window, int maxFailures)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFailures, 1);

        lock (_gate)
        {
            Prune(now - window);

            if (_failures.Count < maxFailures)
            {
                return null;
            }

            // Concurrent requests can push the count above the maximum. The lockout then lasts
            // until enough failures have aged out for the count to fall below the maximum, which
            // is the failure at index (count - maxFailures), not necessarily the oldest one.
            return _failures.ElementAt(_failures.Count - maxFailures) + window;
        }
    }

    /// <summary>Discards failures at or before <paramref name="since"/> and reports whether any remain.</summary>
    /// <param name="since">Failures at or before this instant are dropped.</param>
    /// <returns><see langword="true"/> when at least one later failure is still recorded.</returns>
    public bool HasFailuresSince(DateTimeOffset since)
    {
        lock (_gate)
        {
            Prune(since);
            return _failures.Count > 0;
        }
    }

    private void Prune(DateTimeOffset threshold)
    {
        while (_failures.TryPeek(out var oldest) && oldest <= threshold)
        {
            _failures.Dequeue();
        }
    }
}
