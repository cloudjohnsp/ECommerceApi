using ECommerce.Application.Orders.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders;

public sealed record GetOrderByIdQuery(Guid OrderId, Guid? CustomerId = null) : IRequest<Result<OrderDto>>;
public sealed record GetOrdersQuery(Guid? CustomerId = null) : IRequest<Result<IReadOnlyCollection<OrderDto>>>;
