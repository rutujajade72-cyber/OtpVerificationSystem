using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Otp.Api.Http;

namespace Otp.Api.ExceptionHandling;

/// <summary>
/// The last line of defence: logs any unhandled exception with the request path and answers with a
/// generic 500 problem. Stack traces and exception messages never reach the client.
/// </summary>
public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>Creates the handler.</summary>
    /// <param name="logger">The logger that records the exception.</param>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var path = httpContext.Request.Path.Value;
        LogUnhandled(exception, path);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Instance = path,
        };

        await ProblemDetailsWriter.WriteAsync(httpContext.Response, problem, cancellationToken);
        return true;
    }

    [LoggerMessage(EventId = 30, Level = LogLevel.Error, Message = "Unhandled exception while processing {Path}")]
    private partial void LogUnhandled(Exception exception, string? path);
}
