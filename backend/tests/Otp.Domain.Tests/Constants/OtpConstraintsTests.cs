using Otp.Domain.Constants;

namespace Otp.Domain.Tests.Constants;

public sealed class OtpConstraintsTests
{
    [Fact]
    public void MaxUserIdLength_IsThirty()
    {
        OtpConstraints.MaxUserIdLength.Should().Be(30);
    }
}
