using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Users.Handlers;

public sealed class DeleteUserHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    IUserAuditRepository auditRepository) : IRequestHandler<DeleteUserCommand, Result>
{
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await userRepository.GetByIdForUpdateAsync(request.UserId, cancellationToken);
            if (user is null)
                return Result.Failure("User not found.");

            var deactivateResult = user.Deactivate();
            if (deactivateResult.IsFailure)
            {
                return deactivateResult;
            }

            await userRepository.UpdateAsync(user, cancellationToken);
            await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
            await auditRepository.AddAsync(
                UserAuditEntryFactory.Deactivated(user, request.ActorUserId),
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
