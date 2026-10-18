using Otp.Domain.Constants;
using Otp.Domain.Entities;

namespace Otp.Domain.Tests.Entities;

public sealed class OneTimePasswordTests
{
    private static DateTimeOffset IssuedAt => new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);

    private static OneTimePassword CreateOtp(string code = "123456", int validitySeconds = 30) =>
        new("john.doe", code, IssuedAt, IssuedAt.AddSeconds(validitySeconds));

    [Fact]
    public void Constructor_ValidArguments_ExposesAllValues()
    {
        var otp = new OneTimePassword("john.doe", "123456", IssuedAt, IssuedAt.AddSeconds(30));

        otp.UserId.Should().Be("john.doe");
        otp.Code.Should().Be("123456");
        otp.IssuedAt.Should().Be(IssuedAt);
        otp.ExpiresAt.Should().Be(IssuedAt.AddSeconds(30));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Constructor_BlankUserId_Throws(string? userId)
    {
        var act = () => new OneTimePassword(userId!, "123456", IssuedAt, IssuedAt.AddSeconds(30));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_BlankCode_Throws(string? code)
    {
        var act = () => new OneTimePassword("john.doe", code!, IssuedAt, IssuedAt.AddSeconds(30));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_UserIdLongerThanLimit_Throws()
    {
        var userId = new string('a', OtpConstraints.MaxUserIdLength + 1);

        var act = () => new OneTimePassword(userId, "123456", IssuedAt, IssuedAt.AddSeconds(30));

        act.Should().Throw<ArgumentException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_UserIdAtLimit_Succeeds()
    {
        var userId = new string('a', OtpConstraints.MaxUserIdLength);

        var otp = new OneTimePassword(userId, "123456", IssuedAt, IssuedAt.AddSeconds(30));

        otp.UserId.Should().HaveLength(OtpConstraints.MaxUserIdLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3600)]
    public void Constructor_ExpiryNotAfterIssueTime_Throws(int secondsAfterIssue)
    {
        var act = () => new OneTimePassword("john.doe", "123456", IssuedAt, IssuedAt.AddSeconds(secondsAfterIssue));

        act.Should().Throw<ArgumentException>().WithParameterName("expiresAt");
    }

    [Theory]
    [InlineData(-1000, false)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(29_999, false)]
    [InlineData(30_000, true)]
    [InlineData(30_001, true)]
    [InlineData(3_600_000, true)]
    public void IsExpired_RelativeToThirtySecondValidity_UsesInclusiveBoundary(int millisecondsAfterIssue, bool expected)
    {
        var otp = CreateOtp();

        var expired = otp.IsExpired(IssuedAt.AddMilliseconds(millisecondsAfterIssue));

        expired.Should().Be(expected);
    }

    [Fact]
    public void GetRemainingTime_AtIssueTime_ReturnsFullValidity()
    {
        var otp = CreateOtp();

        otp.GetRemainingTime(IssuedAt).Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetRemainingTime_PartWayThrough_ReturnsTimeLeft()
    {
        var otp = CreateOtp();

        otp.GetRemainingTime(IssuedAt.AddSeconds(12)).Should().Be(TimeSpan.FromSeconds(18));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(3600)]
    public void GetRemainingTime_AtOrAfterExpiry_IsClampedToZero(int secondsAfterIssue)
    {
        var otp = CreateOtp();

        otp.GetRemainingTime(IssuedAt.AddSeconds(secondsAfterIssue)).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Matches_CorrectCode_ReturnsTrue()
    {
        CreateOtp("482913").Matches("482913").Should().BeTrue();
    }

    [Theory]
    [InlineData("482914")]
    [InlineData("000000")]
    [InlineData("48291")]
    [InlineData("4829130")]
    [InlineData(" 482913")]
    [InlineData("482913 ")]
    public void Matches_WrongOrDifferentlyShapedCode_ReturnsFalse(string candidate)
    {
        CreateOtp("482913").Matches(candidate).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Matches_NullOrEmptyCandidate_ReturnsFalse(string? candidate)
    {
        CreateOtp("482913").Matches(candidate).Should().BeFalse();
    }

    [Fact]
    public void Matches_PreservesLeadingZeros()
    {
        var otp = CreateOtp("004213");

        otp.Matches("004213").Should().BeTrue();
        otp.Matches("4213").Should().BeFalse();
    }
}
