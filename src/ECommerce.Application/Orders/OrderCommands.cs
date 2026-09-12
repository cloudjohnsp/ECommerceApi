using ECommerce.Application.Orders.Dtos;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders;

public sealed record CreateOrderItem(Guid ProductId, int Quantity);
public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyCollection<CreateOrderItem> Items) : IRequest<Result<OrderDto>>;
public sealed record UpdateOrderCommand(Guid OrderId, OrderStatus Status) : IRequest<Result<OrderDto>>;
public sealed record DeleteOrderCommand(Guid OrderId) : IRequest<Result>;
