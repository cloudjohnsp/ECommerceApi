using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders.Handlers;

public sealed class GetOrderByIdHandler(IOrderRepository repository)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        return order is null || request.CustomerId is { } customerId && order.CustomerId != customerId
            ? Result<OrderDto>.Failure("Order not found.")
            : Result<OrderDto>.Success(order.ToDto());
    }
}

public sealed class GetOrdersHandler(IOrderRepository repository)
    : IRequestHandler<GetOrdersQuery, Result<IReadOnlyCollection<OrderDto>>>
{
    public async Task<Result<IReadOnlyCollection<OrderDto>>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = request.CustomerId is { } customerId
            ? await repository.GetByCustomerIdAsync(customerId, cancellationToken)
            : await repository.GetAllAsync(cancellationToken);
        IReadOnlyCollection<OrderDto> result = [.. orders.Select(order => order.ToDto())];
        return Result<IReadOnlyCollection<OrderDto>>.Success(result);
    }
}
