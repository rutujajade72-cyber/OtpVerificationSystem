using Otp.Domain.Entities;
using Otp.Infrastructure.Repositories;

namespace Otp.Infrastructure.Tests.Repositories;

public sealed class InMemoryFailedAttemptRepositoryTests
{
    private static DateTimeOffset Now => new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);

    private static TimeSpan Window => TimeSpan.FromSeconds(60);

    [Fact]
    public async Task GetByUserIdAsync_UserWhoNeverFailed_ReturnsNull()
    {
        var repository = new InMemoryFailedAttemptRepository();

        (await repository.GetByUserIdAsync("john.doe")).Should().BeNull();
    }

    [Fact]
    public async Task GetOrAddAsync_NewUser_CreatesAnEmptyHistory()
    {
        var repository = new InMemoryFailedAttemptRepository();

        var history = await repository.GetOrAddAsync("john.doe");

        history.UserId.Should().Be("john.doe");
        (await repository.GetByUserIdAsync("john.doe")).Should().BeSameAs(history);
    }

    [Fact]
    public async Task GetOrAddAsync_CalledTwice_ReturnsTheSameInstance()
    {
        var repository = new InMemoryFailedAttemptRepository();

        var first = await repository.GetOrAddAsync("john.doe");
        var second = await repository.GetOrAddAsync("john.doe");

        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetOrAddAsync_DifferentUsers_GetDifferentHistories()
    {
        var repository = new InMemoryFailedAttemptRepository();

        var john = await repository.GetOrAddAsync("john.doe");
        var jane = await repository.GetOrAddAsync("jane.doe");

        john.Should().NotBeSameAs(jane);
    }

    [Fact]
    public async Task GetOrAddAsync_ConcurrentCalls_ShareASingleInstance()
    {
        var repository = new InMemoryFailedAttemptRepository();

        var histories = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ => Task.Run(() => repository.GetOrAddAsync("john.doe"))));

        histories.Distinct().Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GetOrAddAsync_BlankUserId_Throws(string? userId)
    {
        var repository = new InMemoryFailedAttemptRepository();

        Action act = () => repository.GetOrAddAsync(userId!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task RemoveAsync_ExistingUser_ForgetsTheHistory()
    {
        var repository = new InMemoryFailedAttemptRepository();
        await repository.GetOrAddAsync("john.doe");

        await repository.RemoveAsync("john.doe");

        (await repository.GetByUserIdAsync("john.doe")).Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_UnknownUser_DoesNothing()
    {
        var repository = new InMemoryFailedAttemptRepository();

        var act = () => repository.RemoveAsync("nobody");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveInactiveAsync_RemovesHistoriesWithoutRecentFailures()
    {
        var repository = new InMemoryFailedAttemptRepository();
        var stale = await repository.GetOrAddAsync("stale.user");
        stale.RegisterFailure(Now, Window);
        var recent = await repository.GetOrAddAsync("recent.user");
        recent.RegisterFailure(Now.AddSeconds(50), Window);
        await repository.GetOrAddAsync("empty.user");

        var removed = await repository.RemoveInactiveAsync(Now.AddSeconds(10));

        removed.Should().Be(2);
        (await repository.GetByUserIdAsync("stale.user")).Should().BeNull();
        (await repository.GetByUserIdAsync("empty.user")).Should().BeNull();
        (await repository.GetByUserIdAsync("recent.user")).Should().BeSameAs(recent);
    }

    [Fact]
    public async Task RemoveInactiveAsync_AllHistoriesActive_ReturnsZero()
    {
        var repository = new InMemoryFailedAttemptRepository();
        var history = await repository.GetOrAddAsync("john.doe");
        history.RegisterFailure(Now, Window);

        (await repository.RemoveInactiveAsync(Now.AddSeconds(-1))).Should().Be(0);
    }
}
