using System.Text.Json.Serialization;
using Otp.Api.Cors;
using Otp.Api.ExceptionHandling;
using Otp.Api.RateLimiting;
using Otp.Application;
using Otp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root: each layer registers its own services through one extension method.
builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure();
builder.Services.AddOtpRateLimiting(builder.Configuration);
builder.Services.AddFrontendCors();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Framework errors (400, 404, 429 and so on) become RFC 7807 problem responses.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

var app = builder.Build();

// Middleware order matters: errors first, then CORS, then the rate limiter, then the endpoints.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsSetup.PolicyName);
app.UseRateLimiter();
app.MapControllers();

app.Run();

/// <summary>Makes the entry point visible to WebApplicationFactory in the integration tests.</summary>
public partial class Program
{
}
