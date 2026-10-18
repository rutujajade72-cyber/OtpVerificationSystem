using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otp.Application.Abstractions;
using Otp.Application.Models;
using Otp.Application.Options;
using Otp.Domain.Entities;

namespace Otp.Application.Services;

/// <summary>
/// Implements the generate and validate use cases. The service is stateless: all state lives in
/// the repositories, so a new instance per request needs no synchronisation of its own.
/// </summary>
public sealed partial class OtpService : IOtpService
{
    private readonly IOtpGenerator _generator;
    private readonly IOtpRepository _otpRepository;
    private readonly IFailedAttemptRepository _failedAttemptRepository;
    private readonly TimeProvider _timeProvider;
    private readonly OtpOptions _options;
    private readonly ILogger<OtpService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="generator">Produces the one-time codes.</param>
    /// <param name="otpRepository">Stores the active password of each user.</param>
    /// <param name="failedAttemptRepository">Stores the wrong-code history of each user.</param>
    /// <param name="timeProvider">The clock. Tests replace it with a fake to control time.</param>
    /// <param name="options">The validated settings.</param>
    /// <param name="logger">The logger. The code is never passed to it.</param>
    public OtpService(
        IOtpGenerator generator,
        IOtpRepository otpRepository,
        IFailedAttemptRepository failedAttemptRepository,
        TimeProvider timeProvider,
        IOptions<OtpOptions> options,
        ILogger<OtpService> logger)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(otpRepository);
        ArgumentNullException.ThrowIfNull(failedAttemptRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _generator = generator;
        _otpRepository = otpRepository;
        _failedAttemptRepository = failedAttemptRepository;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GenerateOtpResult> GenerateAsync(
        GenerateOtpCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.UserId);

        var userId = command.UserId.Trim();
        var issuedAt = _timeProvider.GetUtcNow();

        var lockedUntil = await GetLockedUntilAsync(userId, issuedAt, cancellationToken);
        if (lockedUntil is not null)
        {
            LogGenerationRefused(userId, lockedUntil.Value);
            return GenerateOtpResult.LockedOut(ToWholeSeconds(lockedUntil.Value - issuedAt));
        }

        // The DateTime input decides WHICH code is produced. The validity window is anchored to
        // the server's issue time, so a past or future DateTime still yields a full-length
        // password and a client cannot extend the validity by sending a future time.
        var expiresAt = issuedAt.AddSeconds(_options.ValiditySeconds);
        var code = _generator.GenerateCode(userId, command.DateTime);
        var otp = new OneTimePassword(userId, code, issuedAt, expiresAt);

        await _otpRepository.SaveAsync(otp, cancellationToken);
        LogGenerated(userId, expiresAt);

        return GenerateOtpResult.Success(new GeneratedOtp(
            userId,
            code,
            issuedAt,
            expiresAt,
            _options.ValiditySeconds,
            ToWholeSeconds(otp.GetRemainingTime(issuedAt))));
    }

    /// <inheritdoc />
    public async Task<ValidateOtpResult> ValidateAsync(
        ValidateOtpCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.UserId);

        var userId = command.UserId.Trim();
        var now = _timeProvider.GetUtcNow();

        var result = await EvaluateAsync(userId, command.Code, now, cancellationToken);
        LogValidation(userId, result.Status);
        return result;
    }

    // The checks run in a deliberate order:
    //   1. Locked out?  Checked FIRST, otherwise a locked-out attacker could keep guessing and
    //      learn when a guess is right.
    //   2. No password stored?  NotFound.
    //   3. Expired?  Reported as Expired whether or not the code was right, and the stale entry is removed.
    //   4. Code matches?  Consume it atomically and clear the failures.
    //   5. Wrong code  Count the failure; on the last allowed failure revoke the password and lock out.
    private async Task<ValidateOtpResult> EvaluateAsync(
        string userId,
        string? code,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var lockedUntil = await GetLockedUntilAsync(userId, now, cancellationToken);
        if (lockedUntil is not null)
        {
            return ValidateOtpResult.AttemptsExceeded(ToWholeSeconds(lockedUntil.Value - now));
        }

        var otp = await _otpRepository.GetByUserIdAsync(userId, cancellationToken);
        if (otp is null)
        {
            return ValidateOtpResult.NotFound();
        }

        if (otp.IsExpired(now))
        {
            await _otpRepository.TryRemoveAsync(otp, cancellationToken);
            return ValidateOtpResult.Expired();
        }

        if (otp.Matches(code?.Trim()))
        {
            // Only one of two racing requests can remove the password. The loser sees NotFound.
            if (!await _otpRepository.TryRemoveAsync(otp, cancellationToken))
            {
                return ValidateOtpResult.NotFound();
            }

            await _failedAttemptRepository.RemoveAsync(userId, cancellationToken);
            return ValidateOtpResult.Valid();
        }

        return await RegisterFailureAsync(userId, otp, now, cancellationToken);
    }

    private async Task<ValidateOtpResult> RegisterFailureAsync(
        string userId,
        OneTimePassword otp,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromSeconds(_options.LockoutSeconds);
        var history = await _failedAttemptRepository.GetOrAddAsync(userId, cancellationToken);

        var failures = history.RegisterFailure(now, window);
        var remaining = Math.Max(0, _options.MaxFailedAttempts - failures);
        if (remaining > 0)
        {
            return ValidateOtpResult.Invalid(remaining);
        }

        // Revoke the password under attack: the user must generate a new one after the lockout.
        await _otpRepository.TryRemoveAsync(otp, cancellationToken);

        var lockedUntil = history.GetLockedUntil(now, window, _options.MaxFailedAttempts) ?? now + window;
        return ValidateOtpResult.AttemptsExceeded(ToWholeSeconds(lockedUntil - now));
    }

    private async Task<DateTimeOffset?> GetLockedUntilAsync(
        string userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var history = await _failedAttemptRepository.GetByUserIdAsync(userId, cancellationToken);
        return history?.GetLockedUntil(now, TimeSpan.FromSeconds(_options.LockoutSeconds), _options.MaxFailedAttempts);
    }

    // Rounds up, so a remaining 0.2 s is reported as 1 s and never as 0. Zero therefore only
    // ever means "expired" or "no longer locked".
    private static int ToWholeSeconds(TimeSpan span) => (int)Math.Ceiling(Math.Max(0, span.TotalSeconds));

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OTP generated for user {UserId}, expires at {ExpiresAt:O}")]
    private partial void LogGenerated(string userId, DateTimeOffset expiresAt);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "OTP generation refused for locked-out user {UserId} until {LockedUntil:O}")]
    private partial void LogGenerationRefused(string userId, DateTimeOffset lockedUntil);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "OTP validation for user {UserId}: {Status}")]
    private partial void LogValidation(string userId, OtpValidationStatus status);
}
