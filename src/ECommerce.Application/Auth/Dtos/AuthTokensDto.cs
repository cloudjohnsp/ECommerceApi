namespace ECommerce.Application.Auth.Dtos;

public sealed record AuthTokensDto(string AccessToken, string RefreshToken, int ExpiresIn);
