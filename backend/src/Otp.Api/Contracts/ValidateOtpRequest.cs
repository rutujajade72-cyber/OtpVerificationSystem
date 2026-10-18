using System.ComponentModel.DataAnnotations;
using Otp.Domain.Constants;

namespace Otp.Api.Contracts;

/// <summary>The body of <c>POST /api/otp/validate</c>.</summary>
public sealed class ValidateOtpRequest
{
    /// <summary>The user the code belongs to. Required, at most 30 characters.</summary>
    /// <example>john.doe</example>
    [Required]
    [StringLength(OtpConstraints.MaxUserIdLength, MinimumLength = 1)]
    public string UserId { get; init; } = string.Empty;

    /// <summary>The code to check: 4 to 9 digits. Surrounding whitespace is ignored.</summary>
    /// <example>482913</example>
    [Required]
    [RegularExpression(@"^\s*\d{4,9}\s*$", ErrorMessage = "The code must be 4 to 9 digits.")]
    public string Code { get; init; } = string.Empty;
}
