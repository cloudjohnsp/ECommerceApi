using ECommerce.Domain.Enums;
using ECommerce.Application.Users.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Users;

public sealed record UpdateUserProfileCommand(Guid UserId, string? FirstName, string? LastName, string? Email) : IRequest<Result<UserDto>>;
public sealed record ChangeUserPasswordCommand(Guid UserId, string? Password) : IRequest<Result<UserDto>>;
public sealed record ChangeUserRoleCommand(Guid UserId, UserRole Role) : IRequest<Result<UserDto>>;
