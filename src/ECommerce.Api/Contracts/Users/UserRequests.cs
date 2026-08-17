using ECommerce.Domain.Enums;

namespace ECommerce.Api.Contracts.Users;

public sealed record UpdateUserProfileRequest(string? FirstName, string? LastName, string? Email);
public sealed record ChangePasswordRequest(string? Password);
public sealed record ChangeRoleRequest(UserRole Role);
