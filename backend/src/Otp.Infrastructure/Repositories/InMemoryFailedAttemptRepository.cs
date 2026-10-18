using System.Collections.Concurrent;
using Otp.Application.Abstractions;
using Otp.Domain.Entities;

namespace Otp.Infrastructure.Repositories;

/// <summary>
/// Keeps the wrong-code history of each user in process memory. Registered as a singleton.
/// </summary>
public sealed class InMemoryFailedAttemptRepository : IFailedAttemptRepository
{
    private readonly ConcurrentDictionary<string, FailedAttemptHistory> _store = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<FailedAttemptHistory?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _store.TryGetValue(userId, out var history);
        return Task.FromResult(history);
    }

    /// <inheritdoc />
    public Task<FailedAttemptHistory> GetOrAddAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        // GetOrAdd hands one winning instance to every concurrent caller, so no failure is recorded
        // on a history that is then thrown away. The lambda is static: no closure allocation.
        var history = _store.GetOrAdd(userId, static id => new FailedAttemptHistory(id));
        return Task.FromResult(history);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _store.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> RemoveInactiveAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var removed = 0;

        // A failure recorded between the check and the removal could be lost. At worst that grants
        // one extra guess per cleanup interval, which is accepted instead of locking the whole store.
        foreach (var entry in _store)
        {
            if (!entry.Value.HasFailuresSince(since) && _store.TryRemove(entry))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }
}
