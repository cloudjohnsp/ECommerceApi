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

        if (payment.ExternalPaymentId is null)
        {
            var gatewayResult = await paymentGateway.CreateAsync(gatewayRequest, cancellationToken);
            if (gatewayResult.IsFailure)
                return Result<Payment>.Failure([.. gatewayResult.Errors]);

            var registerResult = payment.RegisterExternalPayment(gatewayResult.Value!.ExternalPaymentId);
            if (registerResult.IsFailure)
                return Result<Payment>.Failure([.. registerResult.Errors]);
        }

        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            paymentRepository.Update(payment);
            intention.MarkProcessed();
            outboxMessageRepository.Update(intention);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }

        return Result<Payment>.Success(payment);
    }
}
