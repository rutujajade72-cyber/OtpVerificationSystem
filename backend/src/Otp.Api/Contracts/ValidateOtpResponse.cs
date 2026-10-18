namespace Otp.Api.Contracts;

/// <summary>
/// The result of <c>POST /api/otp/validate</c>. Business outcomes such as a wrong or expired
/// code are reported here with HTTP 200, because they are not transport errors.
/// </summary>
/// <param name="IsValid">Whether the code was accepted.</param>
/// <param name="Status">One of Valid, Invalid, Expired, NotFound or AttemptsExceeded.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
/// <param name="RemainingAttempts">The wrong guesses left. Set only when the status is Invalid.</param>
/// <param name="RetryAfterSeconds">The seconds until the lockout ends. Set only when the status is AttemptsExceeded.</param>
public sealed record ValidateOtpResponse(
    bool IsValid,
    string Status,
    string Message,
    int? RemainingAttempts,
    int? RetryAfterSeconds);
