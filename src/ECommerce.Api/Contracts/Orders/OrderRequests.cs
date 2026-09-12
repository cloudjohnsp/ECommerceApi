using ECommerce.Domain.Enums;

namespace ECommerce.Api.Contracts.Orders;

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);
public sealed record CreateOrderRequest(Guid CustomerId, IReadOnlyCollection<CreateOrderItemRequest> Items);
public sealed record UpdateOrderRequest(OrderStatus Status);
