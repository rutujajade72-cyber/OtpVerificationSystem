using Otp.Application.Models;

namespace Otp.Application.Tests.Models;

public sealed class ValidateOtpResultTests
{
    [Fact]
    public void Valid_IsTheOnlyResultThatIsValid()
    {
        var result = ValidateOtpResult.Valid();

        result.Status.Should().Be(OtpValidationStatus.Valid);
        result.IsValid.Should().BeTrue();
        result.RemainingAttempts.Should().BeNull();
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void Invalid_CarriesTheRemainingAttempts()
    {
        var result = ValidateOtpResult.Invalid(2);

        result.Status.Should().Be(OtpValidationStatus.Invalid);
        result.IsValid.Should().BeFalse();
        result.RemainingAttempts.Should().Be(2);
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void Expired_HasNoOptionalFields()
    {
        var result = ValidateOtpResult.Expired();

        result.Status.Should().Be(OtpValidationStatus.Expired);
        result.IsValid.Should().BeFalse();
        result.RemainingAttempts.Should().BeNull();
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void NotFound_HasNoOptionalFields()
    {
        var result = ValidateOtpResult.NotFound();

        result.Status.Should().Be(OtpValidationStatus.NotFound);
        result.IsValid.Should().BeFalse();
        result.RemainingAttempts.Should().BeNull();
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void AttemptsExceeded_CarriesTheWait()
    {
        var result = ValidateOtpResult.AttemptsExceeded(60);

        result.Status.Should().Be(OtpValidationStatus.AttemptsExceeded);
        result.IsValid.Should().BeFalse();
        result.RetryAfterSeconds.Should().Be(60);
        result.RemainingAttempts.Should().BeNull();
    }

    [Fact]
    public void Results_WithTheSameContent_AreEqual()
    {
        ValidateOtpResult.Invalid(1).Should().Be(ValidateOtpResult.Invalid(1));
        ValidateOtpResult.Invalid(1).Should().NotBe(ValidateOtpResult.Invalid(2));
    }
}
