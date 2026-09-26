using System.Text.Json;
using System.Diagnostics;
using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Application.Orders;
using ECommerce.Application.Products;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Payments.Handlers;

public sealed class ProcessPaymentWebhookHandler(
    IPaymentWebhookSignatureVerifier signatureVerifier,
    IPaymentRepository paymentRepository,
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache) : IRequestHandler<ProcessPaymentWebhookCommand, Result>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result> Handle(
        ProcessPaymentWebhookCommand request,
        CancellationToken cancellationToken)
    {
        if (!signatureVerifier.IsValid(request.Payload, request.Signature))
            return Result.Failure("Invalid payment webhook signature.");

        PaymentWebhook? webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<PaymentWebhook>(request.Payload, SerializerOptions);
        }
        catch (JsonException)
        {
            return Result.Failure("Invalid payment webhook payload.");
        }

        if (webhook?.Data is null || string.IsNullOrWhiteSpace(webhook.Data.Id))
            return Result.Failure("Invalid payment webhook payload.");

        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await paymentRepository.GetByExternalIdForUpdateAsync(
                webhook.Data.Id,
                cancellationToken);
            if (payment is null) return Result.Failure("Payment not found.");

            if (webhook.Event == "payment.approved" && payment.Status == PaymentStatus.Paid)
                return Result.Success();
            if (webhook.Event == "payment.declined" && payment.Status == PaymentStatus.Failed)
                return Result.Success();
            if (webhook.Event == "payment.refunded" && payment.Status == PaymentStatus.Refunded)
                return Result.Success();

            var order = await orderRepository.GetByIdForUpdateAsync(payment.OrderId, cancellationToken);
            if (order is null) return Result.Failure("Order not found.");

            Result transitionResult;
            var updatedProducts = new List<(Product Product, string Reason)>();
            switch (webhook.Event)
            {
                case "payment.approved":
                    transitionResult = payment.MarkAsPaid(webhook.Data.Id);
                    if (transitionResult.IsFailure) return transitionResult;

                    transitionResult = order.MarkAsPaid();
                    if (transitionResult.IsFailure) return transitionResult;
                    foreach (var item in order.Items.OrderBy(item => item.ProductId))
                    {
                        var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                        if (product is null)
                            return Result.Failure($"Product '{item.ProductId}' not found while reducing stock.");
                        var reduceResult = product.ReduceStock(item.Quantity);
                        if (reduceResult.IsFailure) return reduceResult;
                        productRepository.Update(product);
                        updatedProducts.Add((product, StockUpdateReasons.ReservationConsumed));
                    }
                    break;

                case "payment.declined":
                    transitionResult = payment.MarkAsFailed();
                    if (transitionResult.IsFailure) return transitionResult;

                    if (order.Status != OrderStatus.Cancelled)
                    {
                        transitionResult = order.Cancel();
                        if (transitionResult.IsFailure) return transitionResult;

                        foreach (var item in order.Items.OrderBy(item => item.ProductId))
                        {
                            var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                            if (product is null)
                                return Result.Failure($"Product '{item.ProductId}' not found while restoring stock.");

                            var releaseResult = product.ReleaseReservedStock(item.Quantity);
                            if (releaseResult.IsFailure) return releaseResult;
                            productRepository.Update(product);
                            updatedProducts.Add((product, StockUpdateReasons.ReservationReleased));
                        }
                    }
                    break;

                case "payment.refunded":
                    transitionResult = payment.MarkAsRefunded();
                    if (transitionResult.IsFailure) return transitionResult;

                    transitionResult = order.MarkAsRefunded();
                    if (transitionResult.IsFailure) return transitionResult;
                    foreach (var item in order.Items.OrderBy(item => item.ProductId))
                    {
                        var product = await productRepository.GetByIdForUpdateAsync(
                            item.ProductId,
                            cancellationToken);
                        if (product is null)
                            return Result.Failure(
                                $"Product '{item.ProductId}' not found while restoring stock.");

                        var restoreResult = product.RestoreStock(item.Quantity);
                        if (restoreResult.IsFailure) return restoreResult;
                        productRepository.Update(product);
                        updatedProducts.Add((product, StockUpdateReasons.Restored));
                    }
                    break;

                default:
                    return Result.Failure("Unsupported payment webhook event.");
            }

            paymentRepository.Update(payment);
            orderRepository.Update(order);
            var messageType = webhook.Event switch
            {
                "payment.approved" => OutBoxMessageType.OrderPaid,
                "payment.declined" => OutBoxMessageType.PaymentFailed,
                "payment.refunded" => OutBoxMessageType.OrderRefunded,
                _ => throw new UnreachableException()
            };
            await outboxMessageRepository.AddAsync(
                OrderIntegrationEventFactory.Create(order, messageType),
                cancellationToken);
            foreach (var (product, reason) in updatedProducts)
            {
                await outboxMessageRepository.AddAsync(
                    StockIntegrationEventFactory.Create(product, reason, order.Id),
                    cancellationToken);
            }
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            await Task.WhenAll(order.Items.Select(item =>
                productCache.RemoveAsync(item.ProductId, CancellationToken.None)));
            return Result.Success();
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }

    private sealed record PaymentWebhook(string Event, PaymentWebhookData Data);
    private sealed record PaymentWebhookData(string Id, string Status);
}
