using System.Text.Json;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Orders;

internal static class OrderIntegrationEventFactory
{
    public static OutboxMessage Create(Order order, OutBoxMessageType messageType)
    {
        var payload = new OrderIntegrationEventPayload(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.Total,
            DateTimeOffset.UtcNow,
            [.. order.Items.Select(item => new OrderIntegrationEventItem(
                item.ProductId,
                item.ProductName,
                item.UnitPrice,
                item.Quantity,
                item.Subtotal))]);
        return new OutboxMessage(messageType, JsonSerializer.Serialize(payload));
    }
}
