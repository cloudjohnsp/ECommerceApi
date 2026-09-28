using ECommerce.Shared.Results;

namespace ECommerce.Application.Abstractions.Messaging;

public sealed record IntegrationEvent(
    Guid Id,
    string Type,
    string Payload,
    DateTime OccurredAt,
    string CorrelationId = "");

public interface IIntegrationEventPublisher
{
    Task<Result> PublishAsync(
        IntegrationEvent integrationEvent,
        CancellationToken cancellationToken = default);
}
