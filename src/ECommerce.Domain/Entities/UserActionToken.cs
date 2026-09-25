using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Domain.Entities;

public sealed class UserActionToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public UserActionTokenType Type { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private UserActionToken() { }

    private UserActionToken(
        Guid userId,
        string tokenHash,
        UserActionTokenType type,
        DateTimeOffset expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        Type = type;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static Result<UserActionToken> Create(
        Guid userId,
        string? tokenHash,
        UserActionTokenType type,
        DateTimeOffset expiresAt)
    {
        var errors = new List<string>();
        if (userId == Guid.Empty)
            errors.Add("User id is required.");
        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Trim().Length != 64)
            errors.Add("User action token hash must contain 64 characters.");
        if (!Enum.IsDefined(type))
            errors.Add("User action token type is invalid.");
        if (expiresAt <= DateTimeOffset.UtcNow)
            errors.Add("User action token expiration must be in the future.");

        return errors.Count > 0
            ? Result<UserActionToken>.Failure([.. errors])
            : Result<UserActionToken>.Success(new UserActionToken(
                userId,
                tokenHash!.Trim().ToLowerInvariant(),
                type,
                expiresAt));
    }

    public bool IsUsable(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    public Result Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
            return Result.Failure("User action token has already been consumed.");
        if (ExpiresAt <= now)
            return Result.Failure("User action token has expired.");

        ConsumedAt = now;
        return Result.Success();
    }
}
