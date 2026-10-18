using System.ComponentModel.DataAnnotations;
using Otp.Application.Options;

namespace Otp.Application.Tests.Options;

public sealed class OtpOptionsTests
{
    internal const string ValidKey = "a-secret-key-that-is-at-least-32-characters-long";

    private static OtpOptions CreateValid() => new() { SecretKey = ValidKey };

    private static List<ValidationResult> Validate(OtpOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Defaults_MatchTheDocumentedValues()
    {
        var options = new OtpOptions();

        options.SecretKey.Should().BeEmpty();
        options.ValiditySeconds.Should().Be(30);
        options.CodeLength.Should().Be(6);
        options.MaxFailedAttempts.Should().Be(3);
        options.LockoutSeconds.Should().Be(60);
        options.CleanupIntervalSeconds.Should().Be(60);
    }

    [Fact]
    public void SectionName_IsOtp()
    {
        OtpOptions.SectionName.Should().Be("Otp");
    }

    [Fact]
    public void Validate_DefaultsWithAValidKey_Passes()
    {
        Validate(CreateValid()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_KeyOfExactlyThirtyTwoCharacters_Passes()
    {
        var options = new OtpOptions { SecretKey = new string('k', 32) };

        Validate(options).Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    public void Validate_MissingOrShortSecretKey_Fails(string key)
    {
        var options = new OtpOptions { SecretKey = key };

        Validate(options).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(nameof(OtpOptions.SecretKey));
    }

    [Fact]
    public void Validate_KeyOfThirtyOneCharacters_Fails()
    {
        var options = new OtpOptions { SecretKey = new string('k', 31) };

        Validate(options).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(nameof(OtpOptions.ValiditySeconds), 5)]
    [InlineData(nameof(OtpOptions.ValiditySeconds), 300)]
    [InlineData(nameof(OtpOptions.CodeLength), 4)]
    [InlineData(nameof(OtpOptions.CodeLength), 9)]
    [InlineData(nameof(OtpOptions.MaxFailedAttempts), 1)]
    [InlineData(nameof(OtpOptions.MaxFailedAttempts), 10)]
    [InlineData(nameof(OtpOptions.LockoutSeconds), 10)]
    [InlineData(nameof(OtpOptions.LockoutSeconds), 86400)]
    [InlineData(nameof(OtpOptions.CleanupIntervalSeconds), 5)]
    [InlineData(nameof(OtpOptions.CleanupIntervalSeconds), 3600)]
    public void Validate_ValueOnTheBoundary_Passes(string property, int value)
    {
        var options = CreateValid();
        typeof(OtpOptions).GetProperty(property)!.SetValue(options, value);

        Validate(options).Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(OtpOptions.ValiditySeconds), 4)]
    [InlineData(nameof(OtpOptions.ValiditySeconds), 301)]
    [InlineData(nameof(OtpOptions.CodeLength), 3)]
    [InlineData(nameof(OtpOptions.CodeLength), 10)]
    [InlineData(nameof(OtpOptions.MaxFailedAttempts), 0)]
    [InlineData(nameof(OtpOptions.MaxFailedAttempts), 11)]
    [InlineData(nameof(OtpOptions.LockoutSeconds), 9)]
    [InlineData(nameof(OtpOptions.LockoutSeconds), 86401)]
    [InlineData(nameof(OtpOptions.CleanupIntervalSeconds), 4)]
    [InlineData(nameof(OtpOptions.CleanupIntervalSeconds), 3601)]
    public void Validate_ValueOutsideTheAllowedRange_Fails(string property, int value)
    {
        var options = CreateValid();
        typeof(OtpOptions).GetProperty(property)!.SetValue(options, value);

        Validate(options).Should().ContainSingle()
            .Which.MemberNames.Should().Contain(property);
    }
}
