using ECommerce.Application.Abstractions.Caching;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Shared.Results;
using MediatR;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Orders.Handlers;

public sealed class DeleteOrderHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
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

            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                if (product is null)
                    return Result.Failure($"Product '{item.ProductId}' not found while restoring stock.");

                var releaseResult = product.ReleaseReservedStock(item.Quantity);
                if (releaseResult.IsFailure) return releaseResult;
                productRepository.Update(product);
            }

            orderRepository.Update(order);
            await outboxMessageRepository.AddAsync(
                OrderIntegrationEventFactory.Create(order, OutBoxMessageType.OrderCancelled),
                cancellationToken);
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
