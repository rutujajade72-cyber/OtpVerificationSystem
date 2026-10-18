using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Otp.Application.Abstractions;
using Otp.Domain.Entities;
using Otp.Infrastructure.BackgroundJobs;
using Otp.Infrastructure.Repositories;

namespace Otp.Infrastructure.Tests.BackgroundJobs;

public sealed class OtpStoreCleanupServiceTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly InMemoryOtpRepository _otps = new();
    private readonly InMemoryFailedAttemptRepository _failures = new();
    private readonly FakeLogger<OtpStoreCleanupService> _logger = new();

    private OtpStoreCleanupService CreateService(
        IOtpRepository? otpRepository = null,
        Action<Otp.Application.Options.OtpOptions>? configure = null) =>
        new(
            otpRepository ?? _otps,
            _failures,
            _clock,
            TestOptions.Create(configure),
            _logger);

    private OneTimePassword CreateOtp(string userId, int issuedSecondsFromNow, int validitySeconds)
    {
        var issuedAt = _clock.GetUtcNow().AddSeconds(issuedSecondsFromNow);
        return new OneTimePassword(userId, "123456", issuedAt, issuedAt.AddSeconds(validitySeconds));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 250; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        throw new TimeoutException("The expected condition was not met within five seconds.");
    }

    [Fact]
    public async Task CleanUpAsync_RemovesExpiredPasswordsAndKeepsActiveOnes()
    {
        await _otps.SaveAsync(CreateOtp("expired.user", issuedSecondsFromNow: 0, validitySeconds: 30));
        await _otps.SaveAsync(CreateOtp("active.user", issuedSecondsFromNow: 0, validitySeconds: 300));
        _clock.Advance(TimeSpan.FromSeconds(31));

        await CreateService().CleanUpAsync();

        (await _otps.GetByUserIdAsync("expired.user")).Should().BeNull();
        (await _otps.GetByUserIdAsync("active.user")).Should().NotBeNull();
    }

    [Fact]
    public async Task CleanUpAsync_RemovesIdleFailureHistoriesButKeepsRecentOnes()
    {
        var window = TimeSpan.FromSeconds(60);
        var idle = await _failures.GetOrAddAsync("idle.user");
        idle.RegisterFailure(_clock.GetUtcNow(), window);
        _clock.Advance(TimeSpan.FromSeconds(50));
        var recent = await _failures.GetOrAddAsync("recent.user");
        recent.RegisterFailure(_clock.GetUtcNow(), window);
        _clock.Advance(TimeSpan.FromSeconds(11));

        await CreateService().CleanUpAsync();

        (await _failures.GetByUserIdAsync("idle.user")).Should().BeNull();
        (await _failures.GetByUserIdAsync("recent.user")).Should().NotBeNull();
    }

    [Fact]
    public async Task CleanUpAsync_NothingToRemove_LogsNothing()
    {
        await _otps.SaveAsync(CreateOtp("active.user", issuedSecondsFromNow: 0, validitySeconds: 300));

        await CreateService().CleanUpAsync();

        _logger.Collector.GetSnapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task CleanUpAsync_RemovedItems_LogsTheCountsAtDebugLevel()
    {
        await _otps.SaveAsync(CreateOtp("expired.user", issuedSecondsFromNow: 0, validitySeconds: 30));
        _clock.Advance(TimeSpan.FromSeconds(31));

        await CreateService().CleanUpAsync();

        var records = _logger.Collector.GetSnapshot();
        records.Should().ContainSingle();
        records[0].Level.Should().Be(LogLevel.Debug);
        records[0].Message.Should().Contain("1 expired password(s)");
    }

    [Fact]
    public async Task CleanUpAsync_NeverLogsTheCode()
    {
        await _otps.SaveAsync(CreateOtp("expired.user", issuedSecondsFromNow: 0, validitySeconds: 30));
        _clock.Advance(TimeSpan.FromSeconds(31));

        await CreateService().CleanUpAsync();

        _logger.Collector.GetSnapshot().Should().OnlyContain(record => !record.Message.Contains("123456"));
    }

    [Fact]
    public async Task ExecuteAsync_AfterTheInterval_PurgesExpiredPasswords()
    {
        // Already expired when the job starts, so the first tick removes it.
        await _otps.SaveAsync(CreateOtp("expired.user", issuedSecondsFromNow: -60, validitySeconds: 30));
        using var service = CreateService(configure: options => options.CleanupIntervalSeconds = 5);

        await service.StartAsync(CancellationToken.None);
        try
        {
            _clock.Advance(TimeSpan.FromSeconds(5));

            await WaitUntilAsync(async () => await _otps.GetByUserIdAsync("expired.user") is null);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ASweepThrows_LogsTheErrorAndKeepsRunning()
    {
        var otpRepository = Substitute.For<IOtpRepository>();
        otpRepository
            .RemoveExpiredAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException<int>(new InvalidOperationException("first sweep fails")),
                Task.FromResult(0));
        using var service = CreateService(otpRepository, options => options.CleanupIntervalSeconds = 5);

        await service.StartAsync(CancellationToken.None);
        try
        {
            _clock.Advance(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => Task.FromResult(
                _logger.Collector.GetSnapshot().Any(record => record.Level == LogLevel.Error)));

            _clock.Advance(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => Task.FromResult(
                otpRepository.ReceivedCalls().Count() >= 2));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StopAsync_WhileWaitingForTheNextTick_CompletesWithoutThrowing()
    {
        using var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        var act = () => service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_NullDependency_Throws()
    {
        var options = TestOptions.Create();

        Action nullOtps = () => _ = new OtpStoreCleanupService(null!, _failures, _clock, options, _logger);
        Action nullFailures = () => _ = new OtpStoreCleanupService(_otps, null!, _clock, options, _logger);
        Action nullClock = () => _ = new OtpStoreCleanupService(_otps, _failures, null!, options, _logger);
        Action nullOptions = () => _ = new OtpStoreCleanupService(_otps, _failures, _clock, null!, _logger);
        Action nullLogger = () => _ = new OtpStoreCleanupService(_otps, _failures, _clock, options, null!);

        nullOtps.Should().Throw<ArgumentNullException>();
        nullFailures.Should().Throw<ArgumentNullException>();
        nullClock.Should().Throw<ArgumentNullException>();
        nullOptions.Should().Throw<ArgumentNullException>();
        nullLogger.Should().Throw<ArgumentNullException>();
    }
}
