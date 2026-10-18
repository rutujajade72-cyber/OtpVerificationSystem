namespace Otp.Application.Abstractions;

/// <summary>Port: turns a user id and an instant into a one-time code.</summary>
public interface IOtpGenerator
{
    /// <summary>Generates a code. The same inputs always produce the same code.</summary>
    /// <param name="userId">The user the code is for.</param>
    /// <param name="dateTime">The instant the code is derived from.</param>
    /// <returns>A numeric code. A different user or a different instant produces a different code.</returns>
    string GenerateCode(string userId, DateTimeOffset dateTime);
}
