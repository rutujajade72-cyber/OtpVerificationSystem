namespace Otp.Application.Models;

/// <summary>Input of the validate use case.</summary>
/// <param name="UserId">The user the code belongs to.</param>
/// <param name="Code">The code to check. Surrounding whitespace is ignored.</param>
public sealed record ValidateOtpCommand(string UserId, string Code);
