using System.Text.Json;
using System.Diagnostics;
using System.Globalization;
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
    IInventoryReservationRepository inventoryReservationRepository,
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
        var webhookValidation = ValidateWebhook(webhook);
        if (webhookValidation.IsFailure)
            return webhookValidation;

        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await paymentRepository.GetByExternalIdForUpdateAsync(
                webhook.Data.Id,
                cancellationToken);
            if (payment is null) return Result.Failure("Payment not found.");

            var paymentIdentityValidation = ValidatePaymentIdentity(webhook.Data, payment);
            if (paymentIdentityValidation.IsFailure) return paymentIdentityValidation;

            if (webhook.Event == "payment.approved" && payment.Status == PaymentStatus.Paid)
                return Result.Success();
            if (webhook.Event == "payment.declined" && payment.Status == PaymentStatus.Failed)
                return Result.Success();
            if (webhook.Event == "payment.refunded" && payment.Status == PaymentStatus.Refunded)
                return Result.Success();
            if (webhook.Event == "payment.declined" && payment.Status == PaymentStatus.Paid)
                return Result.Success();
            if (payment.Status is PaymentStatus.Failed or PaymentStatus.Refunded)
                return Result.Success();

            var order = await orderRepository.GetByIdForUpdateAsync(payment.OrderId, cancellationToken);
            if (order is null) return Result.Failure("Order not found.");

            IReadOnlyDictionary<Guid, InventoryReservation> activeReservations =
                new Dictionary<Guid, InventoryReservation>();
            if (webhook.Event == "payment.approved")
            {
                activeReservations = (await inventoryReservationRepository
                        .GetActiveByOrderIdForUpdateAsync(order.Id, cancellationToken))
                    .ToDictionary(item => item.ProductId);
            }

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
                        if (!activeReservations.TryGetValue(item.ProductId, out var reservation))
                            return Result.Failure(
                                $"Active inventory reservation for product '{item.ProductId}' was not found.");
                        if (reservation.Quantity != item.Quantity)
                            return Result.Failure(
                                $"Inventory reservation quantity for product '{item.ProductId}' does not match the order.");
                        var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                        if (product is null)
                            return Result.Failure($"Product '{item.ProductId}' not found while reducing stock.");
                        var reduceResult = product.ReduceStock(reservation.Quantity);
                        if (reduceResult.IsFailure) return reduceResult;
                        var reservationResult = reservation.Consume(DateTimeOffset.UtcNow);
                        if (reservationResult.IsFailure) return reservationResult;
                        productRepository.Update(product);
                        inventoryReservationRepository.Update(reservation);
                        updatedProducts.Add((product, StockUpdateReasons.ReservationConsumed));
                    }
                    break;

                case "payment.declined":
                    transitionResult = payment.MarkAsFailed();
                    if (transitionResult.IsFailure) return transitionResult;
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
            if (updatedProducts.Count > 0)
            {
                await Task.WhenAll(updatedProducts.Select(item =>
                    productCache.RemoveAsync(item.Product.Id, CancellationToken.None)));
            }
            return Result.Success();
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }

    private static Result ValidateWebhook(PaymentWebhook webhook)
    {
        var expectedStatus = webhook.Event switch
        {
            "payment.approved" => "approved",
            "payment.declined" => "declined",
            "payment.refunded" => "refunded",
            _ => null
        };
        if (expectedStatus is null)
            return Result.Failure("Unsupported payment webhook event.");
        return string.Equals(
                webhook.Data.Status?.Trim(),
                expectedStatus,
                StringComparison.OrdinalIgnoreCase)
            ? Result.Success()
            : Result.Failure("Payment webhook event and status do not match.");
    }

    private static Result ValidatePaymentIdentity(PaymentWebhookData data, Payment payment)
    {
        var referenceMatches = Guid.TryParse(data.Reference, out var referencedOrderId) &&
                               referencedOrderId == payment.OrderId;
        var amountMatches = decimal.TryParse(
                                data.Amount,
                                NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture,
                                out var webhookAmount) &&
                            webhookAmount == payment.Amount;
        var currencyMatches = string.Equals(
            data.Currency,
            payment.Currency,
            StringComparison.Ordinal);

        return referenceMatches && amountMatches && currencyMatches
            ? Result.Success()
            : Result.Failure("Payment webhook data does not match the local payment.");
    }

    private sealed record PaymentWebhook(string Event, PaymentWebhookData Data);
    private sealed record PaymentWebhookData(
        string Id,
        string Status,
        string? Reference,
        string? Amount,
        string? Currency);
}
