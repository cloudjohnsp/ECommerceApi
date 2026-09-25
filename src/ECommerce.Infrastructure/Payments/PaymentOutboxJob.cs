using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Options;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

public sealed class PaymentOutboxJob(
    IOutboxMessageRepository repository,
    IPaymentCreationProcessor processor,
    IOptions<OutboxProcessorOptions> options,
    ILogger<PaymentOutboxJob> logger)
{
    private readonly OutboxProcessorOptions _options = options.Value;

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var messageIds = await repository.GetPendingIdsAsync(
            OutBoxMessageType.PaymentCreationRequested,
            _options.BatchSize,
            cancellationToken);

        foreach (var messageId in messageIds)
        {
            var result = await processor.ProcessAsync(messageId, cancellationToken);
            if (result.IsFailure)
                logger.LogWarning(
                    "Payment outbox message {MessageId} remains pending: {Errors}",
                    messageId,
                    string.Join("; ", result.Errors));
        }
    }
}
