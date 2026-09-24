using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

public sealed class PaymentOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxProcessorOptions> options,
    ILogger<PaymentOutboxWorker> logger) : BackgroundService
{
    private readonly OutboxProcessorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

        do
        {
            await ProcessPendingMessagesAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
            var processor = scope.ServiceProvider.GetRequiredService<IPaymentCreationProcessor>();
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process payment outbox messages.");
        }
    }
}
