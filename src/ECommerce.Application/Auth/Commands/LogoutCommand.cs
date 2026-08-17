using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Auth.Commands;

public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;
