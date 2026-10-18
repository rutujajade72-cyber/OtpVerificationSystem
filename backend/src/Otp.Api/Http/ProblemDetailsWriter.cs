using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Otp.Api.Http;

/// <summary>Writes RFC 7807 problem details to a response outside of MVC (middleware and handlers).</summary>
internal static class ProblemDetailsWriter
{
    public static async Task WriteAsync(HttpResponse response, ProblemDetails problem, CancellationToken cancellationToken)
    {
        response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        response.ContentType = "application/problem+json";
        await response.WriteAsync(JsonSerializer.Serialize(problem), cancellationToken);
    }
}
