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

public sealed class CreateOrderHandler(
    IOrderRepository orderRepository,
    IUserRepository userRepository,
    IProductRepository productRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    IProductCache productCache) : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var transactionCommitted = false;

        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var customer = await userRepository.GetByIdAsync(request.CustomerId, cancellationToken);
            if (customer is null || !customer.IsActive)
                return Result<OrderDto>.Failure("Customer not found or inactive.");

            var requestedItems = request.Items
                .GroupBy(item => item.ProductId)
                .Select(group => new CreateOrderItem(group.Key, group.Sum(item => item.Quantity)))
                .OrderBy(item => item.ProductId)
                .ToArray();

            var products = new List<(Product Product, int Quantity)>();
            foreach (var item in requestedItems)
            {
                var product = await productRepository.GetByIdForUpdateAsync(item.ProductId, cancellationToken);
                if (product is null) return Result<OrderDto>.Failure($"Product '{item.ProductId}' not found.");
                if (product.AvailableStock < item.Quantity)
                    return Result<OrderDto>.Failure($"Insufficient available stock for product '{product.Name}'.");
                products.Add((product, item.Quantity));
            }

            var orderResult = Order.Create(request.CustomerId);
            if (orderResult.IsFailure) return Result<OrderDto>.Failure([.. orderResult.Errors]);
            var order = orderResult.Value!;

            foreach (var (product, quantity) in products)
            {
                var addResult = order.AddItem(product.Id, product.Name, product.Price, quantity);
                if (addResult.IsFailure) return Result<OrderDto>.Failure([.. addResult.Errors]);

                var stockResult = product.ReserveStock(quantity);
                if (stockResult.IsFailure) return Result<OrderDto>.Failure([.. stockResult.Errors]);
                productRepository.Update(product);
            }

            var orderDto = order.ToDto();
            var outboxMessage = OrderIntegrationEventFactory.Create(
                order,
                OutBoxMessageType.OrderCreated,
                customer.Email.Value);

            await orderRepository.AddAsync(order, cancellationToken);
            await outboxMessageRepository.AddAsync(outboxMessage, cancellationToken);
            foreach (var (product, _) in products)
            {
                await outboxMessageRepository.AddAsync(
                    StockIntegrationEventFactory.Create(
                        product,
                        StockUpdateReasons.Reserved,
                        order.Id),
                    cancellationToken);
            }
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            transactionCommitted = true;
            await Task.WhenAll(products.Select(item =>
                productCache.RemoveAsync(item.Product.Id, CancellationToken.None)));

            return Result<OrderDto>.Success(orderDto);
        }
        finally
        {
            if (!transactionCommitted)
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
        }
    }
};
