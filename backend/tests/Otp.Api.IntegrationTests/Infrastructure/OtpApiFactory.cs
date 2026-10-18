using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Otp.Api.RateLimiting;
using Otp.Application.Options;
using Otp.Infrastructure.BackgroundJobs;

namespace Otp.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API pipeline in memory. The clock is replaced by a shared fake so tests can
/// move time exactly, and the cleanup job is removed so advancing the clock cannot trigger a sweep
/// that purges a password the test is about to validate.
/// </summary>
public sealed class OtpApiFactory : WebApplicationFactory<Program>
{
    public const string SecretKey = "integration-test-secret-key-0123456789abcdef";

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<OtpOptions>(options => options.SecretKey = SecretKey);

            // The default limit would be shared by every test, because test requests have no remote IP.
            services.PostConfigure<RateLimitingOptions>(options => options.PermitLimit = 100_000);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            var cleanupJobs = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                    && descriptor.ImplementationType == typeof(OtpStoreCleanupService))
                .ToList();
            foreach (var descriptor in cleanupJobs)
            {
                services.Remove(descriptor);
            }
        });
    }
}
