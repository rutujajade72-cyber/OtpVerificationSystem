using NSubstitute;
using Otp.Application.Models;
using Otp.Application.Services;
using Otp.Domain.Entities;

namespace Otp.Application.Tests.Services;

public sealed class OtpServiceValidateTests
{
    private readonly OtpServiceHarness _harness = new();

    [Fact]
    public async Task ValidateAsync_CorrectCode_ReturnsValid()
    {
        await _harness.GenerateAsync();

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.Valid);
        result.IsValid.Should().BeTrue();
        result.RemainingAttempts.Should().BeNull();
        result.RetryAfterSeconds.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_CorrectCodeUsedTwice_SecondUseIsNotFound()
    {
        await _harness.GenerateAsync();

        await _harness.ValidateAsync();
        var second = await _harness.ValidateAsync();

        second.Status.Should().Be(OtpValidationStatus.NotFound);
        second.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_CodeWithSurroundingWhitespace_IsAccepted()
    {
        await _harness.GenerateAsync();

        var result = await _harness.ValidateAsync($"  {TestData.Code}  ");

        result.Status.Should().Be(OtpValidationStatus.Valid);
    }

    [Fact]
    public async Task ValidateAsync_UserIdWithSurroundingWhitespace_IsAccepted()
    {
        await _harness.GenerateAsync();

        var result = await _harness.ValidateAsync(userId: $"  {TestData.UserId}  ");

        result.Status.Should().Be(OtpValidationStatus.Valid);
    }

    [Fact]
    public async Task ValidateAsync_UserWithoutAPassword_ReturnsNotFound()
    {
        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.NotFound);
    }

    [Fact]
    public async Task ValidateAsync_PasswordBelongsToAnotherUser_ReturnsNotFound()
    {
        await _harness.GenerateAsync("alice");

        var result = await _harness.ValidateAsync(userId: "bob");

        result.Status.Should().Be(OtpValidationStatus.NotFound);
    }

