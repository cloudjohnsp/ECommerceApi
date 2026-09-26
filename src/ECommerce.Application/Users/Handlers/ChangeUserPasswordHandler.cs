using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Users.Dtos;
using ECommerce.Shared.Results;
using Mapster;
using MediatR;

namespace ECommerce.Application.Users.Handlers;

public sealed class ChangeUserPasswordHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IUserAuditRepository auditRepository) : IRequestHandler<ChangeUserPasswordCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(ChangeUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await userRepository.GetByIdForUpdateAsync(request.UserId, cancellationToken);
            if (user is null)
                return Result<UserDto>.Failure("User not found.");

            if (!passwordHasher.VerifyPassword(request.CurrentPassword!, user.PasswordHash))
                return Result<UserDto>.Failure("Current password is invalid.");

            var changeResult = user.ChangePassword(passwordHasher.HashPassword(request.NewPassword!));
            if (changeResult.IsFailure)
            {
                return Result<UserDto>.Failure([.. changeResult.Errors]);
            }

            await userRepository.UpdateAsync(user, cancellationToken);
            await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
            await auditRepository.AddAsync(
                UserAuditEntryFactory.PasswordChanged(user, request.ActorUserId),
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
