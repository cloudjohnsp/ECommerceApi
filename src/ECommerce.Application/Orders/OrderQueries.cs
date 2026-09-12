using ECommerce.Application.Orders.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderDto>>;
public sealed record GetOrdersQuery : IRequest<Result<IReadOnlyCollection<OrderDto>>>;
