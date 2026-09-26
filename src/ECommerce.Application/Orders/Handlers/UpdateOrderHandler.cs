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
            var order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (order is null) return Result<OrderDto>.Failure("Order not found.");
            if (order.Status == OrderStatus.Paid)
                return Result<OrderDto>.Success(order.ToDto());

            var payment = await paymentRepository.GetByOrderIdForUpdateAsync(
                order.Id,
                cancellationToken);
            if (payment?.Status != PaymentStatus.Paid)
                return Result<OrderDto>.Failure(
                    "Order can only be reconciled after its payment is approved.");

            var result = request.Status == OrderStatus.Paid
                ? order.MarkAsPaid()
                : Result.Failure("Unsupported order status transition.");
            if (result.IsFailure) return Result<OrderDto>.Failure([.. result.Errors]);

            var updatedProducts = new List<Product>();
            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                var product = await productRepository.GetByIdForUpdateAsync(
                    item.ProductId,
                    cancellationToken);
                if (product is null)
                    return Result<OrderDto>.Failure(
                        $"Product '{item.ProductId}' not found while reducing stock.");

                var stockResult = product.ReduceStock(item.Quantity);
                if (stockResult.IsFailure)
                    return Result<OrderDto>.Failure([.. stockResult.Errors]);
                productRepository.Update(product);
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
