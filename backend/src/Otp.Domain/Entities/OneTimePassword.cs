using System.Security.Cryptography;
using System.Text;
using Otp.Domain.Constants;

namespace Otp.Domain.Entities;

/// <summary>
/// An immutable one-time password issued to one user. It knows when it was issued, when it
/// expires and whether a candidate code matches it. The current time is always passed in, so
/// the entity is deterministic and trivially testable.
/// </summary>
public sealed class OneTimePassword
{
    /// <summary>Creates a one-time password.</summary>
    /// <param name="userId">The user the password was issued to. Not blank, at most <see cref="OtpConstraints.MaxUserIdLength"/> characters.</param>
    /// <param name="code">The secret code. Not blank.</param>
    /// <param name="issuedAt">The instant the password was issued.</param>
    /// <param name="expiresAt">The instant the password stops being valid. Must be after <paramref name="issuedAt"/>.</param>
    public OneTimePassword(string userId, string code, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (userId.Length > OtpConstraints.MaxUserIdLength)
        {
            throw new ArgumentException(
                $"The user id must not be longer than {OtpConstraints.MaxUserIdLength} characters.",
                nameof(userId));
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException("The expiry must be after the issue time.", nameof(expiresAt));
        }

        UserId = userId;
        Code = code;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>The user the password was issued to.</summary>
    public string UserId { get; }

    /// <summary>The secret code. Never log this value.</summary>
    public string Code { get; }

    /// <summary>The instant the password was issued.</summary>
    public DateTimeOffset IssuedAt { get; }

    /// <summary>The instant the password stops being valid.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Whether the password is expired at <paramref name="now"/>. The boundary is inclusive:
    /// at exactly the expiry instant the password is already expired.
    /// </summary>
    /// <param name="now">The instant to evaluate.</param>
    /// <returns><see langword="true"/> when <paramref name="now"/> is at or after the expiry.</returns>
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>The time left before expiry, clamped to zero once the password has expired.</summary>
    /// <param name="now">The instant to evaluate.</param>
    /// <returns>The remaining time, never negative.</returns>
    public TimeSpan GetRemainingTime(DateTimeOffset now)
    {
        var remaining = ExpiresAt - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Compares a candidate with the stored code in constant time, so the comparison time does
    /// not reveal how many leading digits were correct.
    /// </summary>
    /// <param name="candidate">The code to check. Null or empty never matches.</param>
    /// <returns><see langword="true"/> when the candidate equals the stored code.</returns>
    public bool Matches(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Code);
        var actual = Encoding.UTF8.GetBytes(candidate);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
