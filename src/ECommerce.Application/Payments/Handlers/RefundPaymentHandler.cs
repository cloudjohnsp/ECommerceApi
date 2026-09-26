using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Application.Orders;
using ECommerce.Application.Products;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Payments.Handlers;

public sealed class RefundPaymentHandler(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    IProductRepository productRepository,
    IOutboxMessageRepository outboxMessageRepository,
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

        var gatewayPayment = refundResult.Value!;
        if (!string.Equals(
                gatewayPayment.ExternalPaymentId,
                payment.ExternalPaymentId,
                StringComparison.Ordinal))
        {
            return Result<PaymentDto>.Failure(
                "Payment gateway returned a mismatched payment identifier.");
        }

        if (!string.Equals(
                gatewayPayment.Status?.Trim(),
                "refunded",
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<PaymentDto>.Failure("Payment gateway returned an invalid refund status.");
        }

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

            var updatedProducts = new List<Product>();
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
                updatedProducts.Add(product);
            }

            paymentRepository.Update(payment);
            orderRepository.Update(order);
            await outboxMessageRepository.AddAsync(
                OrderIntegrationEventFactory.Create(order, OutBoxMessageType.OrderRefunded),
                cancellationToken);
            foreach (var product in updatedProducts)
            {
                await outboxMessageRepository.AddAsync(
                    StockIntegrationEventFactory.Create(
                        product,
                        StockUpdateReasons.Restored,
                        order.Id),
                    cancellationToken);
            }
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
