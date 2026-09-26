using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IUserActionTokenRepository
{
    Task<UserActionToken?> GetByHashAsync(
        string tokenHash,
        UserActionTokenType type,
        CancellationToken cancellationToken = default);
    Task<UserActionToken?> GetByHashForUpdateAsync(
        string tokenHash,
        UserActionTokenType type,
        CancellationToken cancellationToken = default);
    Task AddAsync(UserActionToken token, CancellationToken cancellationToken = default);
    Task ConsumeActiveForUserAsync(
        Guid userId,
        UserActionTokenType type,
        DateTimeOffset consumedAt,
        CancellationToken cancellationToken = default);
}
