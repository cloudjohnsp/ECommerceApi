using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public sealed class UserActionTokenTests
{
    private const string TokenHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Create_WithValidData_ReturnsUsableToken()
    {
        var result = UserActionToken.Create(
            Guid.NewGuid(),
            TokenHash.ToUpperInvariant(),
            UserActionTokenType.EmailConfirmation,
            DateTimeOffset.UtcNow.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        result.Value!.TokenHash.Should().Be(TokenHash);
        result.Value.IsUsable(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void Consume_WhenValid_MakesTokenUnusable()
    {
        var token = UserActionToken.Create(
            Guid.NewGuid(),
            TokenHash,
            UserActionTokenType.PasswordReset,
            DateTimeOffset.UtcNow.AddHours(1)).Value!;
        var now = DateTimeOffset.UtcNow;

        var result = token.Consume(now);

        result.IsSuccess.Should().BeTrue();
        token.ConsumedAt.Should().Be(now);
        token.IsUsable(now).Should().BeFalse();
        token.Consume(now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithExpiredToken_ReturnsFailure()
    {
        var result = UserActionToken.Create(
            Guid.NewGuid(),
            TokenHash,
            UserActionTokenType.PasswordReset,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        result.IsFailure.Should().BeTrue();
    }
}
