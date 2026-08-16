using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth.Commands;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Auth.Handlers;

public sealed class LoginHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : IRequestHandler<LoginCommand, Result<string>>
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<Result<string>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result<string>.Failure(InvalidCredentialsMessage);
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result<string>.Failure(InvalidCredentialsMessage);
        }

        string token = jwtTokenService.GenerateToken(user);
        return Result<string>.Success(token);
    }
}
