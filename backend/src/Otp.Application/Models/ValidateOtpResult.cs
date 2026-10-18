namespace Otp.Application.Models;

/// <summary>
/// The outcome of the validate use case. The factories guarantee that the optional fields are
/// set consistently with the status.
/// </summary>
public sealed record ValidateOtpResult
{
    private ValidateOtpResult(OtpValidationStatus status, int? remainingAttempts = null, int? retryAfterSeconds = null)
    {
        Status = status;
        RemainingAttempts = remainingAttempts;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>The outcome.</summary>
    public OtpValidationStatus Status { get; }

    /// <summary>The wrong guesses left before a lockout. Set only for <see cref="OtpValidationStatus.Invalid"/>.</summary>
    public int? RemainingAttempts { get; }

    /// <summary>The whole seconds until the lockout ends. Set only for <see cref="OtpValidationStatus.AttemptsExceeded"/>.</summary>
    public int? RetryAfterSeconds { get; }

    /// <summary>Whether the code was accepted.</summary>
    public bool IsValid => Status == OtpValidationStatus.Valid;

    /// <summary>Creates a result for a correct code.</summary>
    /// <returns>A valid result.</returns>
    public static ValidateOtpResult Valid() => new(OtpValidationStatus.Valid);

    /// <summary>Creates a result for a wrong code.</summary>
    /// <param name="remainingAttempts">The wrong guesses left before a lockout.</param>
    /// <returns>An invalid result.</returns>
    public static ValidateOtpResult Invalid(int remainingAttempts) =>
        new(OtpValidationStatus.Invalid, remainingAttempts: remainingAttempts);

    /// <summary>Creates a result for an expired password.</summary>
    /// <returns>An expired result.</returns>
    public static ValidateOtpResult Expired() => new(OtpValidationStatus.Expired);

    /// <summary>Creates a result for a user without an active password.</summary>
    /// <returns>A not-found result.</returns>
    public static ValidateOtpResult NotFound() => new(OtpValidationStatus.NotFound);

    /// <summary>Creates a result for a locked-out user.</summary>
    /// <param name="retryAfterSeconds">The whole seconds until the user may try again.</param>
    /// <returns>An attempts-exceeded result.</returns>
    public static ValidateOtpResult AttemptsExceeded(int retryAfterSeconds) =>
        new(OtpValidationStatus.AttemptsExceeded, retryAfterSeconds: retryAfterSeconds);
}
