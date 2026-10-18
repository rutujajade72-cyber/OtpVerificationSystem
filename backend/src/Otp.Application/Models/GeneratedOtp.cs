namespace Otp.Application.Models;

/// <summary>A successfully generated one-time password.</summary>
/// <param name="UserId">The user the password was issued to.</param>
/// <param name="Code">The one-time code.</param>
/// <param name="IssuedAt">The server time the password was issued.</param>
/// <param name="ExpiresAt">The server time the password stops being valid.</param>
/// <param name="ValiditySeconds">The total validity of the password in seconds.</param>
/// <param name="RemainingSeconds">The whole seconds left at the moment of issue, rounded up.</param>
public sealed record GeneratedOtp(
    string UserId,
    string Code,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    int ValiditySeconds,
    int RemainingSeconds);
