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
        var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return Result.Failure("Order not found.");
        if (order.Status == Domain.Enums.OrderStatus.Cancelled) return Result.Success();

        var result = order.Cancel();
        if (result.IsFailure) return result;

        foreach (var item in order.Items)
        {
            var product = await productRepository.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is null) return Result.Failure($"Product '{item.ProductId}' not found while restoring stock.");
            product.RestoreStock(item.Quantity);
            productRepository.Update(product);
        }

        orderRepository.Update(order);
        await unitOfWork.Commit(cancellationToken);
        return Result.Success();
    }
}
