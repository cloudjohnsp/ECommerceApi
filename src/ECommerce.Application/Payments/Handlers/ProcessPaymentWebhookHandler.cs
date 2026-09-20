using System.Text.Json;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Payments.Handlers;

public sealed class ProcessPaymentWebhookHandler(
    IPaymentWebhookSignatureVerifier signatureVerifier,
    IPaymentRepository paymentRepository,
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ProcessPaymentWebhookCommand, Result>
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

            var order = await orderRepository.GetByIdAsync(payment.OrderId, cancellationToken);
            if (order is null) return Result.Failure("Order not found.");

            Result transitionResult;
            switch (webhook.Event)
            {
                case "payment.approved":
                    transitionResult = payment.MarkAsPaid(webhook.Data.Id);
                    if (transitionResult.IsFailure) return transitionResult;

                    transitionResult = order.MarkAsPaid();
                    if (transitionResult.IsFailure) return transitionResult;
                    break;

                case "payment.declined":
                    transitionResult = payment.MarkAsFailed();
                    if (transitionResult.IsFailure) return transitionResult;

                    transitionResult = order.Cancel();
                    if (transitionResult.IsFailure) return transitionResult;

                    foreach (var item in order.Items)
                    {
                        var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                        if (product is null)
                            return Result.Failure($"Product '{item.ProductId}' not found while restoring stock.");
                        product.RestoreStock(item.Quantity);
                        productRepository.Update(product);
                    }
                    break;

                default:
                    return Result.Failure("Unsupported payment webhook event.");
            }

            paymentRepository.Update(payment);
            orderRepository.Update(order);
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

    private sealed record PaymentWebhook(string Event, PaymentWebhookData Data);
    private sealed record PaymentWebhookData(string Id, string Status);
}
