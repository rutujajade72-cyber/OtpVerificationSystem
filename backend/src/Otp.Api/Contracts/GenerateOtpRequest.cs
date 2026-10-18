using System.ComponentModel.DataAnnotations;
using Otp.Domain.Constants;

namespace Otp.Api.Contracts;

/// <summary>The body of <c>POST /api/otp/generate</c>.</summary>
public sealed class GenerateOtpRequest
{
    /// <summary>The user the password is generated for. Required, at most 30 characters.</summary>
    /// <example>john.doe</example>
    [Required]
    [StringLength(OtpConstraints.MaxUserIdLength, MinimumLength = 1)]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// The date and time the code is derived from, as an ISO 8601 string. The value is nullable so
    /// that a missing value is reported as a validation error instead of silently becoming 0001-01-01.
    /// </summary>
    /// <example>2026-10-16T12:30:00.000Z</example>
    [Required]
    public DateTimeOffset? DateTime { get; init; }
}
