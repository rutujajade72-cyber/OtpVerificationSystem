using System.ComponentModel.DataAnnotations;

namespace Otp.Api.RateLimiting;

/// <summary>Settings of the per-client rate limit, bound from the <c>RateLimiting</c> section.</summary>
public sealed class RateLimitingOptions
{
    /// <summary>The name of the configuration section.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>The requests one client may send in one window. Covers both endpoints together.</summary>
    [Range(1, 100000)]
    public int PermitLimit { get; set; } = 30;

    /// <summary>The length of the window, in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}
