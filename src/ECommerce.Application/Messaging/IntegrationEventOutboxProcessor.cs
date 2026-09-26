using ECommerce.Application.Abstractions.Messaging;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Application.Messaging;

public sealed class IntegrationEventOutboxProcessor(
    IOutboxMessageRepository outboxMessageRepository,
    IIntegrationEventPublisher publisher,
    IUnitOfWork unitOfWork) : IIntegrationEventOutboxProcessor
{
    public async Task<Result> ProcessAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default)
    {
        var message = await outboxMessageRepository.GetByIdAsync(outboxMessageId, cancellationToken);
        if (message is null)
            return Result.Failure("Outbox message not found.");
        if (message.Status == OutBoxMessageStatus.Processed)
            return Result.Success();

        var eventType = GetEventType(message.Type);
        if (eventType is null)
            return Result.Failure($"Outbox message type '{message.Type}' is not an integration event.");

        var publishResult = await publisher.PublishAsync(
            new IntegrationEvent(message.Id, eventType, message.Payload, message.CreatedAt),
            cancellationToken);
        if (publishResult.IsFailure)
            return publishResult;

        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            message.MarkProcessed();
            outboxMessageRepository.Update(message);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            return Result.Success();
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }

    private static string? GetEventType(OutBoxMessageType type) => type switch
    {
        OutBoxMessageType.OrderCreated => "order.created",
        OutBoxMessageType.OrderUpdated => "order.updated",
        OutBoxMessageType.OrderDeleted => "order.deleted",
        OutBoxMessageType.OrderPaid => "order.paid",
        OutBoxMessageType.PaymentFailed => "payment.failed",
        OutBoxMessageType.OrderRefunded => "order.refunded",
        OutBoxMessageType.OrderCancelled => "order.cancelled",
        OutBoxMessageType.StockUpdated => "stock.updated",
        _ => null
    };
}
