using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Users;

public sealed record UpdateUserProfileCommand(Guid UserId, string? FirstName, string? LastName, string? Email);
public sealed record ChangeUserPasswordCommand(Guid UserId, string? Password);
public sealed record ChangeUserRoleCommand(Guid UserId, UserRole Role);
