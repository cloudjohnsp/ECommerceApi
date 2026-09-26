using System.Text.Json;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Products;

internal static class StockIntegrationEventFactory
{
    public static OutboxMessage Create(Product product, string reason, Guid? orderId = null)
    {
        var payload = new StockUpdatedIntegrationEventPayload(
            product.Id,
            product.AvailableStock,
            orderId,
            reason,
            DateTimeOffset.UtcNow);
        return new OutboxMessage(
            OutBoxMessageType.StockUpdated,
            JsonSerializer.Serialize(payload));
    }
}
