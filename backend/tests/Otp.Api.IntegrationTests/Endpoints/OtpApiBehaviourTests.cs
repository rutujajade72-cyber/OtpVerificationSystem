using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Otp.Api.IntegrationTests.Infrastructure;
using Otp.Api.RateLimiting;
using Otp.Application.Abstractions;

namespace Otp.Api.IntegrationTests.Endpoints;

/// <summary>Cross-cutting behaviour: rate limiting, CORS, error handling and Swagger.</summary>
public sealed class OtpApiBehaviourTests : IClassFixture<OtpApiFactory>
{
    private readonly OtpApiFactory _factory;

    public OtpApiBehaviourTests(OtpApiFactory factory)
    {
        _factory = factory;
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, OtpEndpointsTests.GenerateUrl);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return request;
    }

    // ------------------------------------------------------------- rate limiting

    [Fact]
    public async Task RateLimit_MoreRequestsThanThePermitLimit_ReturnsTooManyRequests()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<RateLimitingOptions>(options => options.PermitLimit = 3)));
        using var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var request = 0; request < 4; request++)
        {
            var response = await client.PostAsync(OtpEndpointsTests.ValidateUrl, Json("""{"userId":"alice","code":"123456"}"""));
            statuses.Add(response.StatusCode);
        }

        statuses.Take(3).Should().OnlyContain(status => status == HttpStatusCode.OK);
        statuses[3].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task RateLimit_RejectedRequest_CarriesRetryAfterAndProblemDetails()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<RateLimitingOptions>(options => options.PermitLimit = 1)));
        using var client = factory.CreateClient();
        await client.PostAsync(OtpEndpointsTests.ValidateUrl, Json("""{"userId":"alice","code":"123456"}"""));

        var rejected = await client.PostAsync(OtpEndpointsTests.ValidateUrl, Json("""{"userId":"alice","code":"123456"}"""));

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.Contains("Retry-After").Should().BeTrue();
        int.Parse(rejected.Headers.GetValues("Retry-After").Single()).Should().BeInRange(1, 60);
        rejected.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        (await rejected.Content.ReadAsStringAsync()).Should().Contain("Too many requests");
    }

    [Fact]
    public async Task RateLimit_GenerateAndValidateShareOneLimit()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.PostConfigure<RateLimitingOptions>(options => options.PermitLimit = 2)));
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(OtpEndpointsTests.GenerateUrl, new { userId = "alice", dateTime = DateTimeOffset.UtcNow });
        await client.PostAsync(OtpEndpointsTests.ValidateUrl, Json("""{"userId":"alice","code":"123456"}"""));
        var third = await client.PostAsJsonAsync(OtpEndpointsTests.GenerateUrl, new { userId = "alice", dateTime = DateTimeOffset.UtcNow });

        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    // ----------------------------------------------------------------------- CORS

    [Fact]
    public async Task Cors_PreflightFromAnAllowedOrigin_IsAnswered()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                })));
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Preflight("http://localhost:5173"));

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle().Which.Should().Be("http://localhost:5173");
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().Should().Contain("POST");
    }

    [Fact]
    public async Task Cors_PreflightFromAnotherOrigin_IsNotGrantedAccess()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                })));
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Preflight("https://evil.example"));

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_WithNoConfiguredOrigins_NoOriginIsAllowed()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(Preflight("http://localhost:5173"));

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    // ------------------------------------------------------------- error handling

    [Fact]
    public async Task UnhandledException_ReturnsGenericProblemDetailsWithoutInternalDetails()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOtpService>();
                services.AddScoped<IOtpService, ThrowingOtpService>();
            }));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(OtpEndpointsTests.GenerateUrl, new { userId = "alice", dateTime = DateTimeOffset.UtcNow });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        body.Should().Contain("An unexpected error occurred.");
        body.Should().NotContain(ThrowingOtpService.SecretDetail);
        body.Should().NotContain("InvalidOperationException");
        body.Should().NotContain(" at Otp.");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------- Swagger

    [Fact]
    public async Task Swagger_OutsideDevelopment_IsNotExposed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Swagger_InDevelopment_DescribesBothEndpoints()
    {
        await using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var document = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        document.Should().Contain("/api/otp/generate");
        document.Should().Contain("/api/otp/validate");
    }
}
