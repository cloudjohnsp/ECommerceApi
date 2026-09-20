using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders.Handlers;

public sealed class DeleteOrderHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteOrderCommand, Result>
{
    public async Task<Result> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = await orderRepository.GetByIdForUpdateAsync(request.OrderId, cancellationToken);
            if (order is null) return Result.Failure("Order not found.");
            if (order.Status == Domain.Enums.OrderStatus.Cancelled) return Result.Success();

            var result = order.Cancel();
            if (result.IsFailure) return result;

            foreach (var item in order.Items.OrderBy(item => item.ProductId))
            {
                var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                if (product is null)
                    return Result.Failure($"Product '{item.ProductId}' not found while restoring stock.");

                var restoreResult = product.RestoreStock(item.Quantity);
                if (restoreResult.IsFailure) return restoreResult;
                productRepository.Update(product);
            }

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
}
