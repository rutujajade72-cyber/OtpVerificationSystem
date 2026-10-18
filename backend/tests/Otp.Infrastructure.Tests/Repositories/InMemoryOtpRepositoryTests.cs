using Otp.Domain.Entities;
using Otp.Infrastructure.Repositories;

namespace Otp.Infrastructure.Tests.Repositories;

public sealed class InMemoryOtpRepositoryTests
{
    private static DateTimeOffset Now => new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);

    private static OneTimePassword CreateOtp(string userId = "john.doe", string code = "123456", int validitySeconds = 30) =>
        new(userId, code, Now, Now.AddSeconds(validitySeconds));

    [Fact]
    public async Task SaveAsync_NewPassword_CanBeRetrievedByUserId()
    {
        var repository = new InMemoryOtpRepository();
        var otp = CreateOtp();

        await repository.SaveAsync(otp);

        (await repository.GetByUserIdAsync("john.doe")).Should().BeSameAs(otp);
    }

    [Fact]
    public async Task SaveAsync_SecondPasswordForTheSameUser_ReplacesTheFirst()
    {
        var repository = new InMemoryOtpRepository();
        var first = CreateOtp(code: "111111");
        var second = CreateOtp(code: "222222");

        await repository.SaveAsync(first);
        await repository.SaveAsync(second);

        (await repository.GetByUserIdAsync("john.doe")).Should().BeSameAs(second);
    }

    [Fact]
    public async Task SaveAsync_PasswordsOfDifferentUsers_AreKeptSeparately()
    {
        var repository = new InMemoryOtpRepository();
        var john = CreateOtp("john.doe", "111111");
        var jane = CreateOtp("jane.doe", "222222");

        await repository.SaveAsync(john);
        await repository.SaveAsync(jane);

        (await repository.GetByUserIdAsync("john.doe")).Should().BeSameAs(john);
        (await repository.GetByUserIdAsync("jane.doe")).Should().BeSameAs(jane);
    }

    [Fact]
    public void SaveAsync_NullPassword_Throws()
    {
        var repository = new InMemoryOtpRepository();

        Action act = () => repository.SaveAsync(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetByUserIdAsync_UnknownUser_ReturnsNull()
    {
        var repository = new InMemoryOtpRepository();

        (await repository.GetByUserIdAsync("nobody")).Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_UserIdIsCaseSensitive()
    {
        var repository = new InMemoryOtpRepository();
        await repository.SaveAsync(CreateOtp("john.doe"));

        (await repository.GetByUserIdAsync("John.Doe")).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetByUserIdAsync_BlankUserId_Throws(string? userId)
    {
        var repository = new InMemoryOtpRepository();

        Action act = () => repository.GetByUserIdAsync(userId!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task TryRemoveAsync_StoredInstance_RemovesItAndReturnsTrue()
    {
        var repository = new InMemoryOtpRepository();
        var otp = CreateOtp();
        await repository.SaveAsync(otp);

        var removed = await repository.TryRemoveAsync(otp);

        removed.Should().BeTrue();
        (await repository.GetByUserIdAsync("john.doe")).Should().BeNull();
    }

    [Fact]
    public async Task TryRemoveAsync_AlreadyRemoved_ReturnsFalse()
    {
        var repository = new InMemoryOtpRepository();
        var otp = CreateOtp();
        await repository.SaveAsync(otp);
        await repository.TryRemoveAsync(otp);

        (await repository.TryRemoveAsync(otp)).Should().BeFalse();
    }

    [Fact]
    public async Task TryRemoveAsync_NeverStored_ReturnsFalse()
    {
        var repository = new InMemoryOtpRepository();

        (await repository.TryRemoveAsync(CreateOtp())).Should().BeFalse();
    }

    [Fact]
    public async Task TryRemoveAsync_OtpReplacedByNewer_KeepsTheNewerOtp()
    {
        var repository = new InMemoryOtpRepository();
        var older = CreateOtp(code: "111111");
        var newer = CreateOtp(code: "222222");
        await repository.SaveAsync(older);
        await repository.SaveAsync(newer);

        var removed = await repository.TryRemoveAsync(older);

        removed.Should().BeFalse();
        (await repository.GetByUserIdAsync("john.doe")).Should().BeSameAs(newer);
    }

    [Fact]
    public async Task TryRemoveAsync_EqualButDistinctInstance_DoesNotRemove()
    {
        var repository = new InMemoryOtpRepository();
        await repository.SaveAsync(CreateOtp());
        var lookAlike = CreateOtp();

        (await repository.TryRemoveAsync(lookAlike)).Should().BeFalse();
        (await repository.GetByUserIdAsync("john.doe")).Should().NotBeNull();
    }

    [Fact]
    public async Task TryRemoveAsync_ConcurrentCallsForTheSameOtp_OnlyOneSucceeds()
    {
        var repository = new InMemoryOtpRepository();
        var otp = CreateOtp();
        await repository.SaveAsync(otp);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ => Task.Run(() => repository.TryRemoveAsync(otp))));

        results.Count(removed => removed).Should().Be(1);
    }

    [Fact]
    public async Task RemoveExpiredAsync_RemovesOnlyExpiredPasswords()
    {
        var repository = new InMemoryOtpRepository();
        var expired = CreateOtp("expired.user", validitySeconds: 30);
        var active = CreateOtp("active.user", validitySeconds: 300);
        await repository.SaveAsync(expired);
        await repository.SaveAsync(active);

        var removed = await repository.RemoveExpiredAsync(Now.AddSeconds(30));

        removed.Should().Be(1);
        (await repository.GetByUserIdAsync("expired.user")).Should().BeNull();
        (await repository.GetByUserIdAsync("active.user")).Should().BeSameAs(active);
    }

    [Fact]
    public async Task RemoveExpiredAsync_NothingExpired_ReturnsZero()
    {
        var repository = new InMemoryOtpRepository();
        await repository.SaveAsync(CreateOtp());

        (await repository.RemoveExpiredAsync(Now.AddSeconds(29))).Should().Be(0);
        (await repository.GetByUserIdAsync("john.doe")).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveExpiredAsync_EmptyStore_ReturnsZero()
    {
        var repository = new InMemoryOtpRepository();

        (await repository.RemoveExpiredAsync(Now)).Should().Be(0);
    }
}
