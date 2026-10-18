using Otp.Domain.Entities;

namespace Otp.Application.Abstractions;

/// <summary>Port: stores the active one-time password of each user. There is at most one per user.</summary>
public interface IOtpRepository
{
    /// <summary>Stores the password, replacing any earlier password of the same user.</summary>
    /// <param name="otp">The password to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the password is stored.</returns>
    Task SaveAsync(OneTimePassword otp, CancellationToken cancellationToken = default);

    /// <summary>Finds the active password of a user.</summary>
    /// <param name="userId">The user to look up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The stored password, or <see langword="null"/> when the user has none.</returns>
    Task<OneTimePassword?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the password only if the stored instance is still the given one (compare and remove).
    /// This makes a password single-use even when two requests race.
    /// </summary>
    /// <param name="otp">The exact instance that was read earlier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="true"/> when this call removed the password; <see langword="false"/> when it was already gone or replaced.</returns>
    Task<bool> TryRemoveAsync(OneTimePassword otp, CancellationToken cancellationToken = default);

    /// <summary>Removes every password that is expired at <paramref name="now"/>.</summary>
    /// <param name="now">The instant to evaluate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of passwords removed.</returns>
    Task<int> RemoveExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
