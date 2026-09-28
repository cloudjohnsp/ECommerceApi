using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECommerce.Shared.Messaging;

public static class IntegrationEventContract
{
    public const string Version1 = "1";
}

public sealed record IntegrationEventEnvelope<TPayload>(
    [property: JsonPropertyName("messageId")] Guid MessageId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("payload")] TPayload Payload);

public sealed record PaymentWebhookPayload(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("amount")] string? Amount,
    [property: JsonPropertyName("currency")] string? Currency);

public static class IntegrationEventEnvelope
{
    public static IntegrationEventEnvelope<JsonElement> Create(
        Guid messageId,
        string eventType,
        DateTimeOffset occurredAt,
        string correlationId,
        string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new IntegrationEventEnvelope<JsonElement>(
            messageId,
            eventType,
            IntegrationEventContract.Version1,
            occurredAt,
            correlationId,
            document.RootElement.Clone());
    }
}
