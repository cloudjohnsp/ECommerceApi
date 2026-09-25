namespace ECommerce.Shared.Messaging;

public sealed record OrderIntegrationEventItem(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public sealed record OrderIntegrationEventPayload(
    Guid OrderId,
    Guid CustomerId,
    string Status,
    decimal Total,
    DateTimeOffset OccurredAt,
    IReadOnlyCollection<OrderIntegrationEventItem> Items);
