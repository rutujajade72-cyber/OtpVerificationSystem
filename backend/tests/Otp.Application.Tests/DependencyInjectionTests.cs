using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Otp.Application.Abstractions;
using Otp.Application.Services;

namespace Otp.Application.Tests;

public sealed class DependencyInjectionTests
{
    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();

    [Fact]
    public void AddApplication_RegistersTheServiceAsScoped()
    {
        var services = new ServiceCollection();

        services.AddApplication(BuildConfiguration());

        var descriptor = services.Single(item => item.ServiceType == typeof(IOtpService));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
        descriptor.ImplementationType.Should().Be(typeof(OtpService));
    }

    [Fact]
    public void AddApplication_BindsTheOtpSection()
    {
        var configuration = BuildConfiguration(
            ("Otp:SecretKey", new string('k', 40)),
            ("Otp:ValiditySeconds", "45"),
            ("Otp:CodeLength", "8"),
            ("Otp:MaxFailedAttempts", "5"),
            ("Otp:LockoutSeconds", "120"),
            ("Otp:CleanupIntervalSeconds", "30"));
        var services = new ServiceCollection();
        services.AddApplication(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<Application.Options.OtpOptions>>().Value;

        options.SecretKey.Should().HaveLength(40);
        options.ValiditySeconds.Should().Be(45);
        options.CodeLength.Should().Be(8);
        options.MaxFailedAttempts.Should().Be(5);
        options.LockoutSeconds.Should().Be(120);
        options.CleanupIntervalSeconds.Should().Be(30);
    }

    [Fact]
    public void AddApplication_MissingSecretKey_FailsWhenTheOptionsAreFirstUsed()
    {
        var services = new ServiceCollection();
        services.AddApplication(BuildConfiguration());
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<Application.Options.OtpOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddApplication_ShortSecretKey_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddApplication(BuildConfiguration(("Otp:SecretKey", "too-short")));
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<Application.Options.OtpOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddApplication_OutOfRangeSetting_FailsValidation()
    {
        var services = new ServiceCollection();
        services.AddApplication(BuildConfiguration(
            ("Otp:SecretKey", new string('k', 40)),
            ("Otp:CodeLength", "12")));
        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<Application.Options.OtpOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddApplication_NullServices_Throws()
    {
        var act = () => DependencyInjection.AddApplication(null!, BuildConfiguration());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddApplication_NullConfiguration_Throws()
    {
        var act = () => new ServiceCollection().AddApplication(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
