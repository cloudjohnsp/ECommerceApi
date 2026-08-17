using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth.Commands;
using ECommerce.Application.Auth.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Auth.Handlers;

public sealed class RefreshTokenHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IJwtTokenService jwtTokenService) : IRequestHandler<RefreshTokenCommand, Result<AuthTokensDto>>
{
    private const string InvalidRefreshTokenMessage = "Invalid refresh token.";

    public async Task<Result<AuthTokensDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        string tokenHash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (storedToken is null)
        {
            return Result<AuthTokensDto>.Failure(InvalidRefreshTokenMessage);
        }

        if (storedToken.RevokedAt is not null)
        {
            await refreshTokenRepository.RevokeAllForUserAsync(storedToken.UserId, cancellationToken);
            await unitOfWork.Commit(cancellationToken);
            return Result<AuthTokensDto>.Failure(InvalidRefreshTokenMessage);
        }

        if (storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return Result<AuthTokensDto>.Failure(InvalidRefreshTokenMessage);
        }

        var user = await userRepository.GetByIdAsync(storedToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<AuthTokensDto>.Failure(InvalidRefreshTokenMessage);
        }

        var (Dto, RefreshTokenEntity) = AuthTokenFactory.Issue(user, jwtTokenService);
        storedToken.Revoke(RefreshTokenEntity.Id);
        await refreshTokenRepository.AddAsync(RefreshTokenEntity, cancellationToken);
        await unitOfWork.Commit(cancellationToken);

        return Result<AuthTokensDto>.Success(Dto);
    }
}
