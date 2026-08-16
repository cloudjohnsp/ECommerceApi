using ECommerce.Domain.Enums;

namespace ECommerce.Api.Contracts.Users;

public sealed record CreateUserRequest(string FirstName, string LastName, string Email, string Password, UserRole Role = UserRole.Customer);
public sealed record UpdateUserProfileRequest(string? FirstName, string? LastName, string? Email);
public sealed record ChangePasswordRequest(string? Password);
public sealed record ChangeRoleRequest(UserRole Role);
