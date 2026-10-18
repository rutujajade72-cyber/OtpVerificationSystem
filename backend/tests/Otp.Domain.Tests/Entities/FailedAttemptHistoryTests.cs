using Otp.Domain.Entities;

namespace Otp.Domain.Tests.Entities;

public sealed class FailedAttemptHistoryTests
{
    private static DateTimeOffset Start => new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);

    private static TimeSpan Window => TimeSpan.FromSeconds(60);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_BlankUserId_Throws(string? userId)
    {
        var act = () => new FailedAttemptHistory(userId!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ValidUserId_ExposesIt()
    {
        new FailedAttemptHistory("john.doe").UserId.Should().Be("john.doe");
    }

    [Fact]
    public void RegisterFailure_FirstFailure_ReturnsOne()
    {
        var history = new FailedAttemptHistory("john.doe");

        history.RegisterFailure(Start, Window).Should().Be(1);
    }

    [Fact]
    public void RegisterFailure_FailuresInsideWindow_CountsAllOfThem()
    {
        var history = new FailedAttemptHistory("john.doe");

        history.RegisterFailure(Start, Window);
        history.RegisterFailure(Start.AddSeconds(10), Window);
        var count = history.RegisterFailure(Start.AddSeconds(20), Window);

        count.Should().Be(3);
    }

    [Fact]
    public void RegisterFailure_FailureExactlyOneWindowOld_IsDropped()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);

        var count = history.RegisterFailure(Start.AddSeconds(60), Window);

        count.Should().Be(1);
    }

    [Fact]
    public void RegisterFailure_FailureJustInsideWindow_IsKept()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);

        var count = history.RegisterFailure(Start.AddMilliseconds(59_999), Window);

        count.Should().Be(2);
    }

    [Fact]
    public void GetLockedUntil_NoFailures_ReturnsNull()
    {
        var history = new FailedAttemptHistory("john.doe");

        history.GetLockedUntil(Start, Window, 3).Should().BeNull();
    }

    [Fact]
    public void GetLockedUntil_BelowMaximum_ReturnsNull()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);
        history.RegisterFailure(Start.AddSeconds(10), Window);

        history.GetLockedUntil(Start.AddSeconds(10), Window, 3).Should().BeNull();
    }

    [Fact]
    public void GetLockedUntil_AtMaximum_EndsOneWindowAfterTheOldestFailure()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);
        history.RegisterFailure(Start.AddSeconds(10), Window);
        history.RegisterFailure(Start.AddSeconds(20), Window);

        var lockedUntil = history.GetLockedUntil(Start.AddSeconds(20), Window, 3);

        lockedUntil.Should().Be(Start.AddSeconds(60));
    }

    [Fact]
    public void GetLockedUntil_OneSecondBeforeTheLockoutEnds_IsStillLocked()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);
        history.RegisterFailure(Start.AddSeconds(10), Window);
        history.RegisterFailure(Start.AddSeconds(20), Window);

        var lockedUntil = history.GetLockedUntil(Start.AddSeconds(59), Window, 3);

        lockedUntil.Should().Be(Start.AddSeconds(60));
    }

    [Fact]
    public void GetLockedUntil_WhenTheOldestFailureAgesOut_ReturnsNull()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);
        history.RegisterFailure(Start.AddSeconds(10), Window);
        history.RegisterFailure(Start.AddSeconds(20), Window);

        history.GetLockedUntil(Start.AddSeconds(60), Window, 3).Should().BeNull();
    }

    [Fact]
    public void GetLockedUntil_AboveMaximum_EndsWhenTheCountDropsBelowMaximum()
    {
        // Concurrent requests can overshoot the maximum: four failures with a maximum of three.
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);
        history.RegisterFailure(Start.AddSeconds(10), Window);
        history.RegisterFailure(Start.AddSeconds(20), Window);
        history.RegisterFailure(Start.AddSeconds(30), Window);

        // Dropping only the oldest failure (t = 60) would still leave three. The count falls
        // below three only when the second failure (t = 10) ages out, at t = 70.
        history.GetLockedUntil(Start.AddSeconds(30), Window, 3).Should().Be(Start.AddSeconds(70));
        history.GetLockedUntil(Start.AddSeconds(69), Window, 3).Should().Be(Start.AddSeconds(70));
        history.GetLockedUntil(Start.AddSeconds(70), Window, 3).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetLockedUntil_MaximumBelowOne_Throws(int maxFailures)
    {
        var history = new FailedAttemptHistory("john.doe");

        var act = () => history.GetLockedUntil(Start, Window, maxFailures);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void HasFailuresSince_NoFailures_ReturnsFalse()
    {
        new FailedAttemptHistory("john.doe").HasFailuresSince(Start).Should().BeFalse();
    }

    [Fact]
    public void HasFailuresSince_FailureAfterTheThreshold_ReturnsTrue()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);

        history.HasFailuresSince(Start.AddMilliseconds(-1)).Should().BeTrue();
    }

    [Fact]
    public void HasFailuresSince_FailureAtOrBeforeTheThreshold_ReturnsFalseAndDiscardsIt()
    {
        var history = new FailedAttemptHistory("john.doe");
        history.RegisterFailure(Start, Window);

        history.HasFailuresSince(Start).Should().BeFalse();

        // The failure was pruned, so it no longer counts towards a lockout either.
        history.GetLockedUntil(Start, Window, 1).Should().BeNull();
    }

    [Fact]
    public void RegisterFailure_ConcurrentCalls_CountEveryFailure()
    {
        var history = new FailedAttemptHistory("john.doe");

        Parallel.For(0, 100, _ => history.RegisterFailure(Start, Window));

        history.RegisterFailure(Start, Window).Should().Be(101);
    }
}
