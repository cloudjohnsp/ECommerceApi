using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders.Dtos;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Application.Products;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Orders.Handlers;

public sealed class UpdateOrderHandler(
    IOrderRepository orderRepository,
    IPaymentRepository paymentRepository,
    IProductRepository productRepository,
    IInventoryReservationRepository inventoryReservationRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache)
    : IRequestHandler<UpdateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await paymentRepository.GetByOrderIdForUpdateAsync(
                request.OrderId,
                cancellationToken);
            var order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (order is null) return Result<OrderDto>.Failure("Order not found.");
            if (order.Status == OrderStatus.Paid)
                return Result<OrderDto>.Success(order.ToDto());

            if (payment?.Status != PaymentStatus.Paid)
                return Result<OrderDto>.Failure(
                    "Order can only be reconciled after its payment is approved.");

            var result = request.Status == OrderStatus.Paid
                ? order.MarkAsPaid()
                : Result.Failure("Unsupported order status transition.");
            if (result.IsFailure) return Result<OrderDto>.Failure([.. result.Errors]);

            var reservations = await inventoryReservationRepository
                .GetActiveByOrderIdForUpdateAsync(order.Id, cancellationToken);
            var reservationsByProduct = reservations.ToDictionary(item => item.ProductId);
            var updatedProducts = new List<Product>();
            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                if (!reservationsByProduct.TryGetValue(item.ProductId, out var reservation))
                    return Result<OrderDto>.Failure(
                        $"Active inventory reservation for product '{item.ProductId}' was not found.");
                if (reservation.Quantity != item.Quantity)
                    return Result<OrderDto>.Failure(
                        $"Inventory reservation quantity for product '{item.ProductId}' does not match the order.");
                var product = await productRepository.GetByIdForUpdateAsync(
                    item.ProductId,
                    cancellationToken);
                if (product is null)
                    return Result<OrderDto>.Failure(
                        $"Product '{item.ProductId}' not found while reducing stock.");

                var stockResult = product.ReduceStock(reservation.Quantity);
                if (stockResult.IsFailure)
                    return Result<OrderDto>.Failure([.. stockResult.Errors]);
                var reservationResult = reservation.Consume(DateTimeOffset.UtcNow);
                if (reservationResult.IsFailure)
                    return Result<OrderDto>.Failure([.. reservationResult.Errors]);
                productRepository.Update(product);
                inventoryReservationRepository.Update(reservation);
                updatedProducts.Add(product);
            }

            orderRepository.Update(order);
            await outboxMessageRepository.AddAsync(
                OrderIntegrationEventFactory.Create(order, OutBoxMessageType.OrderPaid),
                cancellationToken);
            foreach (var product in updatedProducts)
            {
                await outboxMessageRepository.AddAsync(
                    StockIntegrationEventFactory.Create(
                        product,
                        StockUpdateReasons.ReservationConsumed,
                        order.Id),
                    cancellationToken);
            }
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            await Task.WhenAll(order.Items.Select(item =>
                productCache.RemoveAsync(item.ProductId, CancellationToken.None)));
            return Result<OrderDto>.Success(order.ToDto());
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
