using ECommerce.Application.Users.Dtos;
using ECommerce.Shared.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECommerce.Application.Users.Queries;

public sealed record GetUserByIdQuery(
    Guid UserId
) : IRequest<Result<UserDto>>;
