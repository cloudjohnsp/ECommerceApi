using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Payments.Handlers;

public sealed class RefundPaymentHandler(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    IProductRepository productRepository,
    IPaymentGateway paymentGateway,
    IUnitOfWork unitOfWork,
    IProductCache productCache) : IRequestHandler<RefundPaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        RefundPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null || request.CustomerId is { } customerId && order.CustomerId != customerId)
            return Result<PaymentDto>.Failure("Payment not found.");

        var payment = await paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (payment is null)
            return Result<PaymentDto>.Failure("Payment not found.");

        var currentState = ValidateState(payment, order);
        if (currentState.IsFailure)
            return Result<PaymentDto>.Failure([.. currentState.Errors]);
        if (payment.Status == PaymentStatus.Refunded)
            return Result<PaymentDto>.Success(payment.ToDto());
        if (string.IsNullOrWhiteSpace(payment.ExternalPaymentId))
            return Result<PaymentDto>.Failure("Payment has no external identifier.");

        var refundResult = await paymentGateway.RefundAsync(
            new RefundGatewayPayment(
                payment.Id,
                payment.ExternalPaymentId,
                string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()),
            cancellationToken);
        if (refundResult.IsFailure)
            return Result<PaymentDto>.Failure([.. refundResult.Errors]);
        if (!string.Equals(refundResult.Value!.Status, "refunded", StringComparison.OrdinalIgnoreCase))
            return Result<PaymentDto>.Failure("Payment gateway returned an invalid refund status.");

        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            payment = await paymentRepository.GetByOrderIdForUpdateAsync(request.OrderId, cancellationToken);
            order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (payment is null || order is null ||
                request.CustomerId is { } scopedCustomerId && order.CustomerId != scopedCustomerId)
                return Result<PaymentDto>.Failure("Payment not found.");

            currentState = ValidateState(payment, order);
            if (currentState.IsFailure)
                return Result<PaymentDto>.Failure([.. currentState.Errors]);
            if (payment.Status == PaymentStatus.Refunded)
                return Result<PaymentDto>.Success(payment.ToDto());

            var transitionResult = payment.MarkAsRefunded();
            if (transitionResult.IsFailure)
                return Result<PaymentDto>.Failure([.. transitionResult.Errors]);
            transitionResult = order.MarkAsRefunded();
            if (transitionResult.IsFailure)
                return Result<PaymentDto>.Failure([.. transitionResult.Errors]);

            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                var product = await productRepository.GetByIdForUpdateAsync(
                    item.ProductId,
                    cancellationToken);
                if (product is null)
                    return Result<PaymentDto>.Failure(
                        $"Product '{item.ProductId}' not found while restoring stock.");

                var restoreResult = product.RestoreStock(item.Quantity);
                if (restoreResult.IsFailure)
                    return Result<PaymentDto>.Failure([.. restoreResult.Errors]);
                productRepository.Update(product);
            }

            paymentRepository.Update(payment);
            orderRepository.Update(order);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }

        await Task.WhenAll(order.Items.Select(item =>
            productCache.RemoveAsync(item.ProductId, CancellationToken.None)));
        return Result<PaymentDto>.Success(payment.ToDto());
    }

    private static Result ValidateState(Payment payment, Order order)
    {
        if (payment.Status == PaymentStatus.Refunded && order.Status == OrderStatus.Refunded)
            return Result.Success();
        if (payment.Status == PaymentStatus.Refunded || order.Status == OrderStatus.Refunded)
            return Result.Failure("Payment and order refund states are inconsistent.");
        if (payment.Status != PaymentStatus.Paid)
            return Result.Failure("Only a paid payment can be refunded.");
        if (order.Status != OrderStatus.Paid)
            return Result.Failure("Only a paid order can be refunded.");
        return Result.Success();
    }
}
