using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Auth.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<string>>;
