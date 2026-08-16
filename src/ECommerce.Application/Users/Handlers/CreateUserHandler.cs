using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Abstractions.Security;
using ECommerce.Application.Users.Commands;
using ECommerce.Application.Users.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using ECommerce.Shared.Results;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Users.Handlers;

public sealed class CreateUserHandler(IUserRepository _userRepository, IUnitOfWork _unitOfWork, IPasswordHasher _passwordHasher)
    : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        Result<Email> emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure) return Result<UserDto>.Failure([.. emailResult.Errors]);

        if (await _userRepository.ExistsByEmailAsync(emailResult.Value!.Value, cancellationToken: cancellationToken))
            return Result<UserDto>.Failure("E-mail is already registered.");

        string hashedPassword = _passwordHasher.HashPassword(command.Password);

        Result<User> userResult = User.Create(
            command.FirstName,
            command.LastName,
            emailResult.Value,
            hashedPassword,
            command.Role
        );

        if (userResult.IsFailure) return Result<UserDto>.Failure([.. userResult.Errors]);

        await _userRepository.AddAsync(userResult.Value!, cancellationToken);
        await _unitOfWork.Commit(cancellationToken);

        UserDto userDto = userResult.Value!.Adapt<UserDto>();

        return Result<UserDto>.Success(userDto);
    }
}
