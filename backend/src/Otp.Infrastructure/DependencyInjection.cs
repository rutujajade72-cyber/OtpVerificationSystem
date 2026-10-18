using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otp.Application.Abstractions;
using Otp.Infrastructure.BackgroundJobs;
using Otp.Infrastructure.Generation;
using Otp.Infrastructure.Repositories;

namespace Otp.Infrastructure;

/// <summary>Registers the services of the Infrastructure layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the generator, the in-memory stores and the cleanup job. Replacing the stores with
    /// Redis or SQL means adding classes here and changing two registration lines.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TryAdd: a host or a test that already registered its own clock keeps it.
        services.TryAddSingleton(TimeProvider.System);

        // Singletons: stateless generator, and stores that must outlive individual requests.
        services.AddSingleton<IOtpGenerator, HmacOtpGenerator>();
        services.AddSingleton<IOtpRepository, InMemoryOtpRepository>();
        services.AddSingleton<IFailedAttemptRepository, InMemoryFailedAttemptRepository>();

        services.AddHostedService<OtpStoreCleanupService>();

        return services;
    }
}
