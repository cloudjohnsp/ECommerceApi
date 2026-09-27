using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Domain.Enums;
using ECommerce.Application.Products;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Orders.Handlers;

public sealed class DeleteOrderHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IInventoryReservationRepository inventoryReservationRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache) : IRequestHandler<DeleteOrderCommand, Result>
{
    public async Task<Result> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (order is null || request.CustomerId is { } customerId && order.CustomerId != customerId)
                return Result.Failure("Order not found.");
            if (order.Status == Domain.Enums.OrderStatus.Cancelled) return Result.Success();

            var result = order.Cancel();
            if (result.IsFailure) return result;

            var reservations = await inventoryReservationRepository
                .GetActiveByOrderIdForUpdateAsync(order.Id, cancellationToken);
            var reservationsByProduct = reservations.ToDictionary(item => item.ProductId);
            var updatedProducts = new List<Product>();
            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                if (!reservationsByProduct.TryGetValue(item.ProductId, out var reservation))
                    return Result.Failure(
                        $"Active inventory reservation for product '{item.ProductId}' was not found.");
                if (reservation.Quantity != item.Quantity)
                    return Result.Failure(
                        $"Inventory reservation quantity for product '{item.ProductId}' does not match the order.");
                var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                if (product is null)
                    return Result.Failure($"Product '{item.ProductId}' not found while restoring stock.");

                var releaseResult = product.ReleaseReservedStock(reservation.Quantity);
                if (releaseResult.IsFailure) return releaseResult;
                var reservationResult = reservation.Release(DateTimeOffset.UtcNow);
                if (reservationResult.IsFailure) return reservationResult;
                productRepository.Update(product);
                inventoryReservationRepository.Update(reservation);
                updatedProducts.Add(product);
            }

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
}
