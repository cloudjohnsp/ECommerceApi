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
    IPaymentGateway paymentGateway,
    IUnitOfWork unitOfWork) : IRequestHandler<CreatePaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        CreatePaymentCommand request,
        CancellationToken cancellationToken)
    {
        Payment payment;
        OutboxMessage? intention = null;
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (order is null) return Result<PaymentDto>.Failure("Order not found.");
            if (order.Status != OrderStatus.Pending)
                return Result<PaymentDto>.Failure("Only pending orders can be sent for payment.");

            var existingPayment = await paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
            if (existingPayment is null)
            {
                var paymentResult = Payment.Create(order.Id, order.Total, "ECommercePayment");
                if (paymentResult.IsFailure)
                    return Result<PaymentDto>.Failure([.. paymentResult.Errors]);

                payment = paymentResult.Value!;
                await paymentRepository.AddAsync(payment, cancellationToken);
            }
            else
            {
                payment = existingPayment;
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
                        request.Currency.ToUpperInvariant());
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

        var gatewayPaymentRequest = JsonSerializer.Deserialize<CreateGatewayPayment>(intention!.Payload)!;
        var gatewayResult = await paymentGateway.CreateAsync(gatewayPaymentRequest, cancellationToken);

        if (gatewayResult.IsFailure)
            return Result<PaymentDto>.Failure([.. gatewayResult.Errors]);

        var registerResult = payment.RegisterExternalPayment(gatewayResult.Value!.ExternalPaymentId);
        if (registerResult.IsFailure)
            return Result<PaymentDto>.Failure([.. registerResult.Errors]);

        paymentRepository.Update(payment);
        intention.MarkProcessed();
        outboxMessageRepository.Update(intention);
        await unitOfWork.Commit(cancellationToken);
        return Result<PaymentDto>.Success(payment.ToDto());
    }
}
