using Otp.Application.Models;

namespace Otp.Application.Abstractions;

/// <summary>The use cases of the one-time password feature. The API depends only on this interface.</summary>
public interface IOtpService
{
    /// <summary>Generates a one-time password that is valid for the configured number of seconds.</summary>
    /// <param name="command">The user id and the instant the code is derived from.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The generated password, or a lockout result when the user is locked out.</returns>
    Task<GenerateOtpResult> GenerateAsync(GenerateOtpCommand command, CancellationToken cancellationToken = default);

    /// <summary>Validates a code. A valid code is consumed and cannot be used again.</summary>
    /// <param name="command">The user id and the code to check.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The outcome. Expected failures such as a wrong code are values, not exceptions.</returns>
    Task<ValidateOtpResult> ValidateAsync(ValidateOtpCommand command, CancellationToken cancellationToken = default);
}
