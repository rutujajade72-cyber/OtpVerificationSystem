using Otp.Domain.Entities;

namespace Otp.Application.Abstractions;

/// <summary>Port: stores the wrong-code history of each user.</summary>
public interface IFailedAttemptRepository
{
    /// <summary>Finds the history of a user.</summary>
    /// <param name="userId">The user to look up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The history, or <see langword="null"/> when the user has never failed.</returns>
    Task<FailedAttemptHistory?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Finds the history of a user, creating an empty one if needed. Concurrent callers share one instance.</summary>
    /// <param name="userId">The user to look up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user's history.</returns>
    Task<FailedAttemptHistory> GetOrAddAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Forgets the history of a user, for example after a successful validation.</summary>
    /// <param name="userId">The user whose history is removed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the history is removed.</returns>
    Task RemoveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Removes histories that have no failure after <paramref name="since"/>.</summary>
    /// <param name="since">Histories whose newest failure is at or before this instant are removed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of histories removed.</returns>
    Task<int> RemoveInactiveAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
}
