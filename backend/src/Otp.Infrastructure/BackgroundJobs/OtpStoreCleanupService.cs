using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otp.Application.Abstractions;
using Otp.Application.Options;

namespace Otp.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically removes expired passwords and idle failure histories so the in-memory stores
/// stay bounded. Removing them never changes behaviour: an expired password can no longer be
/// validated, and a history whose failures are all older than the lockout window can no longer
/// lock anyone out.
/// </summary>
public sealed partial class OtpStoreCleanupService : BackgroundService
{
    private readonly IOtpRepository _otpRepository;
    private readonly IFailedAttemptRepository _failedAttemptRepository;
    private readonly TimeProvider _timeProvider;
    private readonly OtpOptions _options;
    private readonly ILogger<OtpStoreCleanupService> _logger;

    /// <summary>Creates the job.</summary>
    /// <param name="otpRepository">The password store to purge.</param>
    /// <param name="failedAttemptRepository">The failure-history store to purge.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="options">The settings that supply the interval and the lockout window.</param>
    /// <param name="logger">The logger.</param>
    public OtpStoreCleanupService(
        IOtpRepository otpRepository,
        IFailedAttemptRepository failedAttemptRepository,
        TimeProvider timeProvider,
        IOptions<OtpOptions> options,
        ILogger<OtpStoreCleanupService> logger)
    {
        ArgumentNullException.ThrowIfNull(otpRepository);
        ArgumentNullException.ThrowIfNull(failedAttemptRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _otpRepository = otpRepository;
        _failedAttemptRepository = failedAttemptRepository;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Runs one sweep. Public so tests can call it without waiting for the timer.</summary>
    /// <param name="cancellationToken">Cancels the sweep.</param>
    /// <returns>A task that completes when the sweep is done.</returns>
    public async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var expiredOtps = await _otpRepository.RemoveExpiredAsync(now, cancellationToken);
        var idleHistories = await _failedAttemptRepository.RemoveInactiveAsync(
            now - TimeSpan.FromSeconds(_options.LockoutSeconds),
            cancellationToken);

        if (expiredOtps > 0 || idleHistories > 0)
        {
            LogCleanedUp(expiredOtps, idleHistories);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.CleanupIntervalSeconds), _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanUpAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // One bad sweep must never kill the job.
                    LogCleanupFailed(exception);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down: exit quietly.
        }
    }

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "OTP store cleanup removed {ExpiredOtps} expired password(s) and {IdleHistories} idle failure history(ies)")]
    private partial void LogCleanedUp(int expiredOtps, int idleHistories);

    [LoggerMessage(EventId = 21, Level = LogLevel.Error, Message = "OTP store cleanup failed; it will be retried at the next interval")]
    private partial void LogCleanupFailed(Exception exception);
}
