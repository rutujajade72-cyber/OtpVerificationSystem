namespace Otp.Api.Contracts;

/// <summary>The successful result of <c>POST /api/otp/generate</c>.</summary>
/// <param name="UserId">The user the password was issued to.</param>
/// <param name="Code">The one-time password.</param>
/// <param name="IssuedAt">The server time the password was issued.</param>
/// <param name="ExpiresAt">The server time the password stops being valid.</param>
/// <param name="ValiditySeconds">The total validity in seconds.</param>
/// <param name="RemainingSeconds">The whole seconds left when the response was created, rounded up.</param>
public sealed record GenerateOtpResponse(
    string UserId,
    string Code,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    int ValiditySeconds,
    int RemainingSeconds);
