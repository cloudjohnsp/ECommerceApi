namespace ECommerce.Api.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn);
