using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Application.Products;
using ECommerce.Shared.Messaging;

namespace ECommerce.Application.Orders.Handlers;

public sealed class AddOrderItemHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IInventoryReservationRepository inventoryReservationRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache) : IRequestHandler<AddOrderItemCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        AddOrderItemCommand request,
        CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (order is null || request.CustomerId is { } customerId && order.CustomerId != customerId)
                return Result<OrderDto>.Failure("Order not found.");

            var product = await productRepository.GetByIdForUpdateAsync(
                request.ProductId,
                cancellationToken);
            if (product is null || !product.IsActive)
                return Result<OrderDto>.Failure("Product not found or inactive.");
            if (product.AvailableStock < request.Quantity)
                return Result<OrderDto>.Failure($"Insufficient available stock for product '{product.Name}'.");

            var addResult = order.AddItem(
                product.Id,
                product.Name,
                product.Price,
                request.Quantity);
            if (addResult.IsFailure)
                return Result<OrderDto>.Failure([.. addResult.Errors]);

            var reserveResult = product.ReserveStock(request.Quantity);
            if (reserveResult.IsFailure)
                return Result<OrderDto>.Failure([.. reserveResult.Errors]);

            var reservationCreatedAt = DateTimeOffset.UtcNow;
            var reservationResult = InventoryReservation.Create(
                order.Id,
                product.Id,
                product.Inventory.Id,
                request.Quantity,
                reservationCreatedAt,
                reservationCreatedAt.Add(InventoryReservation.DefaultLifetime));
            if (reservationResult.IsFailure)
                return Result<OrderDto>.Failure([.. reservationResult.Errors]);

            orderRepository.Update(order);
            productRepository.Update(product);
            await inventoryReservationRepository.AddAsync(
                reservationResult.Value!,
                cancellationToken);
            await outboxMessageRepository.AddAsync(
                OrderIntegrationEventFactory.Create(order, OutBoxMessageType.OrderUpdated),
                cancellationToken);
            await outboxMessageRepository.AddAsync(
                StockIntegrationEventFactory.Create(
                    product,
                    StockUpdateReasons.Reserved,
                    order.Id),
                cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            await productCache.RemoveAsync(product.Id, CancellationToken.None);
            return Result<OrderDto>.Success(order.ToDto());
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
}
