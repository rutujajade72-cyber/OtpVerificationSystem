using Otp.Application.Models;

namespace Otp.Application.Tests.Models;

public sealed class GenerateOtpResultTests
{
    private static GeneratedOtp CreateOtp() => new(
        "john.doe",
        "123456",
        new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 17, 10, 0, 30, TimeSpan.Zero),
        30,
        30);

    [Fact]
    public void Success_CarriesTheGeneratedPassword()
    {
        var otp = CreateOtp();

        var result = GenerateOtpResult.Success(otp);

        result.IsSuccess.Should().BeTrue();
        result.Otp.Should().BeSameAs(otp);
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public void Success_NullPassword_Throws()
    {
        var act = () => GenerateOtpResult.Success(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LockedOut_CarriesTheWaitAndNoPassword()
    {
        var result = GenerateOtpResult.LockedOut(42);

        result.IsSuccess.Should().BeFalse();
        result.Otp.Should().BeNull();
        result.RetryAfterSeconds.Should().Be(42);
    }
}
