using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders.Dtos;
using ECommerce.Domain.Entities;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders.Handlers;

public sealed class CreateOrderHandler(
    IOrderRepository orderRepository,
    IUserRepository userRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await userRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null || !customer.IsActive)
            return Result<OrderDto>.Failure("Customer not found or inactive.");

        var requestedItems = request.Items
            .GroupBy(item => item.ProductId)
            .Select(group => new CreateOrderItem(group.Key, group.Sum(item => item.Quantity)))
            .ToArray();

        var products = new List<(Product Product, int Quantity)>();
        foreach (var item in requestedItems)
        {
            var product = await productRepository.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is null) return Result<OrderDto>.Failure($"Product '{item.ProductId}' not found.");
            if (product.Stock < item.Quantity)
                return Result<OrderDto>.Failure($"Insufficient stock for product '{product.Name}'.");
            products.Add((product, item.Quantity));
        }

        var orderResult = Order.Create(request.CustomerId);
        if (orderResult.IsFailure) return Result<OrderDto>.Failure([.. orderResult.Errors]);
        var order = orderResult.Value!;

        foreach (var (product, quantity) in products)
        {
            var addResult = order.AddItem(product.Id, product.Name, product.Price, quantity);
            if (addResult.IsFailure) return Result<OrderDto>.Failure([.. addResult.Errors]);

            var stockResult = product.RemoveStock(quantity);
            if (stockResult.IsFailure) return Result<OrderDto>.Failure([.. stockResult.Errors]);
            productRepository.Update(product);
        }

        await orderRepository.AddAsync(order, cancellationToken);
        await unitOfWork.Commit(cancellationToken);
        return Result<OrderDto>.Success(order.ToDto());
    }
}
