using ECommerce.Domain.Enums;
using ECommerce.Application.Users.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Users;

public sealed record CreateUserCommand(string FirstName, string LastName, string Email, string Password, UserRole Role = UserRole.Customer) : IRequest<Result<UserDto>>;

public sealed record UpdateUserProfileCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? Email,
    Guid? ActorUserId = null) : IRequest<Result<UserDto>>;
public sealed record ChangeUserPasswordCommand(
    Guid UserId,
    string? CurrentPassword,
    string? NewPassword,
    Guid? ActorUserId = null) : IRequest<Result<UserDto>>;
public sealed record ChangeUserRoleCommand(
    Guid UserId,
    UserRole Role,
    Guid? ActorUserId = null) : IRequest<Result<UserDto>>;
public sealed record DeleteUserCommand(
    Guid UserId,
    Guid? ActorUserId = null) : IRequest<Result>;
