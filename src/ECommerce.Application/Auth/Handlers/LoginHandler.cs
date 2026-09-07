using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Auth.Handlers;

public sealed class LoginHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : IRequestHandler<LoginCommand, Result<AuthTokensDto>>
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<Result<AuthTokensDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<AuthTokensDto>.Failure(InvalidCredentialsMessage);
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result<AuthTokensDto>.Failure(InvalidCredentialsMessage);
        }

        var (Dto, RefreshTokenEntity) = AuthTokenFactory.Issue(user, jwtTokenService);
        await refreshTokenRepository.AddAsync(RefreshTokenEntity, cancellationToken);
        await unitOfWork.Commit(cancellationToken);

        return Result<AuthTokensDto>.Success(Dto);
    }
}
