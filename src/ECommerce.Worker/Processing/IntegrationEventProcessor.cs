namespace ECommerce.Worker.Processing;

public sealed class IntegrationEventProcessor(
    OrderIntegrationEventProcessor orderProcessor,
    StockIntegrationEventProcessor stockProcessor)
{
    public Task<IntegrationEventProcessingResult> ProcessAsync(
        Guid messageId,
        string eventType,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default) =>
        string.Equals(eventType, StockIntegrationEventProcessor.EventType, StringComparison.Ordinal)
            ? stockProcessor.ProcessAsync(messageId, eventType, body, cancellationToken)
            : orderProcessor.ProcessAsync(messageId, eventType, body, cancellationToken);
}
