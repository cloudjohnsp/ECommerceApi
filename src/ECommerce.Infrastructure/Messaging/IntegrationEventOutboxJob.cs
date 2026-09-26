using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Options;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Messaging;

public sealed class IntegrationEventOutboxJob(
    IOutboxMessageRepository repository,
    IIntegrationEventOutboxProcessor processor,
    IOptions<OutboxProcessorOptions> options,
    ILogger<IntegrationEventOutboxJob> logger)
{
    private static readonly OutBoxMessageType[] IntegrationEventTypes =
    [
        OutBoxMessageType.OrderCreated,
        OutBoxMessageType.OrderUpdated,
        OutBoxMessageType.OrderDeleted,
        OutBoxMessageType.OrderPaid,
        OutBoxMessageType.PaymentFailed,
        OutBoxMessageType.OrderRefunded,
        OutBoxMessageType.OrderCancelled,
        OutBoxMessageType.StockUpdated,
        OutBoxMessageType.EmailSent
    ];

    private readonly OutboxProcessorOptions _options = options.Value;

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var messageIds = await repository.GetPendingIdsAsync(
            IntegrationEventTypes,
            _options.BatchSize,
            cancellationToken);
        foreach (var messageId in messageIds)
        {
            var result = await processor.ProcessAsync(messageId, cancellationToken);
            if (result.IsFailure)
            {
                logger.LogWarning(
                    "Integration outbox message {MessageId} remains pending: {Errors}",
                    messageId,
                    string.Join("; ", result.Errors));
                break;
            }
        }
    }
}
