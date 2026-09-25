using ECommerce.Infrastructure.Security;
using FluentAssertions;

namespace ECommerce.Infrastructure.Tests.Security;

public sealed class UserActionTokenServiceTests
{
    [Fact]
    public void Issue_ReturnsUniqueTokensAndDeterministicHashes()
    {
        var service = new UserActionTokenService();

        var first = service.Issue();
        var second = service.Issue();

        first.RawToken.Should().NotBe(second.RawToken);
        first.TokenHash.Should().HaveLength(64);
        service.Hash(first.RawToken).Should().Be(first.TokenHash);
    }
}
