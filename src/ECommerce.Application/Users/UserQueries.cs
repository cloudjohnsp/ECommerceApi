using ECommerce.Application.Users.Dtos;
using ECommerce.Shared.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using ECommerce.Shared.Pagination;

namespace ECommerce.Application.Users;

public sealed record GetUserByIdQuery(
    Guid UserId
) : IRequest<Result<UserDto>>;

public sealed record GetUserAuditHistoryQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<UserAuditEntryDto>>>;
