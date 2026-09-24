using ECommerce.Shared.Results;

namespace ECommerce.Application.Abstractions.Messaging;

public interface IIntegrationEventOutboxProcessor
{
    Task<Result> ProcessAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);
}
