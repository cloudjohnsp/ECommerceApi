using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth.Commands;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Auth.Handlers;

public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IJwtTokenService jwtTokenService) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        string tokenHash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (storedToken is not null && storedToken.IsUsable())
        {
            storedToken.Revoke();
            await unitOfWork.Commit(cancellationToken);
        }

        return Result.Success();
    }
}
