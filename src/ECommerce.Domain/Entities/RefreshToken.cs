namespace ECommerce.Domain.Entities;

public sealed class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken()
    {
    }

    private RefreshToken(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        return new RefreshToken(userId, tokenHash, expiresAt);
    }

    public bool IsUsable() => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    public void Revoke(Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = DateTimeOffset.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}
