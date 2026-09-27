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
    IRefreshTokenRepository refreshTokenRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUserActionTokenService tokenService,
    IUserAuditRepository auditRepository) : IRequestHandler<UpdateUserProfileCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await userRepository.GetByIdForUpdateAsync(request.UserId, cancellationToken);
            if (user is null)
                return Result<UserDto>.Failure("User not found.");

            var emailResult = EmailValue.Create(request.Email ?? user.Email.Value);
            if (emailResult.IsFailure)
            {
                return Result<UserDto>.Failure([.. emailResult.Errors]);
            }

            var previousEmail = user.Email.Value;
            var emailChanged = previousEmail != emailResult.Value!.Value;
            if (emailChanged)
            {
                await userRepository.AcquireEmailLockAsync(
                    emailResult.Value.Value,
                    cancellationToken);
                if (await userRepository.ExistsByEmailAsync(
                        emailResult.Value.Value,
                        user.Id,
                        cancellationToken))
                {
                    return Result<UserDto>.Failure("E-mail is already registered.");
                }
            }

            var previousFirstName = user.FirstName;
            var previousLastName = user.LastName;
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
                var now = DateTimeOffset.UtcNow;
                await tokenRepository.ConsumeActiveForUserAsync(
                    user.Id,
                    UserActionTokenType.EmailConfirmation,
                    now,
                    cancellationToken);
                await tokenRepository.ConsumeActiveForUserAsync(
                    user.Id,
                    UserActionTokenType.PasswordReset,
                    now,
                    cancellationToken);
                await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
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
            await auditRepository.AddAsync(
                UserAuditEntryFactory.ProfileUpdated(
                    user,
                    request.ActorUserId,
                    previousFirstName,
                    previousLastName,
                    previousEmail),
                cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;

            return Result<UserDto>.Success(user.Adapt<UserDto>());
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
