using System.Text.Json;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Application.Email;

public sealed class UserEmailOutboxProcessor(
    IOutboxMessageRepository outboxMessageRepository,
    IUserEmailSender emailSender,
    IUnitOfWork unitOfWork) : IUserEmailOutboxProcessor
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
        if (message.Type is not OutBoxMessageType.EmailConfirmationRequested and
            not OutBoxMessageType.PasswordResetRequested)
        {
            return Result.Failure($"Outbox message type '{message.Type}' is not a user email.");
        }

        UserEmailDelivery? delivery;
        try
        {
            delivery = JsonSerializer.Deserialize<UserEmailDelivery>(message.Payload);
        }
        catch (JsonException)
        {
            return Result.Failure("User email outbox message has an invalid payload.");
        }

        if (delivery is null || !Matches(message.Type, delivery.Type))
            return Result.Failure("User email outbox message has an invalid payload.");

        var sendResult = await emailSender.SendAsync(delivery, cancellationToken);
        if (sendResult.IsFailure)
            return sendResult;

        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            message.MarkProcessed(clearPayload: true);
            outboxMessageRepository.Update(message);
            await outboxMessageRepository.AddAsync(
                EmailIntegrationEventFactory.Create(message.Id, delivery.Type),
                cancellationToken);
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

    private static bool Matches(
        OutBoxMessageType messageType,
        UserEmailDeliveryType deliveryType) =>
        (messageType == OutBoxMessageType.EmailConfirmationRequested &&
            deliveryType == UserEmailDeliveryType.EmailConfirmation) ||
        (messageType == OutBoxMessageType.PasswordResetRequested &&
            deliveryType == UserEmailDeliveryType.PasswordReset);
}
