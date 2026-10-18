using System.Collections.Concurrent;
using Otp.Application.Abstractions;
using Otp.Domain.Entities;

namespace Otp.Infrastructure.Repositories;

/// <summary>
/// Keeps the active password of each user in process memory. The store must outlive individual
/// requests, so it is registered as a singleton. Every operation is thread-safe.
/// </summary>
public sealed class InMemoryOtpRepository : IOtpRepository
{
    private readonly ConcurrentDictionary<string, OneTimePassword> _store = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task SaveAsync(OneTimePassword otp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(otp);

        // Indexer assignment replaces the user's previous password.
        _store[otp.UserId] = otp;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<OneTimePassword?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _store.TryGetValue(userId, out var otp);
        return Task.FromResult(otp);
    }

    /// <inheritdoc />
    public Task<bool> TryRemoveAsync(OneTimePassword otp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(otp);

        // An atomic compare-and-remove: the entry is removed only if the stored value is this very
        // instance. OneTimePassword does not override Equals, so equality is reference equality and
        // a newer password for the same user is never removed by mistake.
        var removed = _store.TryRemove(new KeyValuePair<string, OneTimePassword>(otp.UserId, otp));
        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<int> RemoveExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var removed = 0;

        // Enumerating a ConcurrentDictionary is safe during concurrent writes. Removing by key AND
        // value means a password generated mid-sweep is not discarded together with the expired one
        // it replaced.
        foreach (var entry in _store)
        {
            if (entry.Value.IsExpired(now) && _store.TryRemove(entry))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }
}
