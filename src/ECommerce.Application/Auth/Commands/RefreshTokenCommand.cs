using ECommerce.Application.Auth.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokensDto>>;
