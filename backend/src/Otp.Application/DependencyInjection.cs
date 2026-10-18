using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Otp.Application.Abstractions;
using Otp.Application.Options;
using Otp.Application.Services;

namespace Otp.Application;

/// <summary>Registers the services of the Application layer.</summary>
public static class DependencyInjection
{
    /// <summary>Registers the use cases and the validated <see cref="OtpOptions"/>.</summary>
    /// <param name="services">The service collection to extend.</param>
    /// <param name="configuration">The application configuration that contains the <c>Otp</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<OtpOptions>()
            .Bind(configuration.GetSection(OtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Stateless use-case service: one instance per request, holds no data of its own.
        services.AddScoped<IOtpService, OtpService>();

        return services;
    }
}
