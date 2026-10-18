using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Otp.Api.Contracts;
using Otp.Api.Mapping;
using Otp.Api.RateLimiting;
using Otp.Application.Abstractions;

namespace Otp.Api.Controllers;

/// <summary>Generates and validates one-time passwords.</summary>
[ApiController]
[Route("api/otp")]
[EnableRateLimiting(RateLimitingSetup.PolicyName)]
public sealed class OtpController : ControllerBase
{
    private readonly IOtpService _otpService;

    /// <summary>Creates the controller. It depends only on the use-case interface.</summary>
    /// <param name="otpService">The one-time password use cases.</param>
    public OtpController(IOtpService otpService)
    {
        ArgumentNullException.ThrowIfNull(otpService);
        _otpService = otpService;
    }

    /// <summary>Generates a one-time password for a user and a date/time.</summary>
    /// <remarks>
    /// The password is valid for 30 seconds from the moment the server issues it, whatever date and
    /// time are sent. Generating again replaces the user's previous password.
    /// </remarks>
    /// <param name="request">The user id and the date/time the code is derived from.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The password and its remaining validity.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="429">The user is locked out, or the client sent too many requests.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<GenerateOtpResponse>> Generate(
        [FromBody] GenerateOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _otpService.GenerateAsync(request.ToCommand(), cancellationToken);

        if (!result.IsSuccess)
        {
            var retryAfterSeconds = result.RetryAfterSeconds ?? 0;
            Response.Headers[HeaderNames.RetryAfter] = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return Problem(
                title: "Too many incorrect attempts.",
                detail: OtpContractMapper.DescribeLockout(retryAfterSeconds),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        return Ok(result.Otp.ToResponse());
    }

    /// <summary>Validates a one-time password.</summary>
    /// <remarks>
    /// Always answers 200 when the request is well formed: a wrong, expired, used or locked-out
    /// outcome is described in the body. A valid code is consumed and works only once.
    /// </remarks>
    /// <param name="request">The user id and the code to check.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The outcome of the validation.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="429">The client sent too many requests.</response>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ValidateOtpResponse>> Validate(
        [FromBody] ValidateOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _otpService.ValidateAsync(request.ToCommand(), cancellationToken);
        return Ok(result.ToResponse());
    }
}
