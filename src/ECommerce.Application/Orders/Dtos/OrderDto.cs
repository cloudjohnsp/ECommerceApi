using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Orders.Dtos;

public sealed record OrderItemDto(Guid Id, Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? CancelledAt,
    IReadOnlyCollection<OrderItemDto> Items);

internal static class OrderMapping
{
    internal static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status,
        order.Total,
        order.CreatedAt,
        order.UpdatedAt,
        order.CancelledAt,
        [.. order.Items.Select(item => new OrderItemDto(
            item.Id, item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.Subtotal))]);
}
