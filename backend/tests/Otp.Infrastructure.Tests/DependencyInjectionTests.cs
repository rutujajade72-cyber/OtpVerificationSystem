using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Otp.Application.Abstractions;
using Otp.Infrastructure.BackgroundJobs;
using Otp.Infrastructure.Generation;
using Otp.Infrastructure.Repositories;

namespace Otp.Infrastructure.Tests;

public sealed class DependencyInjectionTests
{
    private static ServiceDescriptor DescriptorFor<TService>(IServiceCollection services) =>
        services.Single(descriptor => descriptor.ServiceType == typeof(TService));

    [Fact]
    public void AddInfrastructure_RegistersTheGeneratorAsASingleton()
    {
        var services = new ServiceCollection().AddInfrastructure();

        var descriptor = DescriptorFor<IOtpGenerator>(services);

        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationType.Should().Be(typeof(HmacOtpGenerator));
    }

    [Fact]
    public void AddInfrastructure_RegistersBothStoresAsSingletons()
    {
        var services = new ServiceCollection().AddInfrastructure();

        var otps = DescriptorFor<IOtpRepository>(services);
        var failures = DescriptorFor<IFailedAttemptRepository>(services);

        otps.Lifetime.Should().Be(ServiceLifetime.Singleton);
        otps.ImplementationType.Should().Be(typeof(InMemoryOtpRepository));
        failures.Lifetime.Should().Be(ServiceLifetime.Singleton);
        failures.ImplementationType.Should().Be(typeof(InMemoryFailedAttemptRepository));
    }

    [Fact]
    public void AddInfrastructure_RegistersTheCleanupJobAsAHostedService()
    {
        var services = new ServiceCollection().AddInfrastructure();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(OtpStoreCleanupService));
    }

    [Fact]
    public void AddInfrastructure_RegistersTheSystemClock()
    {
        var services = new ServiceCollection().AddInfrastructure();

        var descriptor = DescriptorFor<TimeProvider>(services);

        descriptor.ImplementationInstance.Should().BeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddInfrastructure_ClockAlreadyRegistered_KeepsTheExistingClock()
    {
        var customClock = new CustomClock();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(customClock);

        services.AddInfrastructure();

        var descriptor = DescriptorFor<TimeProvider>(services);
        descriptor.ImplementationInstance.Should().BeSameAs(customClock);
    }

    [Fact]
    public void AddInfrastructure_NullServices_Throws()
    {
        var act = () => DependencyInjection.AddInfrastructure(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class CustomClock : TimeProvider
    {
    }
}
