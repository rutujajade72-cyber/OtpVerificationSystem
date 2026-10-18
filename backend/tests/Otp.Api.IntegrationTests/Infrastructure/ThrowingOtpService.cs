using Otp.Application.Abstractions;
using Otp.Application.Models;

namespace Otp.Api.IntegrationTests.Infrastructure;

/// <summary>A service that always fails, used to prove the API hides internal error details.</summary>
public sealed class ThrowingOtpService : IOtpService
{
    public const string SecretDetail = "boom-internal-detail";

    public Task<GenerateOtpResult> GenerateAsync(GenerateOtpCommand command, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(SecretDetail);

    public Task<ValidateOtpResult> ValidateAsync(ValidateOtpCommand command, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(SecretDetail);
}
