using ECommerce.Domain.Enums;

namespace ECommerce.Api.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role = UserRole.Customer
);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn
);

public sealed record RegisterResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role
);
