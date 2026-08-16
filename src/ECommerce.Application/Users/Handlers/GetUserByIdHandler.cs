using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Users.Dtos;
using ECommerce.Application.Users.Queries;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using Mapster;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Users.Handlers;

public sealed class GetUserByIdHandler(IUserRepository _userRepository) : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        User? user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user is null
            ? Result<UserDto>.Failure("User not found.")
            : Result<UserDto>.Success(user.Adapt<UserDto>());
    }
}
