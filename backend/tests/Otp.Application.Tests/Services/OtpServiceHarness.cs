using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Otp.Application.Abstractions;
using Otp.Application.Models;
using Otp.Application.Options;
using Otp.Application.Services;
using Otp.Infrastructure.Repositories;

namespace Otp.Application.Tests.Services;

/// <summary>Constants shared by the service tests.</summary>
public static class TestData
{
    /// <summary>The code the substituted generator returns.</summary>
    public const string Code = "731946";

    /// <summary>A code that is different from <see cref="Code"/>.</summary>
    public const string WrongCode = "918273";

    /// <summary>The user used by most tests.</summary>
    public const string UserId = "alice";
}

/// <summary>
/// Wires an <see cref="OtpService"/> to a fake clock, a substituted generator and the real
/// in-memory stores, so tests can move time and observe the resulting behaviour exactly.
/// </summary>
internal sealed class OtpServiceHarness
{
    public OtpServiceHarness(Action<OtpOptions>? configure = null)
    {
        Generator.GenerateCode(Arg.Any<string>(), Arg.Any<DateTimeOffset>()).Returns(TestData.Code);

        Settings = TestOptions.Create(configure);
        Service = new OtpService(Generator, Otps, Failures, Clock, Settings, Logger);
    }

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero));

    public IOtpGenerator Generator { get; } = Substitute.For<IOtpGenerator>();

    public InMemoryOtpRepository Otps { get; } = new();

    public InMemoryFailedAttemptRepository Failures { get; } = new();

    public FakeLogger<OtpService> Logger { get; } = new();

    public Microsoft.Extensions.Options.IOptions<OtpOptions> Settings { get; }

    public OtpService Service { get; }

    public Task<GenerateOtpResult> GenerateAsync(string userId = TestData.UserId, DateTimeOffset? dateTime = null) =>
        Service.GenerateAsync(new GenerateOtpCommand(userId, dateTime ?? Clock.GetUtcNow()));

    public Task<ValidateOtpResult> ValidateAsync(string code = TestData.Code, string userId = TestData.UserId) =>
        Service.ValidateAsync(new ValidateOtpCommand(userId, code));

    public async Task<ValidateOtpResult> ValidateWrongCodeAsync(int times, string userId = TestData.UserId)
    {
        ValidateOtpResult? last = null;
        for (var i = 0; i < times; i++)
        {
            last = await ValidateAsync(TestData.WrongCode, userId);
        }

        return last ?? throw new ArgumentOutOfRangeException(nameof(times), "At least one attempt is required.");
    }
}
