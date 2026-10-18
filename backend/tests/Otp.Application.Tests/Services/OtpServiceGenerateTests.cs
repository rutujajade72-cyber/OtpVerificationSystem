using NSubstitute;
using Otp.Application.Models;
using Otp.Application.Services;

namespace Otp.Application.Tests.Services;

public sealed class OtpServiceGenerateTests
{
    private readonly OtpServiceHarness _harness = new();

    [Fact]
    public async Task GenerateAsync_ValidCommand_ReturnsTheCodeWithTheConfiguredValidity()
    {
        var result = await _harness.GenerateAsync();

        result.IsSuccess.Should().BeTrue();
        result.RetryAfterSeconds.Should().BeNull();
        result.Otp!.Code.Should().Be(TestData.Code);
        result.Otp.UserId.Should().Be(TestData.UserId);
        result.Otp.ValiditySeconds.Should().Be(30);
        result.Otp.RemainingSeconds.Should().Be(30);
    }

    [Fact]
    public async Task GenerateAsync_ValidCommand_AnchorsTheWindowToTheServerClock()
    {
        var issuedAt = _harness.Clock.GetUtcNow();

        var result = await _harness.GenerateAsync();

        result.Otp!.IssuedAt.Should().Be(issuedAt);
        result.Otp.ExpiresAt.Should().Be(issuedAt.AddSeconds(30));
    }

    [Fact]
    public async Task GenerateAsync_ValidCommand_PassesUserIdAndDateTimeToTheGenerator()
    {
        var dateTime = new DateTimeOffset(2026, 10, 16, 12, 30, 0, TimeSpan.Zero);

        await _harness.GenerateAsync("john.doe", dateTime);

        _harness.Generator.Received(1).GenerateCode("john.doe", dateTime);
    }

    [Fact]
    public async Task GenerateAsync_DateTimeInThePast_StillGivesAFullLengthPassword()
    {
        var past = _harness.Clock.GetUtcNow().AddDays(-3);

        var result = await _harness.GenerateAsync(dateTime: past);

        result.Otp!.ExpiresAt.Should().Be(_harness.Clock.GetUtcNow().AddSeconds(30));
        result.Otp.RemainingSeconds.Should().Be(30);
    }

    [Fact]
    public async Task GenerateAsync_DateTimeInTheFuture_DoesNotExtendTheValidity()
    {
        var future = _harness.Clock.GetUtcNow().AddDays(3);

        var result = await _harness.GenerateAsync(dateTime: future);

        result.Otp!.ExpiresAt.Should().Be(_harness.Clock.GetUtcNow().AddSeconds(30));
        result.Otp.ValiditySeconds.Should().Be(30);
    }

    [Fact]
    public async Task GenerateAsync_CustomValiditySetting_UsesIt()
    {
        var harness = new OtpServiceHarness(options => options.ValiditySeconds = 45);

        var result = await harness.GenerateAsync();

        result.Otp!.ValiditySeconds.Should().Be(45);
        result.Otp.RemainingSeconds.Should().Be(45);
        result.Otp.ExpiresAt.Should().Be(harness.Clock.GetUtcNow().AddSeconds(45));
    }

    [Fact]
    public async Task GenerateAsync_UserIdWithSurroundingWhitespace_IsTrimmed()
    {
        var result = await _harness.GenerateAsync("  john.doe  ");

        result.Otp!.UserId.Should().Be("john.doe");
        _harness.Generator.Received(1).GenerateCode("john.doe", Arg.Any<DateTimeOffset>());
        (await _harness.Otps.GetByUserIdAsync("john.doe")).Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_ValidCommand_StoresThePasswordForTheUser()
    {
        await _harness.GenerateAsync();

        var stored = await _harness.Otps.GetByUserIdAsync(TestData.UserId);

        stored.Should().NotBeNull();
        stored!.Code.Should().Be(TestData.Code);
    }

    [Fact]
    public async Task GenerateAsync_CalledAgain_ReplacesThePreviousPassword()
    {
        _harness.Generator.GenerateCode(Arg.Any<string>(), Arg.Any<DateTimeOffset>()).Returns("111111", "222222");

        await _harness.GenerateAsync();
        await _harness.GenerateAsync();

        (await _harness.ValidateAsync("111111")).Status.Should().Be(OtpValidationStatus.Invalid);
        (await _harness.ValidateAsync("222222")).Status.Should().Be(OtpValidationStatus.Valid);
    }

    [Fact]
    public async Task GenerateAsync_UserLockedOut_RefusesAndReportsTheWait()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);

        var result = await _harness.GenerateAsync();

        result.IsSuccess.Should().BeFalse();
        result.Otp.Should().BeNull();
        result.RetryAfterSeconds.Should().Be(60);
    }

    [Fact]
    public async Task GenerateAsync_UserLockedOut_DoesNotCallTheGenerator()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Generator.ClearReceivedCalls();

        await _harness.GenerateAsync();

        _harness.Generator.DidNotReceiveWithAnyArgs().GenerateCode(default!, default);
    }

    [Fact]
    public async Task GenerateAsync_LockoutCountsDownWithTheClock()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Clock.Advance(TimeSpan.FromSeconds(45));

        var result = await _harness.GenerateAsync();

        result.RetryAfterSeconds.Should().Be(15);
    }

    [Fact]
    public async Task GenerateAsync_AfterTheLockoutEnds_IssuesANewPassword()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Clock.Advance(TimeSpan.FromSeconds(60));

        var result = await _harness.GenerateAsync();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_AnotherUserIsLockedOut_IsNotAffected()
    {
        await _harness.GenerateAsync("mallory");
        await _harness.ValidateWrongCodeAsync(3, "mallory");

        var result = await _harness.GenerateAsync("alice");

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAsync_BlankUserId_Throws(string userId)
    {
        var act = () => _harness.GenerateAsync(userId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateAsync_NullCommand_Throws()
    {
        var act = () => _harness.Service.GenerateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateAsync_GeneratorReturnsAnInvalidCode_Throws()
    {
        _harness.Generator.GenerateCode(Arg.Any<string>(), Arg.Any<DateTimeOffset>()).Returns(string.Empty);

        var act = () => _harness.GenerateAsync();

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullDependency_Throws()
    {
        var harness = new OtpServiceHarness();
        var options = harness.Settings;

        Action nullGenerator = () => _ = new OtpService(null!, harness.Otps, harness.Failures, harness.Clock, options, harness.Logger);
        Action nullOtps = () => _ = new OtpService(harness.Generator, null!, harness.Failures, harness.Clock, options, harness.Logger);
        Action nullFailures = () => _ = new OtpService(harness.Generator, harness.Otps, null!, harness.Clock, options, harness.Logger);
        Action nullClock = () => _ = new OtpService(harness.Generator, harness.Otps, harness.Failures, null!, options, harness.Logger);
        Action nullOptions = () => _ = new OtpService(harness.Generator, harness.Otps, harness.Failures, harness.Clock, null!, harness.Logger);
        Action nullLogger = () => _ = new OtpService(harness.Generator, harness.Otps, harness.Failures, harness.Clock, options, null!);

        nullGenerator.Should().Throw<ArgumentNullException>();
        nullOtps.Should().Throw<ArgumentNullException>();
        nullFailures.Should().Throw<ArgumentNullException>();
        nullClock.Should().Throw<ArgumentNullException>();
        nullOptions.Should().Throw<ArgumentNullException>();
        nullLogger.Should().Throw<ArgumentNullException>();
    }
}
