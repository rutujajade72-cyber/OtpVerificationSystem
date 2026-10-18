using System.Diagnostics.CodeAnalysis;

namespace Otp.Application.Models;

/// <summary>
/// The outcome of the generate use case: either a password or a lockout. The factories
/// guarantee that exactly one of the two is set.
/// </summary>
public sealed record GenerateOtpResult
{
    private GenerateOtpResult(GeneratedOtp? otp, int? retryAfterSeconds)
    {
        Otp = otp;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>The generated password. Not null when <see cref="IsSuccess"/> is true.</summary>
    public GeneratedOtp? Otp { get; }

    /// <summary>The whole seconds until the lockout ends. Set only when the user is locked out.</summary>
    public int? RetryAfterSeconds { get; }

    /// <summary>Whether a password was generated.</summary>
    [MemberNotNullWhen(true, nameof(Otp))]
    public bool IsSuccess => Otp is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="otp">The generated password.</param>
    /// <returns>A result that carries the password.</returns>
    public static GenerateOtpResult Success(GeneratedOtp otp)
    {
        ArgumentNullException.ThrowIfNull(otp);
        return new GenerateOtpResult(otp, null);
    }

    /// <summary>Creates a lockout result.</summary>
    /// <param name="retryAfterSeconds">The whole seconds until the user may try again.</param>
    /// <returns>A result that carries the wait time.</returns>
    public static GenerateOtpResult LockedOut(int retryAfterSeconds) => new(null, retryAfterSeconds);
}
