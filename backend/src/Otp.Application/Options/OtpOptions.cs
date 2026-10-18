using System.ComponentModel.DataAnnotations;

namespace Otp.Application.Options;

/// <summary>
/// Settings of the one-time password feature. They are bound from the <c>Otp</c> configuration
/// section and validated when the application starts, so a bad value stops the app immediately
/// instead of failing on the first request.
/// </summary>
public sealed class OtpOptions
{
    /// <summary>The name of the configuration section.</summary>
    public const string SectionName = "Otp";

    /// <summary>
    /// The server-only HMAC key. It is empty in appsettings.json on purpose: every
    /// non-development environment must supply its own value, for example through the
    /// <c>Otp__SecretKey</c> environment variable.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>How long a password stays valid, in seconds.</summary>
    [Range(5, 300)]
    public int ValiditySeconds { get; set; } = 30;

    /// <summary>
    /// The number of digits in a code. The truncated HMAC value is a 31-bit integer
    /// (at most 2,147,483,647), so nine digits is the most it can fill uniformly.
    /// </summary>
    [Range(4, 9)]
    public int CodeLength { get; set; } = 6;

    /// <summary>The wrong codes inside the lockout window that trigger a lockout.</summary>
    [Range(1, 10)]
    public int MaxFailedAttempts { get; set; } = 3;

    /// <summary>The length of the sliding failure window, which is also the lockout length, in seconds.</summary>
    [Range(10, 86400)]
    public int LockoutSeconds { get; set; } = 60;

    /// <summary>How often the background job purges expired data, in seconds.</summary>
    [Range(5, 3600)]
    public int CleanupIntervalSeconds { get; set; } = 60;
}
