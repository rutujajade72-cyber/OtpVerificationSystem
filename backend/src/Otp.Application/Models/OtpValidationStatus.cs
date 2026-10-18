namespace Otp.Application.Models;

/// <summary>The possible outcomes of validating a code.</summary>
public enum OtpValidationStatus
{
    /// <summary>The code is correct and was consumed.</summary>
    Valid,

    /// <summary>The code is wrong. The user still has attempts left.</summary>
    Invalid,

    /// <summary>The password expired before it was used.</summary>
    Expired,

    /// <summary>The user has no active password, or it was already used.</summary>
    NotFound,

    /// <summary>The user made too many wrong attempts and is locked out for a while.</summary>
    AttemptsExceeded,
}
