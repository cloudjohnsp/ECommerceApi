using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Orders;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Products;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Messaging;
using ECommerce.Shared.Results;

namespace ECommerce.Application.Orders;

public sealed class OrderExpirationProcessor(
    IOrderRepository orderRepository,
    IInventoryReservationRepository inventoryReservationRepository,
    IProductRepository productRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache) : IOrderExpirationProcessor
{
    public async Task<Result<OrderExpirationBatchResult>> ProcessBatchAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            return Result<OrderExpirationBatchResult>.Failure(
                "Order expiration batch size must be greater than zero.");

        var candidates = await orderRepository.GetExpiredPendingCandidatesAsync(
            now,
            batchSize,
            cancellationToken);
        var expiredCount = 0;
        var skippedCount = 0;

        foreach (var candidate in candidates)
        {
            var result = await ExpireAsync(candidate.OrderId, now, cancellationToken);
            if (result.IsFailure)
                return Result<OrderExpirationBatchResult>.Failure([.. result.Errors]);
            if (result.Value)
                expiredCount++;
            else
                skippedCount++;
        }

        var maximumDelaySeconds = candidates.Count == 0
            ? 0
            : Math.Max(0, candidates.Max(candidate => (now - candidate.ExpiresAt).TotalSeconds));
        return Result<OrderExpirationBatchResult>.Success(new OrderExpirationBatchResult(
            candidates.Count,
            expiredCount,
            skippedCount,
            maximumDelaySeconds));
    }

    private async Task<Result<bool>> ExpireAsync(
        Guid orderId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orderRepository.GetByIdForUpdateAsync(orderId, cancellationToken);
            if (order is null || order.Status != OrderStatus.Pending || order.ExpiresAt > now)
                return Result<bool>.Success(false);

            var reservations = await inventoryReservationRepository
                .GetActiveByOrderIdForUpdateAsync(order.Id, cancellationToken);
            var reservationsByProduct = reservations.ToDictionary(item => item.ProductId);
            var updatedProducts = new List<Product>();

            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                if (!reservationsByProduct.TryGetValue(item.ProductId, out var reservation))
                    return Result<bool>.Failure(
                        $"Active inventory reservation for product '{item.ProductId}' was not found.");
                if (reservation.Quantity != item.Quantity)
                    return Result<bool>.Failure(
                        $"Inventory reservation quantity for product '{item.ProductId}' does not match the order.");

                var product = await productRepository.GetByIdForUpdateAsync(
                    item.ProductId,
                    cancellationToken);
                if (product is null)
                    return Result<bool>.Failure(
                        $"Product '{item.ProductId}' not found while expiring the order.");

                var releaseResult = product.ReleaseReservedStock(reservation.Quantity);
                if (releaseResult.IsFailure)
                    return Result<bool>.Failure([.. releaseResult.Errors]);
                var expirationResult = reservation.Expire(now);
                if (expirationResult.IsFailure)
                    return Result<bool>.Failure([.. expirationResult.Errors]);

                productRepository.Update(product);
                inventoryReservationRepository.Update(reservation);
                updatedProducts.Add(product);
            }

            var cancellationResult = order.Cancel(OrderCancellationReason.Expired, now);
            if (cancellationResult.IsFailure)
                return Result<bool>.Failure([.. cancellationResult.Errors]);
            orderRepository.Update(order);
            await outboxMessageRepository.AddAsync(
                OrderIntegrationEventFactory.Create(order, OutBoxMessageType.OrderCancelled),
                cancellationToken);
            foreach (var product in updatedProducts)
            {
                await outboxMessageRepository.AddAsync(
                    StockIntegrationEventFactory.Create(
                        product,
                        StockUpdateReasons.ReservationReleased,
                        order.Id),
                    cancellationToken);
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            await Task.WhenAll(updatedProducts.Select(product =>
                productCache.RemoveAsync(product.Id, CancellationToken.None)));
            return Result<bool>.Success(true);
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
