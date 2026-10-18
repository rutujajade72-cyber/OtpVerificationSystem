using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Otp.Api.Cors;

/// <summary>
/// Configures the CORS policy used by the browser frontend. The allowed origins come from the
/// <c>Cors:AllowedOrigins</c> list and are empty by default outside Development, so no other
/// website can call the API from a browser.
/// </summary>
public static class CorsSetup
{
    /// <summary>The name of the policy.</summary>
    public const string PolicyName = "Frontend";

    /// <summary>Registers the policy. The origins are read when the policy is first needed.</summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Post)
                .AllowAnyHeader());
        });

        return services;
    }
}
