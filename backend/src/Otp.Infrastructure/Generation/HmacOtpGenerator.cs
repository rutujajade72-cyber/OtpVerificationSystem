using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Otp.Application.Abstractions;
using Otp.Application.Options;

namespace Otp.Infrastructure.Generation;

/// <summary>
/// Generates codes with HMAC-SHA256 and the dynamic truncation of RFC 4226 section 5.3.
/// The instance is stateless after construction (key bytes and modulus are computed once),
/// so one singleton can serve every request.
/// </summary>
/// <remarks>
/// Algorithm: <c>message = "{userId}|{utcTicks}"</c>, <c>hash = HMAC-SHA256(key, message)</c>,
/// <c>offset = hash[31] and 0x0F</c>, take four bytes at the offset as a 31-bit integer, then
/// reduce it modulo 10 to the power of the code length and left-pad with zeros.
/// </remarks>
public sealed class HmacOtpGenerator : IOtpGenerator
{
    /// <summary>The fewest digits a code may have.</summary>
    public const int MinCodeLength = 1;

    /// <summary>The most digits a code may have: the truncated HMAC value is a 31-bit integer.</summary>
    public const int MaxCodeLength = 9;

    private readonly byte[] _key;
    private readonly int _codeLength;
    private readonly int _modulus;

    /// <summary>Creates the generator.</summary>
    /// <param name="options">The settings that supply the secret key and the code length.</param>
    public HmacOtpGenerator(IOptions<OtpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.SecretKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.CodeLength, MinCodeLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.CodeLength, MaxCodeLength);

        _key = Encoding.UTF8.GetBytes(settings.SecretKey);
        _codeLength = settings.CodeLength;
        _modulus = ComputeModulus(settings.CodeLength);
    }

    /// <inheritdoc />
    public string GenerateCode(string userId, DateTimeOffset dateTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        // UtcTicks makes the code depend on the instant, not on the offset it was written with:
        // 14:30+02:00 and 12:30Z are the same instant and give the same code. The "|" separator
        // removes any ambiguity between the user id and the number.
        var text = string.Create(CultureInfo.InvariantCulture, $"{userId}|{dateTime.UtcTicks}");
        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(text));

        var value = DynamicTruncate(hash) % _modulus;
        return value.ToString(CultureInfo.InvariantCulture).PadLeft(_codeLength, '0');
    }

    private static int DynamicTruncate(byte[] hash)
    {
        var offset = hash[^1] & 0x0F;

        // The top bit is masked so the result is always a non-negative 31-bit integer.
        return ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
    }

    private static int ComputeModulus(int codeLength)
    {
        var modulus = 1;
        for (var i = 0; i < codeLength; i++)
        {
            modulus *= 10;
        }

        return modulus;
    }
}
