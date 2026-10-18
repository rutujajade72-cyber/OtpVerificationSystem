namespace Otp.Application.Models;

/// <summary>Input of the generate use case.</summary>
/// <param name="UserId">The user the password is for.</param>
/// <param name="DateTime">The instant the code is derived from. It does not change how long the password is valid.</param>
public sealed record GenerateOtpCommand(string UserId, DateTimeOffset DateTime);
