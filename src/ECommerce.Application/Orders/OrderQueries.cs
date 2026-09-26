using ECommerce.Application.Orders.Dtos;
using ECommerce.Shared.Results;
using ECommerce.Shared.Pagination;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Orders;

public sealed record GetOrderByIdQuery(Guid OrderId, Guid? CustomerId = null) : IRequest<Result<OrderDto>>;
public sealed record GetOrdersQuery(Guid? CustomerId = null) : IRequest<Result<IReadOnlyCollection<OrderDto>>>;

public sealed record SearchOrdersQuery(
    Guid? CustomerId = null,
    OrderStatus? Status = null,
    DateTimeOffset? CreatedFromUtc = null,
    DateTimeOffset? CreatedToUtc = null,
    string SortBy = "createdAt",
    bool Descending = true,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<OrderDto>>>;
