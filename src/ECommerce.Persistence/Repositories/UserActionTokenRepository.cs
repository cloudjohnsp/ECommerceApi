using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Persistence.Repositories;

public sealed class UserActionTokenRepository(AppDbContext dbContext) : IUserActionTokenRepository
{
    public Task<UserActionToken?> GetByHashAsync(
        string tokenHash,
        UserActionTokenType type,
        CancellationToken cancellationToken = default) =>
        dbContext.UserActionTokens.FirstOrDefaultAsync(
            token => token.TokenHash == tokenHash && token.Type == type,
            cancellationToken);

    public Task<UserActionToken?> GetByHashForUpdateAsync(
        string tokenHash,
        UserActionTokenType type,
        CancellationToken cancellationToken = default) => dbContext.Database.IsRelational()
            ? dbContext.UserActionTokens
                .FromSqlInterpolated($"""
                    SELECT * FROM user_action_tokens
                    WHERE token_hash = {tokenHash} AND type = {(int)type}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken)
            : dbContext.UserActionTokens.SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash && token.Type == type,
                cancellationToken);

    public async Task AddAsync(UserActionToken token, CancellationToken cancellationToken = default) =>
        await dbContext.UserActionTokens.AddAsync(token, cancellationToken);

    public async Task ConsumeActiveForUserAsync(
        Guid userId,
        UserActionTokenType type,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default)
    {
        var tokens = await dbContext.UserActionTokens
            .Where(token => token.UserId == userId &&
                token.Type == type &&
                token.ConsumedAt == null &&
                token.ExpiresAt > consumedAt)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
            token.Consume(consumedAt);
    }
}
