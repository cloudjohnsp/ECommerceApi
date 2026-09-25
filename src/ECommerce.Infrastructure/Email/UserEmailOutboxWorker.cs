using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Email;

public sealed class UserEmailOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxProcessorOptions> options,
    ILogger<UserEmailOutboxWorker> logger) : BackgroundService
{
    private static readonly OutBoxMessageType[] EmailMessageTypes =
    [
        OutBoxMessageType.EmailConfirmationRequested,
        OutBoxMessageType.PasswordResetRequested
    ];

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
            var processor = scope.ServiceProvider.GetRequiredService<IUserEmailOutboxProcessor>();
            foreach (var messageType in EmailMessageTypes)
            {
                var messageIds = await repository.GetPendingIdsAsync(
                    messageType,
                    _options.BatchSize,
                    cancellationToken);
                foreach (var messageId in messageIds)
                {
                    var result = await processor.ProcessAsync(messageId, cancellationToken);
                    if (result.IsFailure)
                    {
                        logger.LogWarning(
                            "User e-mail outbox message {MessageId} remains pending: {Errors}",
                            messageId,
                            string.Join("; ", result.Errors));
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process user e-mail outbox messages.");
        }
    }
}
