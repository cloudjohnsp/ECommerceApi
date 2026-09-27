using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;
using System.Text.Json;

namespace ECommerce.Application.Payments.Handlers;

public sealed class CreatePaymentHandler(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IPaymentCreationProcessor paymentCreationProcessor,
    IUnitOfWork unitOfWork) : IRequestHandler<CreatePaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        CreatePaymentCommand request,
        CancellationToken cancellationToken)
    {
        Payment payment;
        OutboxMessage? intention = null;
        var currency = request.Currency.Trim().ToUpperInvariant();
        var idempotencyKey = request.IdempotencyKey.Trim();
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (order is null || request.CustomerId is { } customerId && order.CustomerId != customerId)
                return Result<PaymentDto>.Failure("Order not found.");
            var existingPayment = await paymentRepository.GetByOrderAndIdempotencyKeyAsync(
                request.OrderId,
                idempotencyKey,
                cancellationToken);
            if (existingPayment is null)
            {
                if (order.Status == OrderStatus.Pending && order.ExpiresAt <= DateTimeOffset.UtcNow)
                    return Result<PaymentDto>.Failure("Order has expired.");
                if (order.Status != OrderStatus.Pending)
                    return Result<PaymentDto>.Failure("Only pending orders can be sent for payment.");
                var pendingPayment = await paymentRepository.GetPendingByOrderIdForUpdateAsync(
                    request.OrderId,
                    cancellationToken);
                if (pendingPayment is not null)
                    return Result<PaymentDto>.Failure(
                        "A payment attempt is already pending for this order.");

                var paymentResult = Payment.Create(
                    order.Id,
                    order.Total,
                    currency,
                    "ECommercePayment",
                    idempotencyKey);
                if (paymentResult.IsFailure)
                    return Result<PaymentDto>.Failure([.. paymentResult.Errors]);

                payment = paymentResult.Value!;
                await paymentRepository.AddAsync(payment, cancellationToken);
            }
            else
            {
                payment = existingPayment;
                if (!string.Equals(payment.Currency, currency, StringComparison.Ordinal))
                {
                    return Result<PaymentDto>.Failure(
                        "Payment currency does not match the existing payment.");
                }
            }

            if (payment.ExternalPaymentId is null)
            {
                intention = await outboxMessageRepository.GetByIdAsync(payment.Id, cancellationToken);
                if (intention is null)
                {
                    var gatewayPayment = new CreateGatewayPayment(
                        payment.Id,
                        payment.OrderId,
                        payment.Amount,
                        payment.Currency);
                    intention = new OutboxMessage(
                        payment.Id,
                        OutBoxMessageType.PaymentCreationRequested,
                        JsonSerializer.Serialize(gatewayPayment));
                    await outboxMessageRepository.AddAsync(intention, cancellationToken);
                }
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }

        if (payment.ExternalPaymentId is not null)
            return Result<PaymentDto>.Success(payment.ToDto());

        var processResult = await paymentCreationProcessor.ProcessAsync(payment.Id, cancellationToken);
        return processResult.IsFailure
            ? Result<PaymentDto>.Failure([.. processResult.Errors])
            : Result<PaymentDto>.Success(processResult.Value!.ToDto());
    }
}
