using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Users.Commands;

public sealed record DeleteUserCommand(Guid UserId) : IRequest<Result>;
