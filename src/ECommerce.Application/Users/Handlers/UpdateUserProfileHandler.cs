using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users.Dtos;
using ECommerce.Domain.ValueObjects;
using ECommerce.Shared.Results;
using Mapster;
using MediatR;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Auth;
using ECommerce.Domain.Enums;
using EmailValue = ECommerce.Domain.ValueObjects.Email;

namespace ECommerce.Application.Users.Handlers;

public sealed class UpdateUserProfileHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IUserActionTokenRepository tokenRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUserActionTokenService tokenService) : IRequestHandler<UpdateUserProfileCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        var emailResult = EmailValue.Create(request.Email ?? user.Email.Value);
        if (emailResult.IsFailure)
        {
            return Result<UserDto>.Failure([.. emailResult.Errors]);
        }

        if (await userRepository.ExistsByEmailAsync(emailResult.Value!.Value, user.Id, cancellationToken))
        {
            return Result<UserDto>.Failure("E-mail is already registered.");
        }

        var emailChanged = user.Email.Value != emailResult.Value.Value;
        var updateResult = user.UpdateProfile(
            request.FirstName ?? user.FirstName,
            request.LastName ?? user.LastName,
            emailResult.Value);

        if (updateResult.IsFailure)
        {
            return Result<UserDto>.Failure([.. updateResult.Errors]);
        }

        if (emailChanged)
        {
            await tokenRepository.ConsumeActiveForUserAsync(
                user.Id,
                UserActionTokenType.EmailConfirmation,
                DateTimeOffset.UtcNow,
                cancellationToken);
            var pendingTokenResult = UserActionTokenFactory.Create(
                user,
                UserActionTokenType.EmailConfirmation,
                tokenService);
            if (pendingTokenResult.IsFailure)
                return Result<UserDto>.Failure([.. pendingTokenResult.Errors]);

            await tokenRepository.AddAsync(pendingTokenResult.Value!.Token, cancellationToken);
            await outboxMessageRepository.AddAsync(
                pendingTokenResult.Value.OutboxMessage,
                cancellationToken);
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.Commit(cancellationToken);

        return Result<UserDto>.Success(user.Adapt<UserDto>());
    }
}
