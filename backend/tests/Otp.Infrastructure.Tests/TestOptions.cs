using Microsoft.Extensions.Options;
using Otp.Application.Options;

namespace Otp.Infrastructure.Tests;

/// <summary>Builds option wrappers with a valid secret key for the tests.</summary>
internal static class TestOptions
{
    public const string SecretKey = "unit-test-secret-key-that-is-at-least-32-chars";

    public static IOptions<OtpOptions> Create(Action<OtpOptions>? configure = null)
    {
        var options = new OtpOptions { SecretKey = SecretKey };
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }
}
