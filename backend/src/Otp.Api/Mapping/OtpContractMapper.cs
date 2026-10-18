using Otp.Api.Contracts;
using Otp.Application.Models;

namespace Otp.Api.Mapping;

/// <summary>
/// Converts between HTTP contracts and application models, so the HTTP shape can change without
/// touching the use cases. It also owns every user-facing message.
/// </summary>
public static class OtpContractMapper
{
    /// <summary>Converts a generate request to a command.</summary>
    /// <param name="request">The validated request.</param>
    /// <returns>The command for the application layer.</returns>
    public static GenerateOtpCommand ToCommand(this GenerateOtpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dateTime = request.DateTime
            ?? throw new ArgumentException("The date and time are required.", nameof(request));

        return new GenerateOtpCommand(request.UserId, dateTime);
    }

    /// <summary>Converts a validate request to a command.</summary>
    /// <param name="request">The validated request.</param>
    /// <returns>The command for the application layer.</returns>
    public static ValidateOtpCommand ToCommand(this ValidateOtpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValidateOtpCommand(request.UserId, request.Code);
    }

    /// <summary>Converts a generated password to its response.</summary>
    /// <param name="otp">The generated password.</param>
    /// <returns>The response body.</returns>
    public static GenerateOtpResponse ToResponse(this GeneratedOtp otp)
    {
        ArgumentNullException.ThrowIfNull(otp);

        return new GenerateOtpResponse(
            otp.UserId,
            otp.Code,
            otp.IssuedAt,
            otp.ExpiresAt,
            otp.ValiditySeconds,
            otp.RemainingSeconds);
    }

    /// <summary>Converts a validation result to its response.</summary>
    /// <param name="result">The validation result.</param>
    /// <returns>The response body, including the user-facing message.</returns>
    public static ValidateOtpResponse ToResponse(this ValidateOtpResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ValidateOtpResponse(
            result.IsValid,
            result.Status.ToString(),
            DescribeStatus(result),
            result.RemainingAttempts,
            result.RetryAfterSeconds);
    }

    /// <summary>The message shown when a user is locked out.</summary>
    /// <param name="retryAfterSeconds">The whole seconds until the lockout ends.</param>
    /// <returns>The lockout message.</returns>
    public static string DescribeLockout(int retryAfterSeconds) =>
        $"Too many incorrect attempts. Please try again in {retryAfterSeconds} second(s).";

    private static string DescribeStatus(ValidateOtpResult result) => result.Status switch
    {
        OtpValidationStatus.Valid => "The one-time password is valid.",
        OtpValidationStatus.Invalid =>
            $"The one-time password is incorrect. {result.RemainingAttempts ?? 0} attempt(s) remaining.",
        OtpValidationStatus.Expired => "The one-time password has expired. Please generate a new one.",
        OtpValidationStatus.NotFound =>
            "No active one-time password was found for this user. It may already have been used.",
        OtpValidationStatus.AttemptsExceeded => DescribeLockout(result.RetryAfterSeconds ?? 0),

        // A new enum member without a message must fail loudly instead of returning an empty string.
        _ => throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown validation status."),
    };
}
