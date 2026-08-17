using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth.Dtos;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Auth;

internal static class AuthTokenFactory
{
    public static (AuthTokensDto Dto, RefreshToken RefreshTokenEntity) Issue(
        User user,
        IJwtTokenService jwtTokenService)
    {
        string accessToken = jwtTokenService.GenerateAccessToken(user);
        string refreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshTokenEntity = RefreshToken.Create(
            user.Id,
            jwtTokenService.HashRefreshToken(refreshToken),
            jwtTokenService.GetRefreshTokenExpiresAt());

        return (new AuthTokensDto(accessToken, refreshToken, jwtTokenService.AccessTokenExpiresInSeconds), refreshTokenEntity);
    }
}
