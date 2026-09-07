using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users.Dtos;
using ECommerce.Shared.Results;
using Mapster;
using MediatR;

namespace ECommerce.Application.Users.Handlers;

public sealed class ChangeUserRoleHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangeUserRoleCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result<UserDto>.Failure("User not found.");
        }

        var changeResult = user.ChangeRole(request.Role);
        if (changeResult.IsFailure)
        {
            return Result<UserDto>.Failure([.. changeResult.Errors]);
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.Commit(cancellationToken);

        return Result<UserDto>.Success(user.Adapt<UserDto>());
    }
}
