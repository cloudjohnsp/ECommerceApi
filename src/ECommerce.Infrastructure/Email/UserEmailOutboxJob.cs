using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Options;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Email;

public sealed class UserEmailOutboxJob(
    IOutboxMessageRepository repository,
    IUserEmailOutboxProcessor processor,
    IOptions<OutboxProcessorOptions> options,
    ILogger<UserEmailOutboxJob> logger)
{
    private static readonly OutBoxMessageType[] EmailMessageTypes =
    [
        OutBoxMessageType.EmailConfirmationRequested,
        OutBoxMessageType.PasswordResetRequested
    ];

    private readonly OutboxProcessorOptions _options = options.Value;

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
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
}
