using ECommerce.Domain.Entities;
using FluentAssertions;

namespace ECommerce.Domain.Tests.Entities;

public class RefreshTokenTests
{
    [Fact]
    public void Create_SetsIdentityExpiryAndLeavesTokenActive()
    {
        var userId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        var token = RefreshToken.Create(userId, "token-hash", expiresAt);

        token.Id.Should().NotBeEmpty();
        token.UserId.Should().Be(userId);
        token.TokenHash.Should().Be("token-hash");
        token.ExpiresAt.Should().Be(expiresAt);
        token.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        token.RevokedAt.Should().BeNull();
        token.ReplacedByTokenId.Should().BeNull();
        token.IsUsable().Should().BeTrue();
    }

    [Fact]
    public void IsUsable_WhenExpired_ReturnsFalse()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "token-hash", DateTimeOffset.UtcNow.AddSeconds(-1));

        token.IsUsable().Should().BeFalse();
    }

    [Fact]
    public void Revoke_WhenActive_SetsRevokedAtAndReplacement()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "token-hash", DateTimeOffset.UtcNow.AddDays(7));
        var replacementId = Guid.NewGuid();

        token.Revoke(replacementId);

        token.RevokedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        token.ReplacedByTokenId.Should().Be(replacementId);
        token.IsUsable().Should().BeFalse();
    }

    [Fact]
    public void Revoke_WithoutReplacement_LeavesReplacedByTokenIdNull()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "token-hash", DateTimeOffset.UtcNow.AddDays(7));

        token.Revoke();

        token.RevokedAt.Should().NotBeNull();
        token.ReplacedByTokenId.Should().BeNull();
        token.IsUsable().Should().BeFalse();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_DoesNotChangeState()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "token-hash", DateTimeOffset.UtcNow.AddDays(7));
        var originalReplacementId = Guid.NewGuid();
        token.Revoke(originalReplacementId);
        var revokedAt = token.RevokedAt;

        token.Revoke(Guid.NewGuid());

        token.RevokedAt.Should().Be(revokedAt);
        token.ReplacedByTokenId.Should().Be(originalReplacementId);
    }

    [Fact]
    public void Create_AssignsDistinctIds()
    {
        var first = RefreshToken.Create(Guid.NewGuid(), "hash-1", DateTimeOffset.UtcNow.AddDays(1));
        var second = RefreshToken.Create(Guid.NewGuid(), "hash-2", DateTimeOffset.UtcNow.AddDays(1));

        first.Id.Should().NotBe(second.Id);
    }
}
