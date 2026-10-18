using System.Text.RegularExpressions;
using Otp.Infrastructure.Generation;

namespace Otp.Infrastructure.Tests.Generation;

public sealed class HmacOtpGeneratorTests
{
    private static DateTimeOffset Instant => new(2026, 10, 16, 12, 30, 0, TimeSpan.Zero);

    private static HmacOtpGenerator CreateGenerator(int codeLength = 6, string secretKey = TestOptions.SecretKey) =>
        new(TestOptions.Create(options =>
        {
            options.CodeLength = codeLength;
            options.SecretKey = secretKey;
        }));

    [Fact]
    public void GenerateCode_SameInputs_ReturnsTheSameCode()
    {
        var generator = CreateGenerator();

        var first = generator.GenerateCode("john.doe", Instant);
        var second = generator.GenerateCode("john.doe", Instant);

        first.Should().Be(second);
    }

    [Fact]
    public void GenerateCode_TwoGeneratorsWithTheSameKey_ReturnTheSameCode()
    {
        var first = CreateGenerator().GenerateCode("john.doe", Instant);
        var second = CreateGenerator().GenerateCode("john.doe", Instant);

        first.Should().Be(second);
    }

    [Fact]
    public void GenerateCode_KnownInput_MatchesTheReferenceVector()
    {
        // Reference values computed independently with Node's crypto module:
        // HMAC-SHA256(key, "john.doe|<UTC ticks>"), RFC 4226 dynamic truncation, modulo 10^n.
        CreateGenerator(codeLength: 6).GenerateCode("john.doe", Instant).Should().Be("286130");
        CreateGenerator(codeLength: 9).GenerateCode("john.doe", Instant).Should().Be("635286130");
    }

    [Fact]
    public void GenerateCode_CodeWithLeadingZero_KeepsTheZeroes()
    {
        // Reference vector whose six-digit code starts with zero.
        CreateGenerator().GenerateCode("u3", Instant).Should().Be("045852");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    public void GenerateCode_AnyConfiguredLength_ReturnsExactlyThatManyDigits(int codeLength)
    {
        var generator = CreateGenerator(codeLength);

        var code = generator.GenerateCode("john.doe", Instant);

        code.Should().MatchRegex($"^[0-9]{{{codeLength}}}$");
    }

    [Fact]
    public void GenerateCode_ManyDifferentInstants_AlwaysReturnsSixDigitsIncludingLeadingZeroes()
    {
        var generator = CreateGenerator();
        var pattern = new Regex("^[0-9]{6}$");

        for (var second = 0; second < 2000; second++)
        {
            var code = generator.GenerateCode("john.doe", Instant.AddSeconds(second));

            pattern.IsMatch(code).Should().BeTrue($"the code for second {second} was '{code}'");
        }
    }

    [Fact]
    public void GenerateCode_DifferentUsers_ReturnDifferentCodes()
    {
        var generator = CreateGenerator(codeLength: 9);

        var first = generator.GenerateCode("john.doe", Instant);
        var second = generator.GenerateCode("jane.doe", Instant);

        first.Should().NotBe(second);
    }

    [Fact]
    public void GenerateCode_InstantsOneTickApart_ReturnDifferentCodes()
    {
        var generator = CreateGenerator(codeLength: 9);

        var first = generator.GenerateCode("john.doe", Instant);
        var second = generator.GenerateCode("john.doe", Instant.AddTicks(1));

        first.Should().NotBe(second);
    }

    [Fact]
    public void GenerateCode_InstantsOneSecondApart_ReturnDifferentCodes()
    {
        var generator = CreateGenerator(codeLength: 9);

        var first = generator.GenerateCode("john.doe", Instant);
        var second = generator.GenerateCode("john.doe", Instant.AddSeconds(1));

        first.Should().NotBe(second);
    }

    [Fact]
    public void GenerateCode_SameInstantWrittenWithDifferentOffsets_ReturnsTheSameCode()
    {
        var generator = CreateGenerator();
        var utc = new DateTimeOffset(2026, 10, 16, 12, 30, 0, TimeSpan.Zero);
        var plusTwoHours = new DateTimeOffset(2026, 10, 16, 14, 30, 0, TimeSpan.FromHours(2));

        generator.GenerateCode("john.doe", plusTwoHours).Should().Be(generator.GenerateCode("john.doe", utc));
    }

    [Fact]
    public void GenerateCode_DifferentSecretKeys_ReturnDifferentCodes()
    {
        var first = CreateGenerator(9, "first-secret-key-that-is-long-enough-0123456789").GenerateCode("john.doe", Instant);
        var second = CreateGenerator(9, "other-secret-key-that-is-long-enough-0123456789").GenerateCode("john.doe", Instant);

        first.Should().NotBe(second);
    }

    [Fact]
    public void GenerateCode_UserIdIsCaseSensitive()
    {
        var generator = CreateGenerator(codeLength: 9);

        generator.GenerateCode("John.Doe", Instant).Should().NotBe(generator.GenerateCode("john.doe", Instant));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateCode_BlankUserId_Throws(string? userId)
    {
        var generator = CreateGenerator();

        var act = () => generator.GenerateCode(userId!, Instant);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10)]
    public void Constructor_CodeLengthOutsideSupportedRange_Throws(int codeLength)
    {
        var act = () => CreateGenerator(codeLength);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankSecretKey_Throws(string secretKey)
    {
        var act = () => CreateGenerator(secretKey: secretKey);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new HmacOtpGenerator(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
