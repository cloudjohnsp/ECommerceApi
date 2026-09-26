using System.Text.Json;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;

namespace ECommerce.Application.Payments;

public sealed class PaymentCreationProcessor(
    IOutboxMessageRepository outboxMessageRepository,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IUnitOfWork unitOfWork) : IPaymentCreationProcessor
{
    public async Task<Result<Payment>> ProcessAsync(
        Guid outboxMessageId,
        CancellationToken cancellationToken = default)
    {
        var intention = await outboxMessageRepository.GetByIdAsync(outboxMessageId, cancellationToken);
        if (intention is null || intention.Type != OutBoxMessageType.PaymentCreationRequested)
            return Result<Payment>.Failure("Payment creation intention not found.");

        var payment = await paymentRepository.GetByIdAsync(outboxMessageId, cancellationToken);
        if (payment is null)
            return Result<Payment>.Failure("Payment not found.");
        if (intention.Status == OutBoxMessageStatus.Processed)
            return Result<Payment>.Success(payment);

        CreateGatewayPayment gatewayRequest;
        try
        {
            gatewayRequest = JsonSerializer.Deserialize<CreateGatewayPayment>(intention.Payload)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return Result<Payment>.Failure("Payment creation intention has an invalid payload.");
        }

        var externalPaymentId = payment.ExternalPaymentId;
        if (externalPaymentId is null)
        {
            var gatewayResult = await paymentGateway.CreateAsync(gatewayRequest, cancellationToken);
            if (gatewayResult.IsFailure)
                return Result<Payment>.Failure([.. gatewayResult.Errors]);

            var gatewayPayment = gatewayResult.Value!;
            if (!string.Equals(
                    gatewayPayment.Status?.Trim(),
                    "pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Result<Payment>.Failure(
                    "Payment gateway returned an invalid creation status.");
            }

            externalPaymentId = gatewayPayment.ExternalPaymentId;
        }

        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var currentPayment = await paymentRepository.GetByIdForUpdateAsync(
                outboxMessageId,
                cancellationToken);
            if (currentPayment is null)
                return Result<Payment>.Failure("Payment not found.");

            var currentIntention = await outboxMessageRepository.GetByIdForUpdateAsync(
                outboxMessageId,
                cancellationToken);
            if (currentIntention is null ||
                currentIntention.Type != OutBoxMessageType.PaymentCreationRequested)
            {
                return Result<Payment>.Failure("Payment creation intention not found.");
            }

            if (currentIntention.Status == OutBoxMessageStatus.Processed)
                return Result<Payment>.Success(currentPayment);

            var registerResult = currentPayment.RegisterExternalPayment(externalPaymentId);
            if (registerResult.IsFailure)
                return Result<Payment>.Failure([.. registerResult.Errors]);

            paymentRepository.Update(currentPayment);
            currentIntention.MarkProcessed();
            outboxMessageRepository.Update(currentIntention);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;

            return Result<Payment>.Success(currentPayment);
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
