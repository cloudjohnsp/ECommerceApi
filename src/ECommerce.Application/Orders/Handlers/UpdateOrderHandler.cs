using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Orders.Dtos;
using ECommerce.Domain.Enums;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Orders.Handlers;

public sealed class UpdateOrderHandler(IOrderRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return Result<OrderDto>.Failure("Order not found.");

        var result = request.Status == OrderStatus.Paid
            ? order.MarkAsPaid()
            : Result.Failure("Unsupported order status transition.");
        if (result.IsFailure) return Result<OrderDto>.Failure([.. result.Errors]);

        repository.Update(order);
        await unitOfWork.Commit(cancellationToken);
        return Result<OrderDto>.Success(order.ToDto());
    }
}
