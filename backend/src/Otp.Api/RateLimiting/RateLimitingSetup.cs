using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Otp.Api.Http;

namespace Otp.Api.RateLimiting;

/// <summary>
/// Configures a fixed-window rate limit per client IP address. It caps how fast anyone can guess
/// codes and how quickly anyone can add entries to the in-memory stores.
/// </summary>
public static class RateLimitingSetup
{
    /// <summary>The name of the policy that <c>OtpController</c> enables.</summary>
    public const string PolicyName = "otp";

    /// <summary>Registers the validated options and the rate-limiter policy.</summary>
    /// <param name="services">The service collection to extend.</param>
    /// <param name="configuration">The configuration that contains the <c>RateLimiting</c> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOtpRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = WriteRejectionAsync;

            limiter.AddPolicy(PolicyName, httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                // Behind a reverse proxy every client shares one partition until forwarded headers
                // are configured (see the README).
                var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });
        });

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var retryAfterSeconds = 1;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
        }

        var response = context.HttpContext.Response;
        response.Headers[HeaderNames.RetryAfter] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = "Too many requests. Please wait a moment and try again.",
            Instance = context.HttpContext.Request.Path,
        };

        await ProblemDetailsWriter.WriteAsync(response, problem, cancellationToken);
    }
}