    [Fact]
    public async Task ValidateAsync_FirstWrongCode_ReturnsInvalidWithTwoAttemptsLeft()
    {
        await _harness.GenerateAsync();

        var result = await _harness.ValidateAsync(TestData.WrongCode);

        result.Status.Should().Be(OtpValidationStatus.Invalid);
        result.RemainingAttempts.Should().Be(2);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_SecondWrongCode_ReturnsInvalidWithOneAttemptLeft()
    {
        await _harness.GenerateAsync();

        var result = await _harness.ValidateWrongCodeAsync(2);

        result.Status.Should().Be(OtpValidationStatus.Invalid);
        result.RemainingAttempts.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAsync_ThirdWrongCode_LocksTheUserOutForSixtySeconds()
    {
        await _harness.GenerateAsync();

        var result = await _harness.ValidateWrongCodeAsync(3);

        result.Status.Should().Be(OtpValidationStatus.AttemptsExceeded);
        result.RetryAfterSeconds.Should().Be(60);
        result.RemainingAttempts.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WrongCodeDoesNotConsumeThePassword()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateAsync(TestData.WrongCode);

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.Valid);
    }

    [Fact]
    public async Task ValidateAsync_WhenTheLockoutIsReached_RevokesThePassword()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Clock.Advance(TimeSpan.FromSeconds(60));

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.NotFound);
    }

    [Fact]
    public async Task ValidateAsync_LockedOutUser_IsRejectedBeforeTheCodeIsChecked()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);

        // Put a fresh, correct password back directly. A locked-out user must still be refused,
        // otherwise an attacker could keep guessing and learn when a guess is right.
        var now = _harness.Clock.GetUtcNow();
        await _harness.Otps.SaveAsync(new OneTimePassword(TestData.UserId, TestData.Code, now, now.AddSeconds(30)));

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.AttemptsExceeded);
    }

    [Fact]
    public async Task ValidateAsync_LockedOutUser_ReceivesTheRemainingWait()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Clock.Advance(TimeSpan.FromSeconds(20));

        var result = await _harness.ValidateAsync(TestData.WrongCode);

        result.Status.Should().Be(OtpValidationStatus.AttemptsExceeded);
        result.RetryAfterSeconds.Should().Be(40);
    }

    [Fact]
    public async Task ValidateAsync_LockoutWaitIsRoundedUpToWholeSeconds()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Clock.Advance(TimeSpan.FromMilliseconds(59_800));

        var result = await _harness.ValidateAsync(TestData.WrongCode);

        result.Status.Should().Be(OtpValidationStatus.AttemptsExceeded);
        result.RetryAfterSeconds.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAsync_AfterTheLockoutEnds_TheUserCanUseANewPassword()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Clock.Advance(TimeSpan.FromSeconds(60));
        await _harness.GenerateAsync();

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.Valid);
    }

    [Fact]
    public async Task ValidateAsync_SuccessfulValidation_ClearsTheFailureHistory()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(2);
        (await _harness.ValidateAsync()).Status.Should().Be(OtpValidationStatus.Valid);

        await _harness.GenerateAsync();
        var result = await _harness.ValidateAsync(TestData.WrongCode);

        // Two earlier failures were forgotten, so this is a fresh first failure.
        result.RemainingAttempts.Should().Be(2);
    }

    [Fact]
    public async Task ValidateAsync_FailuresOlderThanTheWindow_AreForgotten()
    {
        var harness = new OtpServiceHarness(options => options.ValiditySeconds = 300);
        await harness.GenerateAsync();
        await harness.ValidateAsync(TestData.WrongCode);
        harness.Clock.Advance(TimeSpan.FromSeconds(61));

        var result = await harness.ValidateAsync(TestData.WrongCode);

        result.Status.Should().Be(OtpValidationStatus.Invalid);
        result.RemainingAttempts.Should().Be(2);
    }

    [Fact]
    public async Task ValidateAsync_FailuresSpreadOutInsideTheWindow_StillLockTheUserOut()
    {
        var harness = new OtpServiceHarness(options => options.ValiditySeconds = 300);
        await harness.GenerateAsync();
        await harness.ValidateAsync(TestData.WrongCode);
        harness.Clock.Advance(TimeSpan.FromSeconds(25));
        await harness.ValidateAsync(TestData.WrongCode);
        harness.Clock.Advance(TimeSpan.FromSeconds(25));

        var result = await harness.ValidateAsync(TestData.WrongCode);

        result.Status.Should().Be(OtpValidationStatus.AttemptsExceeded);
        result.RetryAfterSeconds.Should().Be(10);
    }

    [Fact]
    public async Task ValidateAsync_CustomMaximumOfFiveAttempts_IsHonoured()
    {
        var harness = new OtpServiceHarness(options => options.MaxFailedAttempts = 5);
        await harness.GenerateAsync();

        var fourth = await harness.ValidateWrongCodeAsync(4);
        var fifth = await harness.ValidateAsync(TestData.WrongCode);

        fourth.RemainingAttempts.Should().Be(1);
        fifth.Status.Should().Be(OtpValidationStatus.AttemptsExceeded);
    }

    [Fact]
    public async Task ValidateAsync_FailuresOfOneUser_DoNotLockAnotherUser()
    {
        await _harness.GenerateAsync("mallory");
        await _harness.ValidateWrongCodeAsync(3, "mallory");
        await _harness.GenerateAsync("alice");

        var result = await _harness.ValidateAsync(userId: "alice");

        result.Status.Should().Be(OtpValidationStatus.Valid);
    }

    [Fact]
    public async Task ValidateAsync_JustBeforeThirtySeconds_IsStillValid()
    {
        await _harness.GenerateAsync();
        _harness.Clock.Advance(TimeSpan.FromMilliseconds(29_999));

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.Valid);
    }

    [Fact]
    public async Task ValidateAsync_AtExactlyThirtySeconds_ReturnsExpired()
    {
        await _harness.GenerateAsync();
        _harness.Clock.Advance(TimeSpan.FromSeconds(30));

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.Expired);
    }

    [Fact]
    public async Task ValidateAsync_LongAfterExpiry_ReturnsExpired()
    {
        await _harness.GenerateAsync();
        _harness.Clock.Advance(TimeSpan.FromMinutes(10));

        var result = await _harness.ValidateAsync();

        result.Status.Should().Be(OtpValidationStatus.Expired);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredPasswordWithAWrongCode_IsReportedAsExpiredNotInvalid()
    {
        await _harness.GenerateAsync();
        _harness.Clock.Advance(TimeSpan.FromSeconds(31));

        var result = await _harness.ValidateAsync(TestData.WrongCode);

        result.Status.Should().Be(OtpValidationStatus.Expired);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredPassword_IsRemovedSoTheNextCheckIsNotFound()
    {
        await _harness.GenerateAsync();
        _harness.Clock.Advance(TimeSpan.FromSeconds(31));
        await _harness.ValidateAsync();

        var second = await _harness.ValidateAsync();

        second.Status.Should().Be(OtpValidationStatus.NotFound);
        (await _harness.Otps.GetByUserIdAsync(TestData.UserId)).Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ExpiredPassword_DoesNotCountAsAFailedAttempt()
    {
        await _harness.GenerateAsync();
        _harness.Clock.Advance(TimeSpan.FromSeconds(31));
        await _harness.ValidateAsync(TestData.WrongCode);
        await _harness.GenerateAsync();

        var result = await _harness.ValidateAsync(TestData.WrongCode);

        result.RemainingAttempts.Should().Be(2);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12")]
    [InlineData("")]
    public async Task ValidateAsync_CodeOfTheWrongShape_CountsAsAWrongCode(string code)
    {
        await _harness.GenerateAsync();

        var result = await _harness.ValidateAsync(code);

        result.Status.Should().Be(OtpValidationStatus.Invalid);
        result.RemainingAttempts.Should().Be(2);
    }

    [Fact]
    public async Task ValidateAsync_NullCode_CountsAsAWrongCode()
    {
        await _harness.GenerateAsync();

        var result = await _harness.Service.ValidateAsync(new ValidateOtpCommand(TestData.UserId, null!));

        result.Status.Should().Be(OtpValidationStatus.Invalid);
    }

    [Fact]
    public async Task ValidateAsync_PasswordConsumedByAConcurrentRequest_ReturnsNotFound()
    {
        var harness = new OtpServiceHarness();
        var now = harness.Clock.GetUtcNow();
        var otp = new OneTimePassword(TestData.UserId, TestData.Code, now, now.AddSeconds(30));

        // Simulate losing the race: the password is still readable, but another request already removed it.
        var otps = Substitute.For<Otp.Application.Abstractions.IOtpRepository>();
        otps.GetByUserIdAsync(TestData.UserId, Arg.Any<CancellationToken>()).Returns(otp);
        otps.TryRemoveAsync(otp, Arg.Any<CancellationToken>()).Returns(false);
        var service = new OtpService(harness.Generator, otps, harness.Failures, harness.Clock, harness.Settings, harness.Logger);

        var result = await service.ValidateAsync(new ValidateOtpCommand(TestData.UserId, TestData.Code));

        result.Status.Should().Be(OtpValidationStatus.NotFound);
    }

    [Fact]
    public async Task ValidateAsync_ManyConcurrentRequestsWithTheCorrectCode_ExactlyOneSucceeds()
    {
        await _harness.GenerateAsync();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(_ => Task.Run(() => _harness.ValidateAsync())));

        results.Count(result => result.Status == OtpValidationStatus.Valid).Should().Be(1);
        results.Where(result => result.Status != OtpValidationStatus.Valid)
            .Should().OnlyContain(result => result.Status == OtpValidationStatus.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_BlankUserId_Throws(string userId)
    {
        var act = () => _harness.ValidateAsync(userId: userId);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateAsync_NullCommand_Throws()
    {
        var act = () => _harness.Service.ValidateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
