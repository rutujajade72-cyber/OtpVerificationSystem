using Microsoft.Extensions.Logging;
using Otp.Application.Models;

namespace Otp.Application.Tests.Services;

public sealed class OtpServiceLoggingTests
{
    private readonly OtpServiceHarness _harness = new();

    private IReadOnlyList<string> Messages =>
        _harness.Logger.Collector.GetSnapshot().Select(record => record.Message).ToList();

    // Drives every outcome the service can produce, so the assertions below cover every log path.
    private async Task ExerciseEveryPathAsync()
    {
        // Generated, Invalid, Invalid, AttemptsExceeded (lockout), refused generation, locked validation.
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        await _harness.GenerateAsync();
        await _harness.ValidateAsync();

        // NotFound, Generated, Valid, NotFound (single use).
        _harness.Clock.Advance(TimeSpan.FromSeconds(61));
        await _harness.ValidateAsync();
        await _harness.GenerateAsync();
        await _harness.ValidateAsync();
        await _harness.ValidateAsync();

        // Generated, Expired.
        await _harness.GenerateAsync();
        _harness.Clock.Advance(TimeSpan.FromSeconds(31));
        await _harness.ValidateAsync();
    }

    [Fact]
    public async Task Logging_OnEveryPath_IdentifiesTheUser()
    {
        await ExerciseEveryPathAsync();

        Messages.Should().NotBeEmpty();
        Messages.Should().OnlyContain(message => message.Contains(TestData.UserId));
    }

    [Fact]
    public async Task Logging_OnEveryPath_NeverContainsTheCode()
    {
        await ExerciseEveryPathAsync();

        Messages.Should().NotBeEmpty();
        Messages.Should().OnlyContain(message => !message.Contains(TestData.Code));
        Messages.Should().OnlyContain(message => !message.Contains(TestData.WrongCode));
    }

    [Fact]
    public async Task Logging_OnEveryPath_WritesExactlyOneRecordPerCall()
    {
        await ExerciseEveryPathAsync();

        // ExerciseEveryPathAsync makes 12 service calls and each of them logs exactly once.
        _harness.Logger.Collector.Count.Should().Be(12);
    }

    [Fact]
    public async Task GenerateAsync_Success_LogsAnInformationRecordWithTheExpiry()
    {
        await _harness.GenerateAsync();

        var record = _harness.Logger.Collector.GetSnapshot().Single();
        record.Level.Should().Be(LogLevel.Information);
        record.Message.Should().Contain("OTP generated for user alice");
        record.Message.Should().Contain(_harness.Clock.GetUtcNow().AddSeconds(30).ToString("O"));
    }

    [Fact]
    public async Task GenerateAsync_LockedOut_LogsAWarning()
    {
        await _harness.GenerateAsync();
        await _harness.ValidateWrongCodeAsync(3);
        _harness.Logger.Collector.Clear();

        await _harness.GenerateAsync();

        var record = _harness.Logger.Collector.GetSnapshot().Single();
        record.Level.Should().Be(LogLevel.Warning);
        record.Message.Should().Contain("refused");
    }

    [Theory]
    [InlineData(OtpValidationStatus.Valid)]
    [InlineData(OtpValidationStatus.NotFound)]
    public async Task ValidateAsync_Outcome_LogsTheStatusName(OtpValidationStatus expected)
    {
        if (expected == OtpValidationStatus.Valid)
        {
            await _harness.GenerateAsync();
        }

        _harness.Logger.Collector.Clear();
        await _harness.ValidateAsync();

        var record = _harness.Logger.Collector.GetSnapshot().Single();
        record.Level.Should().Be(LogLevel.Information);
        record.Message.Should().Contain(expected.ToString());
    }
}
