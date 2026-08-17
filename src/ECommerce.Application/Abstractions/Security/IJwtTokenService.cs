using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Security;

public interface IJwtTokenService
{
    int AccessTokenExpiresInSeconds { get; }

    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
    DateTimeOffset GetRefreshTokenExpiresAt();
}
