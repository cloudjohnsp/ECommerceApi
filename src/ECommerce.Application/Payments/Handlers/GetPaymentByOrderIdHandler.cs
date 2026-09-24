using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Payments.Dtos;
using ECommerce.Shared.Results;
using MediatR;

namespace ECommerce.Application.Payments.Handlers;

public sealed class GetPaymentByOrderIdHandler(
    IPaymentRepository paymentRepository,
    IOrderRepository orderRepository)
    : IRequestHandler<GetPaymentByOrderIdQuery, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        GetPaymentByOrderIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId is { } customerId)
        {
            var order = await orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
            if (order is null || order.CustomerId != customerId)
                return Result<PaymentDto>.Failure("Payment not found.");
        }

        var payment = await paymentRepository.GetByOrderIdAsync(request.OrderId, cancellationToken);
        return payment is null
            ? Result<PaymentDto>.Failure("Payment not found.")
            : Result<PaymentDto>.Success(payment.ToDto());
    }
}
