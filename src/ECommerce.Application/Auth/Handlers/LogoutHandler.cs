using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
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
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var storedToken = await refreshTokenRepository.GetByTokenHashForUpdateAsync(
                tokenHash,
                cancellationToken);
            if (storedToken is null)
                return Result.Success();

            await refreshTokenRepository.RevokeAllForUserAsync(
                storedToken.UserId,
                cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;

            return Result.Success();
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
